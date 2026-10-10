using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using IndexTTSStudio.Helpers;

namespace IndexTTSStudio.Services;

public class PythonBackendService : IDisposable
{
    private const int MaxLogChars = 2_000_000;

    private Process? _backendProcess;
    private readonly int _port;
    private readonly bool _useFp16;
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(5) };
    // stdout and stderr callbacks arrive on different threads
    private readonly StringBuilder _fullLog = new();
    private readonly object _logLock = new();

    public event Action<string>? OnLog;
    public bool IsRunning => _backendProcess is { HasExited: false };
    public string FullLog { get { lock (_logLock) return _fullLog.ToString(); } }

    private string BaseUrl => $"http://127.0.0.1:{_port}";

    public PythonBackendService(int port = 5299, bool useFp16 = true)
    {
        _port = port;
        _useFp16 = useFp16;
    }

    private void AppendLog(string prefix, string line)
    {
        lock (_logLock)
        {
            // Verbose inference logging can run for hours; keep only the recent tail
            if (_fullLog.Length > MaxLogChars)
                _fullLog.Remove(0, _fullLog.Length - MaxLogChars / 2);
            _fullLog.Append(prefix).AppendLine(line);
        }
        OnLog?.Invoke(line);
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        if (IsRunning) return;

        _backendProcess?.Dispose();
        _backendProcess = null;

        // A backend orphaned by a previous crash would hold the port; ask it to exit.
        await ShutdownStaleBackendAsync(ct).ConfigureAwait(false);

        var apiServerPath = PathHelper.ApiServerScript;

        // Copy api_server.py to backend directory so it can access the venv
        var targetScript = Path.Combine(PathHelper.BackendDir, "api_server.py");
        File.Copy(apiServerPath, targetScript, overwrite: true);

        // Install uv if it has gone missing (or was never on PATH) instead of failing to launch
        var uv = await ProcessHelper.EnsureUvAsync(s => OnLog?.Invoke(s), ct).ConfigureAwait(false);

        var fp16Flag = _useFp16 ? "--fp16" : "--no-fp16";
        _backendProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = uv,
                Arguments = $"run python api_server.py --port {_port} --model-dir ./checkpoints {fp16Flag}",
                WorkingDirectory = PathHelper.BackendDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
            EnableRaisingEvents = true,
        };

        _backendProcess.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null) AppendLog("[STDOUT] ", e.Data);
        };
        _backendProcess.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null) AppendLog("[STDERR] ", e.Data);
        };

        _backendProcess.Start();
        _backendProcess.BeginOutputReadLine();
        _backendProcess.BeginErrorReadLine();

        try
        {
            await WaitUntilReadyAsync(_backendProcess, ct).ConfigureAwait(false);
        }
        catch
        {
            // Don't leave a half-loaded backend holding the port and GPU memory
            try { _backendProcess.Kill(entireProcessTree: true); } catch { }
            throw;
        }
    }

    private async Task WaitUntilReadyAsync(Process process, CancellationToken ct)
    {
        OnLog?.Invoke("Waiting for backend to initialize...");
        var maxWait = TimeSpan.FromMinutes(5); // Model loading can take time
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < maxWait)
        {
            ct.ThrowIfCancellationRequested();
            // Bail early if the process already died (crash during model load)
            if (process.HasExited)
            {
                throw new InvalidOperationException(
                    $"Backend process exited unexpectedly (exit code: {process.ExitCode}). " +
                    $"Common causes: missing model files, missing dependencies, or corrupted installation. " +
                    $"Run Setup again to fix.");
            }

            if (await IsReadyAsync(ct).ConfigureAwait(false))
            {
                OnLog?.Invoke("Backend is ready!");
                return;
            }
            await Task.Delay(1000, ct).ConfigureAwait(false);
        }

        throw new TimeoutException(
            $"Backend failed to start within {maxWait.TotalMinutes:F1} minutes. " +
            $"The model may still be loading, or checkpoints may be missing at {PathHelper.CheckpointsDir}. " +
            $"Run Setup again if needed.");
    }

    private async Task<bool> IsReadyAsync(CancellationToken ct)
    {
        try
        {
            using var response = await _httpClient.GetAsync($"{BaseUrl}/api/health", ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return false;

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("status", out var status) &&
                   status.GetString() == "ready";
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            return false; // not up yet
        }
    }

    private async Task ShutdownStaleBackendAsync(CancellationToken ct)
    {
        try
        {
            using var health = await _httpClient.GetAsync($"{BaseUrl}/api/health", ct).ConfigureAwait(false);
            if (!health.IsSuccessStatusCode) return;
            OnLog?.Invoke("Stopping a backend left over from a previous session...");
            using var _ = await _httpClient.PostAsync($"{BaseUrl}/api/shutdown", null, ct).ConfigureAwait(false);
            await Task.Delay(1500, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException) { /* nothing listening: the normal case */ }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { /* timed out */ }
    }

    public async Task StopAsync()
    {
        var process = _backendProcess;
        if (process is not { HasExited: false }) return;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            using var _ = await _httpClient.PostAsync($"{BaseUrl}/api/shutdown", null, cts.Token).ConfigureAwait(false);
            using var exitCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await process.WaitForExitAsync(exitCts.Token).ConfigureAwait(false);
        }
        catch { /* fall through to kill */ }

        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException) { /* exited in the meantime */ }
    }

    public void SaveLogToFile(string path)
    {
        File.WriteAllText(path, FullLog);
    }

    public void Dispose()
    {
        // Run off the caller's (UI) synchronization context so blocking here can't deadlock
        try { Task.Run(StopAsync).Wait(TimeSpan.FromSeconds(8)); }
        catch (AggregateException) { /* best effort on exit */ }
        _backendProcess?.Dispose();
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }
}
