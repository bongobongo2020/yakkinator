using System.Diagnostics;
using System.IO;

namespace IndexTTSStudio.Helpers;

public static class ProcessHelper
{
    public static async Task<(int exitCode, string output, string error)> RunAsync(
        string fileName, string arguments, string? workingDirectory = null,
        CancellationToken ct = default, Action<string>? onOutput = null)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        var output = new System.Text.StringBuilder();
        var error = new System.Text.StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                output.AppendLine(e.Data);
                onOutput?.Invoke(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                error.AppendLine(e.Data);
                onOutput?.Invoke(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            // Don't leave a download/install running in the background after cancel
            try { process.Kill(entireProcessTree: true); } catch { }
            throw;
        }
        return (process.ExitCode, output.ToString(), error.ToString());
    }

    public static string? FindExecutable(string name)
    {
        // Check PATH
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        var extensions = new[] { "", ".exe", ".cmd", ".bat" };

        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var ext in extensions)
            {
                var fullPath = Path.Combine(dir.Trim('"'), name + ext);
                if (File.Exists(fullPath)) return fullPath;
            }
        }
        return null;
    }

    private static string? _uvPath;

    /// <summary>
    /// Locates uv, including the places installers put it that are often not on PATH
    /// (a freshly installed uv isn't on this process's PATH until the app restarts).
    /// </summary>
    public static string? FindUv()
    {
        if (_uvPath != null && File.Exists(_uvPath)) return _uvPath;
        return _uvPath = FindExecutable("uv") ?? UvCandidates().FirstOrDefault(File.Exists);
    }

    private static IEnumerable<string> UvCandidates()
    {
        // Resolve like the installers do: environment variables first
        static string Dir(string envVar, Environment.SpecialFolder fallback) =>
            Environment.GetEnvironmentVariable(envVar) is { Length: > 0 } v ? v : Environment.GetFolderPath(fallback);
        var home = Dir("USERPROFILE", Environment.SpecialFolder.UserProfile);
        var appData = Dir("APPDATA", Environment.SpecialFolder.ApplicationData);
        var localAppData = Dir("LOCALAPPDATA", Environment.SpecialFolder.LocalApplicationData);

        // Standalone installer (honors UV_INSTALL_DIR / XDG_BIN_HOME), cargo, and winget
        foreach (var custom in new[] { "UV_INSTALL_DIR", "XDG_BIN_HOME" }.Select(Environment.GetEnvironmentVariable))
            if (!string.IsNullOrEmpty(custom))
            {
                yield return Path.Combine(custom, "uv.exe");
                yield return Path.Combine(custom, "bin", "uv.exe");
            }
        yield return Path.Combine(home, ".local", "bin", "uv.exe");
        yield return Path.Combine(home, ".cargo", "bin", "uv.exe");
        yield return Path.Combine(localAppData, "Microsoft", "WinGet", "Links", "uv.exe");

        // `pip install uv` next to a Python on PATH (e.g. C:\Python312\Scripts)
        foreach (var python in new[] { "python", "python3" }.Select(FindExecutable).OfType<string>())
        {
            var dir = Path.GetDirectoryName(python)!;
            yield return Path.Combine(dir, "Scripts", "uv.exe");
            yield return Path.Combine(dir, "uv.exe");
        }

        // `pip install --user uv` (also pip's fallback when Python is in Program Files):
        // %APPDATA%\Python\Python313\Scripts, plus per-user Python installs
        foreach (var root in new[] { Path.Combine(appData, "Python"), Path.Combine(localAppData, "Programs", "Python") })
        {
            if (!Directory.Exists(root)) continue;
            foreach (var versionDir in Directory.EnumerateDirectories(root).OrderDescending())
                yield return Path.Combine(versionDir, "Scripts", "uv.exe");
        }
    }

    /// <summary>uv executable to launch: the resolved path when known, otherwise rely on PATH.</summary>
    public static string Uv => FindUv() ?? "uv";

    /// <summary>Returns the uv path, installing uv first if it can't be found.</summary>
    public static async Task<string> EnsureUvAsync(Action<string>? onOutput = null, CancellationToken ct = default)
    {
        var uv = FindUv();
        if (uv != null) return uv;

        // Official standalone installer: needs no Python, installs to %USERPROFILE%\.local\bin
        onOutput?.Invoke("uv not found, installing it...");
        try
        {
            await RunAsync("powershell",
                "-NoProfile -ExecutionPolicy ByPass -Command \"irm https://astral.sh/uv/install.ps1 | iex\"",
                ct: ct, onOutput: onOutput);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            onOutput?.Invoke($"Standalone uv installer failed: {ex.Message}");
        }

        uv = FindUv();
        if (uv != null) return uv;

        // Fall back to pip; --user avoids needing admin rights for a Program Files Python
        var python = FindExecutable("python") ?? FindExecutable("py");
        if (python != null)
        {
            onOutput?.Invoke("Trying pip install --user uv...");
            await RunAsync(python, "-m pip install --user -U uv", ct: ct, onOutput: onOutput);
        }

        return FindUv() ?? throw new InvalidOperationException(
            "Could not install uv automatically. Install it from https://docs.astral.sh/uv/ and restart the app.");
    }
}
