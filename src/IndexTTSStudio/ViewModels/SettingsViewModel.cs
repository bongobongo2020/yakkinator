using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IndexTTSStudio.Services;
using Microsoft.Win32;

namespace IndexTTSStudio.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;

    [ObservableProperty] private bool _useFp16;
    [ObservableProperty] private int _apiPort;
    [ObservableProperty] private float _defaultTemperature;
    [ObservableProperty] private float _defaultTopP;
    [ObservableProperty] private int _defaultTopK;
    [ObservableProperty] private string _outputDirectory = "";

    public SettingsViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;
        var s = settingsService.Settings;
        UseFp16 = s.UseFp16;
        ApiPort = s.ApiPort;
        DefaultTemperature = s.DefaultTemperature;
        DefaultTopP = s.DefaultTopP;
        DefaultTopK = s.DefaultTopK;
        OutputDirectory = s.OutputDirectory;
    }

    [RelayCommand]
    private void BrowseOutputDirectory()
    {
        var dlg = new OpenFolderDialog
        {
            Title = "Select Output Directory"
        };
        if (dlg.ShowDialog() == true)
            OutputDirectory = dlg.FolderName;
    }

    [RelayCommand]
    private void Save()
    {
        var s = _settingsService.Settings;
        s.UseFp16 = UseFp16;
        s.ApiPort = ApiPort;
        s.DefaultTemperature = DefaultTemperature;
        s.DefaultTopP = DefaultTopP;
        s.DefaultTopK = DefaultTopK;
        s.OutputDirectory = OutputDirectory;
        _settingsService.Save();
    }
}
