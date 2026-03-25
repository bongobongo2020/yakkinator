using System.IO;
using IndexTTSStudio.Models;
using IndexTTSStudio.Helpers;

namespace IndexTTSStudio.Services;

public class VoiceLibraryService
{
    public List<VoiceProfile> GetVoices()
    {
        var dir = PathHelper.VoicesDir;
        if (!Directory.Exists(dir)) return [];

        return Directory.GetFiles(dir, "*.wav")
            .Select(f => new FileInfo(f))
            .Select(fi => new VoiceProfile
            {
                Name = Path.GetFileNameWithoutExtension(fi.Name),
                FilePath = fi.FullName,
                FileSize = fi.Length,
                Modified = fi.LastWriteTime,
            })
            .OrderByDescending(v => v.Modified)
            .ToList();
    }

    public string SaveVoice(string name, string sourceFilePath)
    {
        Directory.CreateDirectory(PathHelper.VoicesDir);
        var safeName = new string(name.Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == ' ').ToArray()).Trim();
        var destPath = Path.Combine(PathHelper.VoicesDir, $"{safeName}.wav");
        File.Copy(sourceFilePath, destPath, overwrite: true);
        return destPath;
    }

    public void DeleteVoice(string name)
    {
        var path = Path.Combine(PathHelper.VoicesDir, $"{name}.wav");
        if (File.Exists(path)) File.Delete(path);
    }
}
