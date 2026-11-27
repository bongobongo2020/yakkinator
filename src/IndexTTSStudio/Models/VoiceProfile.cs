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
    /// <summary>Set for voices made with the voice designer: the description it was made from.</summary>
    public string? DesignDescription { get; set; }
    /// <summary>What the clip says, when known (designed voices).</summary>
    public string? Transcript { get; set; }
    public bool IsDesigned => !string.IsNullOrEmpty(DesignDescription);
}

/// <summary>How a designed voice was made, saved next to its clip.</summary>
public record VoiceDesignInfo(string Description, string Transcript, string Language, long Seed, string Model);
