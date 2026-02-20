using System.IO;
using System.Text.Json;
using IndexTTSStudio.Models;
using IndexTTSStudio.Helpers;

namespace IndexTTSStudio.Services;

public class SettingsService
{
    private AppSettings? _settings;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public AppSettings Settings => _settings ??= Load();

    private AppSettings Load()
    {
        if (File.Exists(PathHelper.SettingsFile))
        {
            var json = File.ReadAllText(PathHelper.SettingsFile);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        return new AppSettings();
    }

    public void Save()
    {
        PathHelper.EnsureDirectories();
        var json = JsonSerializer.Serialize(Settings, JsonOpts);
        File.WriteAllText(PathHelper.SettingsFile, json);
    }
}
