using System.IO;

namespace IndexTTSStudio.Helpers;

public static class OutputHelper
{
    /// <summary>
    /// Writes generated audio to the outputs folder (which History lists), plus a copy in
    /// the user's custom output directory when one is set. Returns the outputs-folder path.
    /// </summary>
    public static async Task<string> SaveAsync(byte[] audioData, string jobId, string? customDir, CancellationToken ct = default)
    {
        PathHelper.EnsureDirectories();
        var outputPath = Path.Combine(PathHelper.OutputsDir, $"{jobId}.wav");
        await File.WriteAllBytesAsync(outputPath, audioData, ct);

        if (!string.IsNullOrWhiteSpace(customDir) && Directory.Exists(customDir))
            await File.WriteAllBytesAsync(Path.Combine(customDir, $"{jobId}.wav"), audioData, ct);

        return outputPath;
    }

    public static string NewJobId() => Guid.NewGuid().ToString()[..8];
}
