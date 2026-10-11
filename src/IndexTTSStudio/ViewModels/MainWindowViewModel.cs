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
}
