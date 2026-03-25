namespace IndexTTSStudio.Models;

public class TTSResponse
{
    public bool Success { get; set; }
    public string? AudioFilePath { get; set; }
    public string? ErrorMessage { get; set; }
    public string? JobId { get; set; }
}
