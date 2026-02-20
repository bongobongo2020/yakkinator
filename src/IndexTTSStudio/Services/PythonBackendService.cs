using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using IndexTTSStudio.Helpers;

namespace IndexTTSStudio.Services;

public class PythonBackendService : IDisposable
{
    private Process? _backendProcess;
    private readonly int _port;
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(5) };

    public event Action<string>? OnLog;
    public bool IsRunning => _backendProcess is { HasExited: false };

    public PythonBackendService(int port = 5299)
    {
        _port = port;
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        if (IsRunning) return;

        var apiServerPath = PathHelper.ApiServerScript;

        // Copy api_server.py to backend directory so it can access the venv
        var targetScript = Path.Combine(PathHelper.BackendDir, "api_server.py");
        File.Copy(apiServerPath, targetScript, overwrite: true);

        _backendProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "uv",
                Arguments = $"run python api_server.py --port {_port} --model-dir ./checkpoints --fp16",
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
            if (e.Data != null) OnLog?.Invoke(e.Data);
        };
        _backendProcess.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null) OnLog?.Invoke(e.Data);
        };

        _backendProcess.Start();
        _backendProcess.BeginOutputReadLine();
        _backendProcess.BeginErrorReadLine();

        // Wait for the API to become healthy
        OnLog?.Invoke("Waiting for backend to initialize...");
        var maxWait = TimeSpan.FromMinutes(5); // Model loading can take time
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < maxWait)
        {
            ct.ThrowIfCancellationRequested();
            // Bail early if the process already died (crash during model load)
            if (_backendProcess.HasExited)
                throw new InvalidOperationException($"Backend process exited unexpectedly (code {_backendProcess.ExitCode}). Check that models are installed correctly.");

            try
            {
                var response = await _httpClient.GetAsync($"http://127.0.0.1:{_port}/api/health", ct);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(ct);
                    var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("status", out var status) &&
                        status.GetString() == "ready")
                    {
                        OnLog?.Invoke("Backend is ready!");
                        return;
                    }
                    OnLog?.Invoke("Backend is loading model...");
                }
            }
            catch { /* not ready yet */ }
            await Task.Delay(2000, ct);
        }

        throw new TimeoutException("Backend failed to start within the timeout period.");
    }

    public async Task StopAsync()
    {
        if (!IsRunning) return;
        try
        {
            await _httpClient.PostAsync($"http://127.0.0.1:{_port}/api/shutdown", null);
            await Task.Delay(1000);
        }
        catch { /* ignore */ }

        if (_backendProcess is { HasExited: false })
        {
            _backendProcess.Kill(entireProcessTree: true);
        }
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
        _backendProcess?.Dispose();
        _httpClient.Dispose();
    }
}
