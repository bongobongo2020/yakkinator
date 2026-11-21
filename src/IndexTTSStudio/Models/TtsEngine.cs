namespace IndexTTSStudio.Models;

public enum TtsEngine
{
    IndexTTS,
    EchoTTS,
    IndicSpeak,
}

public record TtsEngineOption(TtsEngine Engine, string Name);
