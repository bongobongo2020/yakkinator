using System.IO;
using System.Text.Json;
using IndexTTSStudio.Models;
using IndexTTSStudio.Helpers;

namespace IndexTTSStudio.Services;

public class VoiceLibraryService
{
    // kept for VoiceLibraryViewModel.AddVoice fallback
    public string? LastUsedVoicePath { get; set; }

    public List<VoiceProfile> GetVoices()
    {
        var dir = PathHelper.VoicesDir;
        if (!Directory.Exists(dir)) return [];

        return Directory.GetFiles(dir, "*.wav")
            .Select(f => new FileInfo(f))
            .Select(fi =>
            {
                var profile = new VoiceProfile
                {
                    Name = Path.GetFileNameWithoutExtension(fi.Name),
                    FilePath = fi.FullName,
                    FileSize = fi.Length,
                    Modified = fi.LastWriteTime,
                };
                var jsonPath = Path.ChangeExtension(fi.FullName, ".json");
                if (File.Exists(jsonPath))
                {
                    try
                    {
                        var saved = JsonSerializer.Deserialize<VoiceProfileSettings>(File.ReadAllText(jsonPath));
                        if (saved != null)
                        {
                            profile.EmotionMode = saved.EmotionMode;
                            profile.EmotionAlpha = saved.EmotionAlpha;
                            profile.EmotionVector = saved.EmotionVector;
                        }
                    }
                    catch { }
                }
                return profile;
            })
            .OrderByDescending(v => v.Modified)
            .ToList();
    }

    public string SaveVoice(string name, string sourceFilePath, string emotionMode = "none", float emotionAlpha = 0.6f, float[]? emotionVector = null)
    {
        Directory.CreateDirectory(PathHelper.VoicesDir);
        var safeName = new string(name.Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == ' ').ToArray()).Trim();
        var destPath = Path.Combine(PathHelper.VoicesDir, $"{safeName}.wav");
        File.Copy(sourceFilePath, destPath, overwrite: true);

        var jsonPath = Path.ChangeExtension(destPath, ".json");
        var settings = new VoiceProfileSettings
        {
            EmotionMode = emotionMode,
            EmotionAlpha = emotionAlpha,
            EmotionVector = emotionVector ?? new float[8],
        };
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(settings));
        return destPath;
    }

    public void DeleteVoice(string name)
    {
        var path = Path.Combine(PathHelper.VoicesDir, $"{name}.wav");
        if (File.Exists(path)) File.Delete(path);
        var jsonPath = Path.ChangeExtension(path, ".json");
        if (File.Exists(jsonPath)) File.Delete(jsonPath);
    }

    private class VoiceProfileSettings
    {
        public string EmotionMode { get; set; } = "none";
        public float EmotionAlpha { get; set; } = 0.6f;
        public float[] EmotionVector { get; set; } = new float[8];
    }
}
