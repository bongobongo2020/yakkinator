using System.IO;

namespace IndexTTSStudio.Helpers;

public static class PathHelper
{
    public static string AppDataDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "IndexTTSStudio");

    public static string BackendDir => Path.Combine(AppDataDir, "backend");
    public static string CheckpointsDir => Path.Combine(BackendDir, "checkpoints");
    public static string VoicesDir => Path.Combine(AppDataDir, "voices");
    public static string OutputsDir => Path.Combine(AppDataDir, "outputs");
    public static string SettingsFile => Path.Combine(AppDataDir, "settings.json");
    public static string SetupStateFile => Path.Combine(AppDataDir, "setup-state.json");

    /// <summary>
    /// Path to the api_server.py bundled with the .NET app.
    /// At dev time, this is in the repo's python/ folder.
    /// </summary>
    public static string ApiServerScript
    {
        get
        {
            // First check if it's next to the exe (published)
            var exeDir = AppContext.BaseDirectory;
            var published = Path.Combine(exeDir, "python", "api_server.py");
            if (File.Exists(published)) return published;

            // Dev time: walk up to repo root
            var dir = new DirectoryInfo(exeDir);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "python", "api_server.py");
                if (File.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }

            throw new FileNotFoundException("api_server.py not found");
        }
    }

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(AppDataDir);
        Directory.CreateDirectory(VoicesDir);
        Directory.CreateDirectory(OutputsDir);
    }
}
