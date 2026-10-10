using System.IO;
using System.Text.Json;
using IndexTTSStudio.Models;
using IndexTTSStudio.Helpers;

namespace IndexTTSStudio.Services;

public class SetupService
{
    public event Action<string>? OnStatusChanged;
    public event Action<double>? OnProgressChanged;
    /// <summary>Raised when setup or a model re-download finishes successfully.</summary>
    public event Action? OnSetupCompleted;

    private SetupState _state = new();
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };
    private static readonly string[] CriticalModelFiles = ["gpt.pth", "config.yaml", "wav2vec2bert_stats.pt"];

    public SetupState State => _state;

    public SetupService()
    {
        LoadState();
    }

    public bool IsSetupComplete =>
        _state.GitInstalled && _state.UvInstalled && _state.RepoCloned &&
        _state.DependenciesInstalled && _state.ModelsDownloaded;

    private void LoadState()
    {
        if (!File.Exists(PathHelper.SetupStateFile)) return;
        try
        {
            var json = File.ReadAllText(PathHelper.SetupStateFile);
            _state = JsonSerializer.Deserialize<SetupState>(json) ?? new SetupState();
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            // A corrupt state file just means setup re-checks each step
            _state = new SetupState();
        }
    }

    private void SaveState()
    {
        PathHelper.EnsureDirectories();
        File.WriteAllText(PathHelper.SetupStateFile, JsonSerializer.Serialize(_state, JsonOpts));
    }

    private void Status(string msg)
    {
        OnStatusChanged?.Invoke(msg);
    }

    private void Progress(double pct)
    {
        OnProgressChanged?.Invoke(pct);
    }

    private InvalidOperationException Fail(string message)
    {
        _state.ErrorMessage = message;
        SaveState();
        return new InvalidOperationException(message);
    }

    /// <summary>Drops warning/traceback noise from hf CLI stderr, leaving actual error lines.</summary>
    private static string FilterDownloadErrors(string stderr) =>
        string.Join("\n", stderr.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Where(line => !line.Contains("FutureWarning") &&
                           !line.Contains("DeprecationWarning") &&
                           !line.Contains("Xet Storage") &&
                           !line.Contains("PyDevTerminal") &&
                           !line.Contains("site-packages") &&
                           !line.TrimStart().StartsWith("at ") &&
                           !string.IsNullOrWhiteSpace(line)));

    public async Task DownloadModelsAsync(bool force = false, CancellationToken ct = default)
    {
        await DownloadModelsCoreAsync(force, ct);
        OnSetupCompleted?.Invoke();
    }

    private async Task DownloadModelsCoreAsync(bool force, CancellationToken ct)
    {
        if (!force && _state.ModelsDownloaded)
        {
            Status("Models already downloaded. Use force=true to re-download.");
            return;
        }

        var uv = ProcessHelper.Uv;
        Status("Downloading IndexTTS-2 models from HuggingFace (this may take 10-30 minutes)...");
        Status("Ensuring huggingface-hub CLI with Xet support is installed...");

        // Ensure hf CLI with hf_xet is available (needed for Xet Storage files)
        await ProcessHelper.RunAsync(uv, "tool install \"huggingface-hub[hf_xet]\" --force",
            workingDirectory: PathHelper.BackendDir, ct: ct,
            onOutput: s => Status($"CLI: {s}"));

        Status("Downloading models using hf CLI (supports Xet Storage)...");
        var (exitCode, _, error) = await ProcessHelper.RunAsync(
            uv, "tool run hf download IndexTeam/IndexTTS-2 --local-dir checkpoints",
            workingDirectory: PathHelper.BackendDir, ct: ct,
            onOutput: s => Status($"Download: {s}"));

        if (exitCode != 0)
        {
            var actualErrors = FilterDownloadErrors(error);
            if (!string.IsNullOrEmpty(actualErrors))
                throw Fail($"Download failed (exit {exitCode}):\n{actualErrors}");
        }

        // Verify critical files — if any are missing, try a targeted per-file download
        // using hf_hub_download inside the project venv (where hf_xet is installed).
        Status("Verifying downloaded files...");
        var missing = CriticalModelFiles
            .Where(f => !File.Exists(Path.Combine(PathHelper.CheckpointsDir, f)))
            .ToList();

        if (missing.Count > 0)
        {
            Status($"Some files are missing ({string.Join(", ", missing)}), retrying with direct download...");

            // Install hf_xet into the project venv so hf_hub_download can use it
            await ProcessHelper.RunAsync(uv, "pip install \"huggingface_hub[hf_xet]\"",
                workingDirectory: PathHelper.BackendDir, ct: ct,
                onOutput: s => Status($"  {s}"));

            foreach (var file in missing)
            {
                Status($"Downloading {file}...");
                var script = $@"
import sys, os
os.makedirs('checkpoints', exist_ok=True)
from huggingface_hub import hf_hub_download
path = hf_hub_download(
    repo_id='IndexTeam/IndexTTS-2',
    filename='{file}',
    local_dir='checkpoints',
)
print(f'Downloaded: {{path}}')
";
                var scriptPath = Path.Combine(PathHelper.BackendDir, "_dl_file.py");
                File.WriteAllText(scriptPath, script);
                try
                {
                    await ProcessHelper.RunAsync(uv, $"run python \"{scriptPath}\"",
                        workingDirectory: PathHelper.BackendDir, ct: ct,
                        onOutput: s => Status($"  {s}"));
                }
                finally
                {
                    if (File.Exists(scriptPath)) File.Delete(scriptPath);
                }

                if (!File.Exists(Path.Combine(PathHelper.CheckpointsDir, file)))
                    throw Fail($"Failed to download {file} even with direct method. " +
                               "Check your internet connection and try again.");
            }
        }

        _state.ModelsDownloaded = true;
        _state.LastSetupDate = DateTime.UtcNow;
        _state.ErrorMessage = null;
        SaveState();
        Status("Model download complete!");
    }

    public async Task RunFullSetupAsync(CancellationToken ct = default)
    {
        PathHelper.EnsureDirectories();

        // Step 1: Check Git
        Progress(0.05);
        Status("Checking for Git...");
        if (ProcessHelper.FindExecutable("git") == null)
            throw Fail("Git is not installed. Please install Git from https://git-scm.com and restart.");
        _state.GitInstalled = true;
        SaveState();

        // Step 2: Git LFS
        Progress(0.10);
        Status("Setting up Git LFS...");
        await ProcessHelper.RunAsync("git", "lfs install", ct: ct);
        _state.GitLfsInstalled = true;
        SaveState();

        // Step 3: Install/Check uv
        Progress(0.15);
        Status("Checking for uv package manager...");
        if (ProcessHelper.FindUv() == null)
        {
            Status("Installing uv package manager...");
            // Install uv via pip or standalone installer
            var pipPath = ProcessHelper.FindExecutable("pip") ?? ProcessHelper.FindExecutable("pip3");
            if (pipPath != null)
            {
                await ProcessHelper.RunAsync(pipPath, "install -U uv", ct: ct,
                    onOutput: s => Status($"Installing uv: {s}"));
            }
            else
            {
                // Standalone installer; it installs to %USERPROFILE%\.local\bin, which FindUv checks
                await ProcessHelper.RunAsync("powershell",
                    "-NoProfile -ExecutionPolicy ByPass -Command \"irm https://astral.sh/uv/install.ps1 | iex\"",
                    ct: ct, onOutput: s => Status($"Installing uv: {s}"));
            }

            if (ProcessHelper.FindUv() == null)
                throw Fail("Failed to install uv. Please install it manually: pip install uv");
        }
        _state.UvInstalled = true;
        SaveState();
        var uv = ProcessHelper.Uv;

        // Step 4: Clone the IndexTTS repo
        Progress(0.20);
        if (!_state.RepoCloned || !Directory.Exists(PathHelper.BackendDir))
        {
            Status("Cloning IndexTTS repository (this may take a moment)...");
            if (Directory.Exists(PathHelper.BackendDir))
            {
                // Git pack files are read-only on Windows; strip attributes before deleting
                foreach (var f in Directory.GetFiles(PathHelper.BackendDir, "*", SearchOption.AllDirectories))
                    File.SetAttributes(f, FileAttributes.Normal);
                Directory.Delete(PathHelper.BackendDir, true);
            }

            // A fresh clone has no venv or checkpoints, whatever the saved state says
            _state.DependenciesInstalled = false;
            _state.ModelsDownloaded = false;

            // Skip LFS smudge: the repo's LFS budget is often exceeded for example WAV files.
            // Model weights are downloaded separately from HuggingFace.
            var (exitCode, _, error) = await ProcessHelper.RunAsync(
                "git", $"-c filter.lfs.smudge= -c filter.lfs.required=false clone https://github.com/index-tts/index-tts.git \"{PathHelper.BackendDir}\"",
                ct: ct, onOutput: s => Status($"Cloning: {s}"));

            if (exitCode != 0)
                throw Fail($"Failed to clone repository: {error}");

            _state.RepoCloned = true;
            SaveState();
        }
        Progress(0.40);

        // Step 5: Install Python dependencies with uv
        if (!_state.DependenciesInstalled)
        {
            Status("Installing Python dependencies (this will take several minutes)...");
            var (exitCode, _, error) = await ProcessHelper.RunAsync(
                uv, "sync --extra webui",
                workingDirectory: PathHelper.BackendDir, ct: ct,
                onOutput: s => Status($"Dependencies: {s}"));

            if (exitCode != 0)
                throw Fail($"Failed to install dependencies: {error}");
            _state.DependenciesInstalled = true;
            SaveState();
        }
        Progress(0.60);

        // Step 6: Install FastAPI + Uvicorn into the uv environment
        Status("Installing API server dependencies...");
        var (apiExit, _, apiError) = await ProcessHelper.RunAsync(uv, "pip install fastapi uvicorn python-multipart",
            workingDirectory: PathHelper.BackendDir, ct: ct);
        if (apiExit != 0)
            throw Fail($"Failed to install API server dependencies: {apiError}");
        Progress(0.65);

        // Step 7: Download models
        await DownloadModelsCoreAsync(force: false, ct);

        Progress(1.0);
        _state.LastSetupDate = DateTime.UtcNow;
        _state.ErrorMessage = null;
        SaveState();
        Status("Setup complete! Ready to generate speech.");
        OnSetupCompleted?.Invoke();
    }
}
