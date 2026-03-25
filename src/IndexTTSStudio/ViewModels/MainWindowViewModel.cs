using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace IndexTTSStudio.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _backendStatus = "Stopped";

    [ObservableProperty]
    private bool _isBackendRunning;
}
