using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using IndexTTSStudio.Helpers;

namespace IndexTTSStudio.Services;

/// <summary>One designed candidate voice: the clip on disk and the seed that produced it.</summary>
public record VoiceDesignCandidate(string FilePath, long Seed, string Description, string Transcript, string Language)
{
    public string Label => $"Seed {Seed}";
}

/// <summary>
/// Runs python/voicedesign_server.py (Qwen3-TTS VoiceDesign) from its own venv. It creates an
/// original voice from a text description; the clip the user picks goes into the voice library,
/// where Echo-TTS and IndexTTS-2 clone it. It only loads while the user is designing: the caller
/// unloads the speech engines first, and generating speech stops it again.
/// </summary>
public class VoiceDesignService : IDisposable
{
    public const string ModelName = "Qwen3-TTS-12Hz-1.7B-VoiceDesign";
    private const int MaxLogChars = 2_000_000;
    private const int MaxCandidateFiles = 24;

    private readonly int _port;
    private readonly HttpClient _probe = new() { Timeout = TimeSpan.FromSeconds(5) };
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(10) };
    private readonly SemaphoreSlim _startGate = new(1, 1);
    private readonly StringBuilder _fullLog = new();
    private readonly object _logLock = new();

    private Process? _process;
    private CancellationTokenSource? _startCts;

    /// <summary>Raised (on a worker thread) when the server becomes ready or stops.</summary>
    public event Action? StateChanged;

    public bool IsRunning => _process is { HasExited: false };
    public bool IsReady { get; private set; }
    public string FullLog { get { lock (_logLock) return _fullLog.ToString(); } }

    private string BaseUrl => $"http://127.0.0.1:{_port}";

    /// <summary>Languages the model accepts ("Auto" lets it pick from the text).</summary>
    public static IReadOnlyList<string> Languages { get; } =
        ["Auto", "English", "Chinese", "French", "German", "Italian", "Japanese", "Korean", "Portuguese", "Russian", "Spanish"];

    public VoiceDesignService(SettingsService settingsService)
    {
        _port = settingsService.Settings.VoiceDesignApiPort;
    }

    public static bool IsInstalled =>
        File.Exists(PathHelper.VoiceDesignPython) &&
        File.Exists(Path.Combine(PathHelper.VoiceDesignModelDir, "model.safetensors")) &&
        File.Exists(Path.Combine(PathHelper.VoiceDesignModelDir, "speech_tokenizer", "model.safetensors"));

    private void AppendLog(string prefix, string line)
    {
        lock (_logLock)
        {
            if (_fullLog.Length > MaxLogChars)
                _fullLog.Remove(0, _fullLog.Length - MaxLogChars / 2);
            _fullLog.Append(prefix).AppendLine(line);
        }
    }

    /// <summary>Designs one candidate. Pass <paramref name="seed"/> to reproduce an earlier one.</summary>
    public async Task<VoiceDesignCandidate> DesignAsync(string description, string transcript, string language,
        long? seed = null, Action<string>? onStatus = null, CancellationToken ct = default)
    {
        await EnsureStartedAsync(onStatus, ct).ConfigureAwait(false);

        using var response = await _http.PostAsJsonAsync($"{BaseUrl}/api/design", new
        {
            text = transcript,
            instruct = description,
            language = string.IsNullOrWhiteSpace(language) ? "Auto" : language,
            seed,
        }, ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            throw new InvalidOperationException(ExtractDetail(error));
        }

        var usedSeed = response.Headers.TryGetValues("X-Seed", out var seeds) && long.TryParse(seeds.First(), out var s)
            ? s : seed ?? 0;
        var audio = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);

        Directory.CreateDirectory(PathHelper.VoiceDesignCandidatesDir);
        var path = Path.Combine(PathHelper.VoiceDesignCandidatesDir, $"candidate_{DateTime.Now:yyyyMMdd_HHmmss_fff}_{usedSeed}.wav");
        await File.WriteAllBytesAsync(path, audio, ct).ConfigureAwait(false);
        PruneCandidates();
        return new VoiceDesignCandidate(path, usedSeed, description, transcript, language);
    }

    private static void PruneCandidates()
    {
        try
        {
            foreach (var old in new DirectoryInfo(PathHelper.VoiceDesignCandidatesDir).EnumerateFiles("*.wav")
                         .OrderByDescending(f => f.LastWriteTimeUtc).Skip(MaxCandidateFiles))
            {
                try { old.Delete(); } catch { /* may be playing */ }
            }
        }
        catch (IOException) { }
    }

    private static string ExtractDetail(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("detail", out var d) && d.ValueKind == JsonValueKind.String)
                return d.GetString() ?? body;
        }
        catch (JsonException) { }
        return string.IsNullOrWhiteSpace(body) ? "The voice designer failed" : body;
    }

    private async Task EnsureStartedAsync(Action<string>? onStatus, CancellationToken ct)
    {
        if (IsReady && IsRunning) return;

        await _startGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (IsReady && IsRunning) return;
            if (!IsInstalled)
                throw new InvalidOperationException("The voice designer isn't installed. Install it from the Setup page.");

            onStatus?.Invoke("Loading the voice designer (first load takes a minute)...");
            _startCts = new CancellationTokenSource();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _startCts.Token);
            await StartAsync(linked.Token).ConfigureAwait(false);
        }
        finally
        {
            _startCts?.Dispose();
            _startCts = null;
            _startGate.Release();
        }
    }

    private async Task StartAsync(CancellationToken ct)
    {
        await ShutdownStaleServerAsync(ct).ConfigureAwait(false);

        var psi = new ProcessStartInfo
        {
            FileName = PathHelper.VoiceDesignPython,
            WorkingDirectory = PathHelper.VoiceDesignDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in new[] { PathHelper.VoiceDesignServerScript, "--port", _port.ToString(), "--model-dir", PathHelper.VoiceDesignModelDir })
            psi.ArgumentList.Add(a);
        psi.Environment["PYTHONUTF8"] = "1";
        // Everything is in the local model folder; don't let transformers phone home on every load
        psi.Environment["HF_HUB_OFFLINE"] = "1";

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) => { if (e.Data != null) AppendLog("[STDOUT] ", e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) AppendLog("[STDERR] ", e.Data); };
        process.Exited += (_, _) =>
        {
            if (!ReferenceEquals(_process, process)) return;
            IsReady = false;
            StateChanged?.Invoke();
        };
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        _process = process;

        try
        {
            await WaitUntilReadyAsync(process, ct).ConfigureAwait(false);
            IsReady = true;
            StateChanged?.Invoke();
        }
        catch
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            throw;
        }
    }

    private async Task WaitUntilReadyAsync(Process process, CancellationToken ct)
    {
        var maxWait = TimeSpan.FromMinutes(6);
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < maxWait)
        {
            ct.ThrowIfCancellationRequested();
            if (process.HasExited)
            {
                throw new InvalidOperationException(
                    $"The voice designer exited while loading (exit code: {process.ExitCode}). " +
                    $"{LastErrorLine()}Full log: {SaveErrorLog()}");
            }

            try
            {
                using var resp = await _probe.GetAsync($"{BaseUrl}/api/health", ct).ConfigureAwait(false);
                if (resp.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
                    if (doc.RootElement.TryGetProperty("status", out var s) && s.GetString() == "ready") return;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                // not listening yet
            }
            await Task.Delay(1000, ct).ConfigureAwait(false);
        }

        throw new TimeoutException(
            $"The voice designer failed to load within {maxWait.TotalMinutes:F0} minutes. Full log: {SaveErrorLog()}");
    }

    private string LastErrorLine()
    {
        var line = FullLog.Split('\n').LastOrDefault(l => l.Contains("FATAL") || l.Contains("Error"))?.Trim();
        if (string.IsNullOrEmpty(line)) return "";
        if (line.Length > 300) line = line[..300] + "...";
        return line + "\n\n";
    }

    public string SaveErrorLog()
    {
        var path = Path.Combine(PathHelper.AppDataDir, "voicedesign_error.log");
        try { File.WriteAllText(path, FullLog); } catch { }
        return path;
    }

    private async Task ShutdownStaleServerAsync(CancellationToken ct)
    {
        try
        {
            using var health = await _probe.GetAsync($"{BaseUrl}/api/health", ct).ConfigureAwait(false);
            if (!health.IsSuccessStatusCode) return;
            using var _ = await _probe.PostAsync($"{BaseUrl}/api/shutdown", null, ct).ConfigureAwait(false);
            await Task.Delay(1500, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException) { /* nothing listening: the normal case */ }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { /* timed out */ }
    }

    /// <summary>Stops the server (cancelling a load in progress) to free GPU memory.</summary>
    public async Task StopAsync()
    {
        try { _startCts?.Cancel(); } catch (ObjectDisposedException) { }

        var process = _process;
        _process = null;
        var wasReady = IsReady;
        IsReady = false;
        if (wasReady) StateChanged?.Invoke();
        if (process == null) return;

        try
        {
            if (!process.HasExited)
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                try { using var _ = await _probe.PostAsync($"{BaseUrl}/api/shutdown", null, cts.Token).ConfigureAwait(false); }
                catch { /* fall through to kill */ }
                using var exitCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                try { await process.WaitForExitAsync(exitCts.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) { }
                if (!process.HasExited) process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException) { /* exited in the meantime */ }
        finally { process.Dispose(); }
    }

    public void Dispose()
    {
        try { Task.Run(StopAsync).Wait(TimeSpan.FromSeconds(8)); }
        catch (AggregateException) { /* best effort on exit */ }
        _probe.Dispose();
        _http.Dispose();
        GC.SuppressFinalize(this);
    }
}
