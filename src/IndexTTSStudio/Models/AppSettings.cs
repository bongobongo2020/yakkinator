namespace IndexTTSStudio.Models;

public class AppSettings
{
    public TtsEngine Engine { get; set; } = TtsEngine.IndexTTS;
    public bool UseFp16 { get; set; } = true;
    public int ApiPort { get; set; } = 5299;
    public int EchoApiPort { get; set; } = 5300;
    public int IndicApiPort { get; set; } = 5302;
    public int VoiceDesignApiPort { get; set; } = 5303;

    // Indic-Speak: last-used voice and style (it has a fixed voice library, no cloning)
    public string IndicLanguage { get; set; } = "hi";
    public string IndicSpeaker { get; set; } = "Kavya";
    public string IndicStyle { get; set; } = "";

    // Voice designer: last description and sample script
    public string VoiceDesignDescription { get; set; } = "";
    public string VoiceDesignScript { get; set; } = "";
    public string VoiceDesignLanguage { get; set; } = "English";
    public string ModelVersion { get; set; } = "IndexTTS-2";
    public bool AutoStartBackend { get; set; } = true;
    public string OutputDirectory { get; set; } = "";
    public float DefaultTemperature { get; set; } = 1.0f;
    public float DefaultTopP { get; set; } = 0.8f;
    public int DefaultTopK { get; set; } = 30;

    // Remote access for the Android app
    public bool RemoteEnabled { get; set; }
    public int RemotePort { get; set; } = 5301;
    public string RemoteToken { get; set; } = "";
    /// <summary>Address the phone should use (e.g. the PC's Tailscale IP); empty = auto-detect.</summary>
    public string RemoteAddress { get; set; } = "";
}
