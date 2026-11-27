namespace IndexTTSStudio.Models;

public enum TtsEngine
{
    IndexTTS,
    EchoTTS,
    IndicSpeak,
}

/// <summary>One engine button on the Generate page: what it is, whether it's picked, and its install/load state.</summary>
public partial class EngineChoice(TtsEngine engine, string name, string tagline) : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    public TtsEngine Engine { get; } = engine;
    public string Name { get; } = name;
    public string Tagline { get; } = tagline;

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private bool _isSelected;
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private bool _isInstalled;
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private string _status = "";
}
