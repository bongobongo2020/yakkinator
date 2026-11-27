namespace IndexTTSStudio.Models;

public class SetupState
{
    public bool GitInstalled { get; set; }
    public bool GitLfsInstalled { get; set; }
    public bool UvInstalled { get; set; }
    public bool RepoCloned { get; set; }
    public bool DependenciesInstalled { get; set; }
    public bool ModelsDownloaded { get; set; }
    public bool EchoRuntimeInstalled { get; set; }
    public bool EchoModelsDownloaded { get; set; }
    public bool IndicEnvInstalled { get; set; }
    public bool IndicModelDownloaded { get; set; }
    public bool VoiceDesignEnvInstalled { get; set; }
    public bool VoiceDesignModelDownloaded { get; set; }
    public bool MossDesignEnvInstalled { get; set; }
    public bool MossDesignModelDownloaded { get; set; }
    public bool VoxCpmDesignEnvInstalled { get; set; }
    public bool VoxCpmDesignModelDownloaded { get; set; }
    public string? ModelVersion { get; set; } = "IndexTTS-2";
    public DateTime? LastSetupDate { get; set; }
    public string? ErrorMessage { get; set; }
}
