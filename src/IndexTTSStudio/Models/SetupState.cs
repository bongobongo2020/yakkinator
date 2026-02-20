namespace IndexTTSStudio.Models;

public class SetupState
{
    public bool GitInstalled { get; set; }
    public bool GitLfsInstalled { get; set; }
    public bool UvInstalled { get; set; }
    public bool RepoCloned { get; set; }
    public bool DependenciesInstalled { get; set; }
    public bool ModelsDownloaded { get; set; }
    public string? ModelVersion { get; set; } = "IndexTTS-2";
    public DateTime? LastSetupDate { get; set; }
    public string? ErrorMessage { get; set; }
}
