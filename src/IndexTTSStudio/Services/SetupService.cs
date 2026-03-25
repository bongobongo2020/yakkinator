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

    public async Task DownloadModelsAsync(bool force = false, CancellationToken ct = default)
    {
        if (!force && _state.ModelsDownloaded)
        {
            Status("Models already downloaded. Use force=true to re-download.");
            return;
        }

        Status("Downloading IndexTTS-2 models from HuggingFace (2-4 GB)...");
        Status("Ensuring huggingface-hub CLI with Xet support is installed...");

        // Ensure hf CLI with hf_xet is available (needed for Xet Storage files)
        await ProcessHelper.RunAsync("uv", "tool install \"huggingface-hub[hf_xet]\" --force",
            workingDirectory: PathHelper.BackendDir, ct: ct,
            onOutput: s => Status($"CLI: {s}"));

        Status("Downloading models using hf CLI (supports Xet Storage)...");
        var (exitCode, output, error) = await ProcessHelper.RunAsync(
            "uv", "tool run hf download IndexTeam/IndexTTS-2 --local-dir checkpoints",
            workingDirectory: PathHelper.BackendDir, ct: ct,
            onOutput: s => Status($"Download: {s}"));

        if (exitCode != 0)
        {
            var errorLines = error.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            var actualErrors = string.Join("\n", errorLines.Where(line =>
                !line.Contains("FutureWarning") && !line.Contains("DeprecationWarning") &&
                !line.Contains("Xet Storage") && !string.IsNullOrWhiteSpace(line)));

            if (!string.IsNullOrEmpty(actualErrors))
            {
                _state.ErrorMessage = $"Download failed (exit {exitCode}):\n{actualErrors}";
                SaveState();
                throw new InvalidOperationException(_state.ErrorMessage);
            }
        }

        // Verify critical files — if any are missing, try a targeted per-file download
        // using hf_hub_download inside the project venv (where hf_xet is installed).
        Status("Verifying downloaded files...");
        var criticalFiles = new[] { "gpt.pth", "config.yaml", "wav2vec2bert_stats.pt" };
        var missing = criticalFiles
            .Where(f => !File.Exists(Path.Combine(PathHelper.BackendDir, "checkpoints", f)))
            .ToList();

        if (missing.Count > 0)
        {
            Status($"Some files are missing ({string.Join(", ", missing)}), retrying with direct download...");

            // Install hf_xet into the project venv so hf_hub_download can use it
            await ProcessHelper.RunAsync("uv", "pip install \"huggingface_hub[hf_xet]\"",
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
    local_dir_use_symlinks=False,
)
print(f'Downloaded: {{path}}')
";
                var scriptPath = Path.Combine(PathHelper.BackendDir, "_dl_file.py");
                File.WriteAllText(scriptPath, script);
                var (rc, _, _) = await ProcessHelper.RunAsync("uv", $"run python \"{scriptPath}\"",
                    workingDirectory: PathHelper.BackendDir, ct: ct,
                    onOutput: s => Status($"  {s}"));
                if (File.Exists(scriptPath)) File.Delete(scriptPath);

                if (!File.Exists(Path.Combine(PathHelper.BackendDir, "checkpoints", file)))
                {
                    _state.ErrorMessage = $"Failed to download {file} even with direct method. " +
                                          "Check your internet connection and try again.";
                    SaveState();
                    throw new InvalidOperationException(_state.ErrorMessage);
                }
            }
        }

        _state.ModelsDownloaded = true;
        _state.LastSetupDate = DateTime.UtcNow;
        _state.ErrorMessage = null;
        SaveState();
        Status("Model download complete! Restart the app to start the backend.");
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

        // Step 7: Download models using the official IndexTTS method
        if (!_state.ModelsDownloaded)
        {
            Status("Downloading IndexTTS-2 models (this may take 10-30 minutes)...");

            // Use the official download method recommended by IndexTTS team
            Status("Installing huggingface-cli tool...");
            await ProcessHelper.RunAsync("uv", "tool install \"huggingface-hub[hf_xet]\"",
                workingDirectory: PathHelper.BackendDir, ct: ct);

            Status("Downloading models using hf download (official method)...");
            var (exitCode, output, error) = await ProcessHelper.RunAsync(
                "uv", "tool run hf download IndexTeam/IndexTTS-2 --local-dir checkpoints",
                workingDirectory: PathHelper.BackendDir, ct: ct,
                onOutput: s => Status($"Download: {s}"));

            // Filter out warnings from error message
            var errorLines = error.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            var actualErrors = string.Join("\n",
                errorLines
                    .Where(line => !line.Contains("FutureWarning") &&
                                  !line.Contains("DeprecationWarning") &&
                                  !line.Contains("Xet Storage") &&
                                  !line.Contains("PyDevTerminal") &&
                                  !line.Contains("site-packages") &&
                                  !line.TrimStart().StartsWith("at ") &&
                                  !string.IsNullOrWhiteSpace(line)));

            if (exitCode != 0 && !string.IsNullOrEmpty(actualErrors))
            {
                _state.ErrorMessage = $"Failed to download models (exit code: {exitCode}):\n{actualErrors}";
                SaveState();
                throw new InvalidOperationException(_state.ErrorMessage);
            }

            // Verify download succeeded
            Status("Verifying downloaded files...");
            var verificationScript = @"
import os
import sys

critical_files = ['gpt.pth', 'config.yaml', 'wav2vec2bert_stats.pt']
missing = []

for f in critical_files:
    path = os.path.join('checkpoints', f)
    if not os.path.exists(path):
        # Check subdirectories
        found = False
        for root, dirs, files in os.walk('checkpoints'):
            if f in files:
                print(f'Found {f} in subdirectory: {root}')
                import shutil
                shutil.copy(os.path.join(root, f), path)
                found = True
                break
        if not found:
            missing.append(f)

if missing:
    print(f'MODEL_DOWNLOAD_FAILED: Missing critical files: {missing}', file=sys.stderr)
    print('Note: Some files may be downloaded automatically on first run.', file=sys.stderr)
    sys.exit(1)

# List all files
print('Downloaded files:')
for item in os.listdir('checkpoints'):
    item_path = os.path.join('checkpoints', item)
    if os.path.isfile(item_path):
        size_mb = os.path.getsize(item_path) / (1024*1024)
        print(f'  {item} ({size_mb:.1f} MB)')

print('MODEL_DOWNLOAD_SUCCESS')
";

            var scriptPath = Path.Combine(PathHelper.BackendDir, "_verify_download.py");
            File.WriteAllText(scriptPath, verificationScript);

            var (verifyExitCode, verifyOutput, verifyError) = await ProcessHelper.RunAsync(
                "uv", $"run python \"{scriptPath}\"",
                workingDirectory: PathHelper.BackendDir, ct: ct);

            if (File.Exists(scriptPath)) File.Delete(scriptPath);

            if (verifyExitCode != 0 || !verifyOutput.Contains("MODEL_DOWNLOAD_SUCCESS"))
            {
                _state.ErrorMessage = $"Model verification failed:\n{verifyError}\n\nOutput:\n{verifyOutput}";
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
