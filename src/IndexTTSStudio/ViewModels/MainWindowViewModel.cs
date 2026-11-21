using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IndexTTSStudio.Models;

namespace IndexTTSStudio.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _backendStatus = "Stopped";

    [ObservableProperty]
    private bool _isBackendRunning;

    /// <summary>The engine picked on the Generate page; MainWindow starts/stops backends to match.</summary>
    [ObservableProperty]
    private TtsEngine _selectedEngine;

    /// <summary>True after another app (e.g. FlipPix, before its LLM runs) had the engines unloaded to free GPU
    /// memory. The selected engine loads again on the next generation.</summary>
    [ObservableProperty]
    private bool _isUnloaded;

    /// <summary>Unloads every engine (MainWindow); true when one was loaded.</summary>
    public Func<Task<bool>>? UnloadEnginesAsync { get; set; }

    /// <summary>Starts IndexTTS-2 again after an unload (MainWindow); completes once it is running or failed.</summary>
    public Func<Task>? ReloadIndexTtsAsync { get; set; }
}
