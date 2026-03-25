namespace IndexTTSStudio.Models;

public class VoiceProfile
{
    public string Name { get; set; } = "";
    public string FilePath { get; set; } = "";
    public long FileSize { get; set; }
    public DateTime Modified { get; set; }
}
