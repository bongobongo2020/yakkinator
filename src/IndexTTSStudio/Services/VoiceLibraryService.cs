using System.IO;
using System.Text.Json;
using IndexTTSStudio.Models;
using IndexTTSStudio.Helpers;

namespace IndexTTSStudio.Services;

public class VoiceLibraryService
{
    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase) { ".wav", ".mp3", ".flac" };

    /// <summary>Raised after a voice is saved or deleted.</summary>
    public event Action? VoicesChanged;

    public List<VoiceProfile> GetVoices()
    {
        var dir = PathHelper.VoicesDir;
        if (!Directory.Exists(dir)) return [];

        return new DirectoryInfo(dir).EnumerateFiles()
            .Where(fi => AudioExtensions.Contains(fi.Extension))
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
        if (safeName.Length == 0) safeName = "voice";

        // Keep the real extension: NAudio picks its decoder by extension, so an MP3 saved as .wav won't play
        var ext = Path.GetExtension(sourceFilePath).ToLowerInvariant();
        if (!AudioExtensions.Contains(ext)) ext = ".wav";
        var destPath = Path.Combine(PathHelper.VoicesDir, safeName + ext);

        // Re-saving a library voice (e.g. after generating with it) must not copy the file onto itself
        if (!string.Equals(Path.GetFullPath(sourceFilePath), Path.GetFullPath(destPath), StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(sourceFilePath, destPath, overwrite: true);
            // Drop any same-named voice stored with a different extension
            foreach (var other in AudioExtensions.Where(e => e != ext))
            {
                var otherPath = Path.Combine(PathHelper.VoicesDir, safeName + other);
                if (File.Exists(otherPath)) File.Delete(otherPath);
            }
        }
        else
        {
            // Bump so the most recently used voice sorts first
            File.SetLastWriteTime(destPath, DateTime.Now);
        }

        var jsonPath = Path.ChangeExtension(destPath, ".json");
        var settings = new VoiceProfileSettings
        {
            EmotionMode = emotionMode,
            EmotionAlpha = emotionAlpha,
            EmotionVector = emotionVector ?? new float[8],
        };
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(settings));
        VoicesChanged?.Invoke();
        return destPath;
    }

    public void DeleteVoice(VoiceProfile voice)
    {
        if (File.Exists(voice.FilePath)) File.Delete(voice.FilePath);
        var jsonPath = Path.ChangeExtension(voice.FilePath, ".json");
        if (File.Exists(jsonPath)) File.Delete(jsonPath);
        VoicesChanged?.Invoke();
    }

    private class VoiceProfileSettings
    {
        public string EmotionMode { get; set; } = "none";
        public float EmotionAlpha { get; set; } = 0.6f;
        public float[] EmotionVector { get; set; } = new float[8];
    }
}
