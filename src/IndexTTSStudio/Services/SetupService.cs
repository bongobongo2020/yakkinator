using System.IO;
using System.Text.Json;
using IndexTTSStudio.Models;
using IndexTTSStudio.Helpers;

namespace IndexTTSStudio.Services;

public class SetupService
{
    public event Action<string>? OnStatusChanged;
    public event Action<double>? OnProgressChanged;

    private SetupState _state = new();
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

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
        if (File.Exists(PathHelper.SetupStateFile))
        {
            var json = File.ReadAllText(PathHelper.SetupStateFile);
            _state = JsonSerializer.Deserialize<SetupState>(json) ?? new SetupState();
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

    public async Task RunFullSetupAsync(CancellationToken ct = default)
    {
        PathHelper.EnsureDirectories();

        // Step 1: Check Git
        Progress(0.05);
        Status("Checking for Git...");
        var gitPath = ProcessHelper.FindExecutable("git");
        if (gitPath == null)
        {
            _state.ErrorMessage = "Git is not installed. Please install Git from https://git-scm.com and restart.";
            SaveState();
            throw new InvalidOperationException(_state.ErrorMessage);
        }
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
        var uvPath = ProcessHelper.FindExecutable("uv");
        if (uvPath == null)
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
                // Try standalone installer
                await ProcessHelper.RunAsync("powershell", "-Command \"irm https://astral.sh/uv/install.ps1 | iex\"",
                    ct: ct, onOutput: s => Status($"Installing uv: {s}"));
            }

            uvPath = ProcessHelper.FindExecutable("uv");
            if (uvPath == null)
            {
                _state.ErrorMessage = "Failed to install uv. Please install it manually: pip install uv";
                SaveState();
                throw new InvalidOperationException(_state.ErrorMessage);
            }
        }
        _state.UvInstalled = true;
        SaveState();

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

            // Skip LFS smudge: the repo's LFS budget is often exceeded for example WAV files.
            // Model weights are downloaded separately from HuggingFace.
            var (exitCode, _, error) = await ProcessHelper.RunAsync(
                "git", $"-c filter.lfs.smudge= -c filter.lfs.required=false clone https://github.com/index-tts/index-tts.git \"{PathHelper.BackendDir}\"",
                ct: ct, onOutput: s => Status($"Cloning: {s}"));

            if (exitCode != 0)
            {
                _state.ErrorMessage = $"Failed to clone repository: {error}";
                SaveState();
                throw new InvalidOperationException(_state.ErrorMessage);
            }

            _state.RepoCloned = true;
            SaveState();
        }
        Progress(0.40);

        // Step 5: Install Python dependencies with uv
        if (!_state.DependenciesInstalled)
        {
            Status("Installing Python dependencies (this will take several minutes)...");
            var (exitCode, _, error) = await ProcessHelper.RunAsync(
                "uv", "sync --extra webui",
                workingDirectory: PathHelper.BackendDir, ct: ct,
                onOutput: s => Status($"Dependencies: {s}"));

            if (exitCode != 0)
            {
                _state.ErrorMessage = $"Failed to install dependencies: {error}";
                SaveState();
                throw new InvalidOperationException(_state.ErrorMessage);
            }
            _state.DependenciesInstalled = true;
            SaveState();
        }
        Progress(0.60);

        // Step 6: Install FastAPI + Uvicorn into the uv environment
        Status("Installing API server dependencies...");
        await ProcessHelper.RunAsync("uv", "pip install fastapi uvicorn python-multipart",
            workingDirectory: PathHelper.BackendDir, ct: ct);

        // Step 7: Download models
        if (!_state.ModelsDownloaded)
        {
            Status("Downloading IndexTTS-2 models from HuggingFace (2-4 GB)...");

            // Use huggingface-hub to download models
            var downloadScript = @"
from huggingface_hub import snapshot_download
import sys
try:
    snapshot_download(
        repo_id='IndexTeam/IndexTTS-2',
        local_dir='checkpoints',
        resume_download=True
    )
    print('MODEL_DOWNLOAD_SUCCESS')
except Exception as e:
    print(f'MODEL_DOWNLOAD_FAILED: {e}', file=sys.stderr)
    sys.exit(1)
";
            var scriptPath = Path.Combine(PathHelper.BackendDir, "_download_models.py");
            File.WriteAllText(scriptPath, downloadScript);

            var (exitCode, output, error) = await ProcessHelper.RunAsync(
                "uv", $"run python \"{scriptPath}\"",
                workingDirectory: PathHelper.BackendDir, ct: ct,
                onOutput: s => Status($"Models: {s}"));

            // Cleanup temp script
            if (File.Exists(scriptPath)) File.Delete(scriptPath);

            if (exitCode != 0 || !output.Contains("MODEL_DOWNLOAD_SUCCESS"))
            {
                _state.ErrorMessage = $"Failed to download models: {error}";
                SaveState();
                throw new InvalidOperationException(_state.ErrorMessage);
            }

            _state.ModelsDownloaded = true;
            SaveState();
        }
        Progress(1.0);
        _state.LastSetupDate = DateTime.UtcNow;
        _state.ErrorMessage = null;
        SaveState();
        Status("Setup complete! Ready to generate speech.");
    }
}
