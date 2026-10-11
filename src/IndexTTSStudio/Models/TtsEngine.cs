namespace IndexTTSStudio.Models;

public enum TtsEngine
{
    IndexTTS,
    EchoTTS,
}

public record TtsEngineOption(TtsEngine Engine, string Name);
