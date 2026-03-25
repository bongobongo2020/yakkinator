namespace IndexTTSStudio.Models;

public class AppSettings
{
    public bool UseFp16 { get; set; } = true;
    public int ApiPort { get; set; } = 5299;
    public string ModelVersion { get; set; } = "IndexTTS-2";
    public bool AutoStartBackend { get; set; } = true;
    public string OutputDirectory { get; set; } = "";
    public float DefaultTemperature { get; set; } = 1.0f;
    public float DefaultTopP { get; set; } = 0.8f;
    public int DefaultTopK { get; set; } = 30;
}
