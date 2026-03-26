namespace IndexTTSStudio.Models;

public class VoiceProfile
{
    public string Name { get; set; } = "";
    public string FilePath { get; set; } = "";
    public long FileSize { get; set; }
    public DateTime Modified { get; set; }
    public string EmotionMode { get; set; } = "none";
    public float EmotionAlpha { get; set; } = 0.6f;
    public float[] EmotionVector { get; set; } = new float[8];
}
