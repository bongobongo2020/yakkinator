using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using IndexTTSStudio.Helpers;
using IndexTTSStudio.Models;
using NAudio.Wave;

namespace IndexTTSStudio.Services;

/// <summary>Sampling settings Echo-TTS fixes at server launch (they can't be sent per request).</summary>
public record EchoSamplerSettings(int Steps, float CfgText, float CfgSpeaker, int Seed);

/// <summary>
/// Runs the Echo-TTS C++ server (<c>echo-tts serve</c>). The server encodes its voices and
/// fixes its sampler settings at startup, so it is relaunched whenever a request needs a
/// voice it doesn't know or different settings. Recently used voices are kept registered
/// so switching back and forth between them doesn't reload the model.
/// </summary>
public class EchoBackendService : IDisposable
{
    private const int MaxLogChars = 2_000_000;
    private const int MaxRegisteredVoices = 6;

    private readonly int _port;
    private readonly SettingsService _settingsService;
    private readonly HttpClient _probe = new() { Timeout = TimeSpan.FromSeconds(5) };
    // Long texts are synthesized chunk by chunk and can take many minutes
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(30) };
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly StringBuilder _fullLog = new();
    private readonly object _logLock = new();

    private Process? _process;
    private EchoSamplerSettings? _launchedSettings;
    // Most recently used first: (server voice name, WAV path handed to the server)
    private readonly List<(string Name, string WavPath)> _voices = [];

    public event Action<string>? OnLog;
    public bool IsRunning => _process is { HasExited: false };
    public string FullLog { get { lock (_logLock) return _fullLog.ToString(); } }

    private string BaseUrl => $"http://127.0.0.1:{_port}";

    public EchoBackendService(SettingsService settingsService)
    {
        _settingsService = settingsService;
        _port = settingsService.Settings.EchoApiPort;
    }

    public static bool IsInstalled =>
        File.Exists(PathHelper.EchoExe) && File.Exists(PathHelper.EchoModelFile) &&
        File.Exists(PathHelper.EchoDacEncoder) && File.Exists(PathHelper.EchoDacDecoder);

    private void AppendLog(string prefix, string line)
    {
        lock (_logLock)
        {
            if (_fullLog.Length > MaxLogChars)
                _fullLog.Remove(0, _fullLog.Length - MaxLogChars / 2);
            _fullLog.Append(prefix).AppendLine(line);
        }
        OnLog?.Invoke(line);
    }

    /// <summary>Synthesizes <paramref name="text"/> and saves the WAV like IndexTTS outputs.</summary>
    public async Task<TTSResponse> GenerateAsync(string text, string voicePath, EchoSamplerSettings settings,
        Action<string>? onStatus = null, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var voiceName = await EnsureServerAsync(voicePath, settings, onStatus, ct).ConfigureAwait(false);

            onStatus?.Invoke("Generating speech...");
            using var response = await _http.PostAsJsonAsync($"{BaseUrl}/v1/audio/speech", new
            {
                model = "echo-tts",
                input = text,
                voice = voiceName,
                response_format = "wav",
            }, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                return new TTSResponse { Success = false, ErrorMessage = error };
            }

            var jobId = OutputHelper.NewJobId();
            var audioData = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
            var outputPath = await OutputHelper.SaveAsync(audioData, jobId,
                _settingsService.Settings.OutputDirectory, ct).ConfigureAwait(false);
            return new TTSResponse { Success = true, AudioFilePath = outputPath, JobId = jobId };
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Makes sure a server is up with this voice and these settings; returns the voice's server name.</summary>
    private async Task<string> EnsureServerAsync(string voicePath, EchoSamplerSettings settings,
        Action<string>? onStatus, CancellationToken ct)
    {
        var (name, wavPath) = PrepareVoice(voicePath);
        var index = _voices.FindIndex(v => v.Name == name);

        if (IsRunning && index >= 0 && settings == _launchedSettings)
        {
            // Keep it at the front so it survives the next eviction
            _voices.RemoveAt(index);
            _voices.Insert(0, (name, wavPath));
            return name;
        }

        if (index >= 0) _voices.RemoveAt(index);
        _voices.Insert(0, (name, wavPath));
        if (_voices.Count > MaxRegisteredVoices)
            _voices.RemoveRange(MaxRegisteredVoices, _voices.Count - MaxRegisteredVoices);
        // Voices deleted or moved since they were registered would make the server refuse to start
        _voices.RemoveAll(v => !File.Exists(v.WavPath));

        onStatus?.Invoke(IsRunning ? "Reloading Echo-TTS for the new voice/settings..." : "Loading Echo-TTS model...");
        await StopAsync().ConfigureAwait(false);
        try
        {
            await StartAsync(settings, ct).ConfigureAwait(false);
        }
        catch
        {
            // One unreadable older voice would otherwise break every later launch
            _voices.RemoveRange(1, _voices.Count - 1);
            throw;
        }
        return name;
    }

    /// <summary>Gives each voice file (by path and modification time) a stable server name; converts non-WAV audio.</summary>
    private static (string Name, string WavPath) PrepareVoice(string voicePath)
    {
        var fullPath = Path.GetFullPath(voicePath);
        var key = $"{fullPath.ToLowerInvariant()}|{File.GetLastWriteTimeUtc(fullPath).Ticks}";
        var name = "v" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..12].ToLowerInvariant();

        if (Path.GetExtension(fullPath).Equals(".wav", StringComparison.OrdinalIgnoreCase))
            return (name, fullPath);

        Directory.CreateDirectory(PathHelper.EchoVoiceCacheDir);
        var wavPath = Path.Combine(PathHelper.EchoVoiceCacheDir, name + ".wav");
        if (!File.Exists(wavPath))
        {
            // Media Foundation decodes MP3 and FLAC; Echo-TTS resamples and downmixes itself
            var tmp = wavPath + ".tmp";
            using (var reader = new MediaFoundationReader(fullPath))
                WaveFileWriter.CreateWaveFile(tmp, reader);
            File.Move(tmp, wavPath, overwrite: true);
        }
        return (name, wavPath);
    }

    private async Task StartAsync(EchoSamplerSettings settings, CancellationToken ct)
    {
        if (!IsInstalled)
            throw new InvalidOperationException("Echo-TTS is not installed. Install it from the Setup page.");

        KillStaleServers();

        var psi = new ProcessStartInfo
        {
            FileName = PathHelper.EchoExe,
            // Its CUDA/cuDNN/ONNX Runtime DLLs sit next to the exe
            WorkingDirectory = PathHelper.EchoBinDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        string[] args =
        [
            "serve",
            "--model", PathHelper.EchoModelFile,
            "--dac-encoder", PathHelper.EchoDacEncoder,
            "--dac-decoder", PathHelper.EchoDacDecoder,
            "--host", "127.0.0.1",
            "--port", _port.ToString(),
            "--steps", settings.Steps.ToString(),
            "--cfg-text", settings.CfgText.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--cfg-speaker", settings.CfgSpeaker.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--seed", settings.Seed.ToString(),
        ];
        foreach (var a in args) psi.ArgumentList.Add(a);
        foreach (var (name, wavPath) in _voices)
        {
            psi.ArgumentList.Add("--voice");
            psi.ArgumentList.Add($"{name}={wavPath}");
        }

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) => { if (e.Data != null) AppendLog("[STDOUT] ", e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) AppendLog("[STDERR] ", e.Data); };
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        _process = process;

        try
        {
            await WaitUntilReadyAsync(process, ct).ConfigureAwait(false);
            _launchedSettings = settings;
        }
        catch
        {
            // Don't leave a half-loaded server holding the port and GPU memory
            _launchedSettings = null;
            try { process.Kill(entireProcessTree: true); } catch { }
            throw;
        }
    }

    private async Task WaitUntilReadyAsync(Process process, CancellationToken ct)
    {
        var maxWait = TimeSpan.FromMinutes(5);
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < maxWait)
        {
            ct.ThrowIfCancellationRequested();
            if (process.HasExited)
            {
                throw new InvalidOperationException(
                    $"Echo-TTS exited while starting (exit code: {process.ExitCode}). " +
                    "It needs an NVIDIA GPU with a recent driver (CUDA 12). " +
                    $"Full log: {SaveErrorLog()}");
            }

            try
            {
                using var resp = await _probe.GetAsync($"{BaseUrl}/health", ct).ConfigureAwait(false);
                if (resp.IsSuccessStatusCode) return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                // not listening yet
            }
            await Task.Delay(1000, ct).ConfigureAwait(false);
        }

        throw new TimeoutException(
            $"Echo-TTS failed to start within {maxWait.TotalMinutes:F0} minutes. Full log: {SaveErrorLog()}");
    }

    /// <summary>Saves the server log next to the app's other logs and returns its path.</summary>
    public string SaveErrorLog()
    {
        var path = Path.Combine(PathHelper.AppDataDir, "echo_error.log");
        try { File.WriteAllText(path, FullLog); } catch { }
        return path;
    }

    /// <summary>A server orphaned by a previous crash would hold the port (it has no shutdown endpoint).</summary>
    private static void KillStaleServers()
    {
        foreach (var p in Process.GetProcessesByName("echo-tts"))
        {
            try
            {
                if (string.Equals(p.MainModule?.FileName, PathHelper.EchoExe, StringComparison.OrdinalIgnoreCase))
                {
                    p.Kill(entireProcessTree: true);
                    p.WaitForExit(3000);
                }
            }
            catch { /* exited already, or not ours */ }
            finally { p.Dispose(); }
        }
    }

    public Task StopAsync()
    {
        var process = _process;
        _process = null;
        _launchedSettings = null;
        if (process == null) return Task.CompletedTask;
        return Task.Run(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5000);
                }
            }
            catch (InvalidOperationException) { /* exited in the meantime */ }
            finally { process.Dispose(); }
        });
    }

    public void Dispose()
    {
        try { StopAsync().Wait(TimeSpan.FromSeconds(8)); }
        catch (AggregateException) { /* best effort on exit */ }
        _probe.Dispose();
        _http.Dispose();
        GC.SuppressFinalize(this);
    }
}
