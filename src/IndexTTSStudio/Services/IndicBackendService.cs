using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using IndexTTSStudio.Helpers;
using IndexTTSStudio.Models;

namespace IndexTTSStudio.Services;

/// <summary>Per-request settings for Indic-Speak.</summary>
public record IndicRequest(string Text, string Speaker, string? Style,
    float Temperature = 0.6f, float TopP = 0.9f, int TopK = 50, int? Seed = null);

/// <summary>
/// Runs python/indic_server.py from Indic-Speak's own venv. The model takes a while to load, so
/// <see cref="WarmUpAsync"/> starts it as soon as the engine is picked, and a generation that
/// arrives while it is still loading waits for that same start instead of launching another.
/// </summary>
public class IndicBackendService : IDisposable
{
    private const int MaxLogChars = 2_000_000;

    private readonly int _port;
    private readonly SettingsService _settingsService;
    private readonly HttpClient _probe = new() { Timeout = TimeSpan.FromSeconds(5) };
    // Long texts are generated sentence by sentence and can take many minutes
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(30) };
    private readonly SemaphoreSlim _startGate = new(1, 1);
    private readonly StringBuilder _fullLog = new();
    private readonly object _logLock = new();

    private Process? _process;
    private CancellationTokenSource? _startCts;

    public event Action<string>? OnLog;
    /// <summary>Raised (on a worker thread) when the server becomes ready or stops.</summary>
    public event Action? StateChanged;

    public bool IsRunning => _process is { HasExited: false };
    public bool IsReady { get; private set; }
    public string FullLog { get { lock (_logLock) return _fullLog.ToString(); } }

    private string BaseUrl => $"http://127.0.0.1:{_port}";

    public IndicBackendService(SettingsService settingsService)
    {
        _settingsService = settingsService;
        _port = settingsService.Settings.IndicApiPort;
    }

    public static bool IsInstalled =>
        File.Exists(PathHelper.IndicPython) &&
        File.Exists(Path.Combine(PathHelper.IndicModelDir, "inference.py")) &&
        File.Exists(Path.Combine(PathHelper.IndicModelDir, "model.safetensors"));

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

    /// <summary>Starts loading the model in the background; errors surface on the next generation.</summary>
    public async Task WarmUpAsync()
    {
        try { await EnsureStartedAsync(null, CancellationToken.None).ConfigureAwait(false); }
        catch { /* reported when the user generates */ }
    }

    public async Task<TTSResponse> GenerateAsync(IndicRequest request, Action<string>? onStatus = null,
        CancellationToken ct = default)
    {
        await EnsureStartedAsync(onStatus, ct).ConfigureAwait(false);

        onStatus?.Invoke("Generating speech...");
        using var response = await _http.PostAsJsonAsync($"{BaseUrl}/api/tts", new
        {
            text = request.Text,
            speaker = request.Speaker,
            style = string.IsNullOrWhiteSpace(request.Style) ? null : request.Style.Trim(),
            temperature = request.Temperature,
            top_p = request.TopP,
            top_k = request.TopK,
            seed = request.Seed,
        }, ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return new TTSResponse { Success = false, ErrorMessage = ExtractDetail(error) };
        }

        var jobId = response.Headers.TryGetValues("X-Job-Id", out var ids) ? ids.First() : OutputHelper.NewJobId();
        var audioData = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
        var outputPath = await OutputHelper.SaveAsync(audioData, jobId,
            _settingsService.Settings.OutputDirectory, ct).ConfigureAwait(false);
        return new TTSResponse { Success = true, AudioFilePath = outputPath, JobId = jobId };
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
        return body;
    }

    private async Task EnsureStartedAsync(Action<string>? onStatus, CancellationToken ct)
    {
        if (IsReady && IsRunning) return;

        await _startGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (IsReady && IsRunning) return;
            if (!IsInstalled)
                throw new InvalidOperationException("Indic-Speak isn't installed. Install it from the Setup page.");

            onStatus?.Invoke("Loading Indic-Speak model (first load takes a minute)...");
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

        var script = PathHelper.IndicServerScript;
        var psi = new ProcessStartInfo
        {
            FileName = PathHelper.IndicPython,
            WorkingDirectory = PathHelper.IndicDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in new[] { script, "--port", _port.ToString(), "--model-dir", PathHelper.IndicModelDir })
            psi.ArgumentList.Add(a);
        psi.Environment["PYTHONUTF8"] = "1";
        // Not HF_HUB_OFFLINE: inference.py may fetch the public SNAC quantizer by repo id.
        // Setup pre-caches it, so this only touches the network if that cache was cleared.

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
                    $"Indic-Speak exited while loading (exit code: {process.ExitCode}). " +
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
            $"Indic-Speak failed to load within {maxWait.TotalMinutes:F0} minutes. Full log: {SaveErrorLog()}");
    }

    /// <summary>The server prints "[INDIC] FATAL: ..." when the model fails to load; show that line.</summary>
    private string LastErrorLine()
    {
        var line = FullLog.Split('\n').LastOrDefault(l => l.Contains("FATAL") || l.Contains("Error"))?.Trim();
        if (string.IsNullOrEmpty(line)) return "";
        if (line.Length > 300) line = line[..300] + "...";
        return line + "\n\n";
    }

    public string SaveErrorLog()
    {
        var path = Path.Combine(PathHelper.AppDataDir, "indic_error.log");
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
