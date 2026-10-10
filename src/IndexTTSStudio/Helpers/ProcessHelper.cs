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

    /// <summary>
    /// Locates uv, including its default install locations. A freshly installed uv
    /// is not on this process's PATH until the app restarts.
    /// </summary>
    public static string? FindUv()
    {
        var found = FindExecutable("uv");
        if (found != null) return found;

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] candidates =
        [
            Path.Combine(home, ".local", "bin", "uv.exe"),
            Path.Combine(home, ".cargo", "bin", "uv.exe"),
        ];
        return candidates.FirstOrDefault(File.Exists);
    }

    /// <summary>uv executable to launch: the resolved path when known, otherwise rely on PATH.</summary>
    public static string Uv => FindUv() ?? "uv";
}
