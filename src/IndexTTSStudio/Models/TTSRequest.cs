namespace IndexTTSStudio.Models;

public class TTSRequest
{
    public string Text { get; set; } = "";
    public string VoiceFilePath { get; set; } = "";
    public string EmotionMode { get; set; } = "none"; // none, audio, vector, text
    public string? EmotionAudioPath { get; set; }
    public float EmotionAlpha { get; set; } = 0.6f;
    public float[] EmotionVector { get; set; } = new float[8]; // joy,anger,sad,fear,disgust,melancholy,surprise,calm
    public float Temperature { get; set; } = 1.0f;
    public float TopP { get; set; } = 0.8f;
    public int TopK { get; set; } = 30;
    public int MaxTokens { get; set; } = 120;
}
