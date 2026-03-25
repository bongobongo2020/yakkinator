using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IndexTTSStudio.Services;

namespace IndexTTSStudio.ViewModels;

public partial class SetupViewModel : ObservableObject
{
    private readonly SetupService _setupService;

    [ObservableProperty] private string _statusText = "Ready to set up IndexTTS-2";
    [ObservableProperty] private double _progress;
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private bool _isComplete;
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private string _errorText = "";
    [ObservableProperty] private string _logText = "";
    [ObservableProperty] private bool _canRedownloadModels;

    public SetupViewModel(SetupService setupService)
    {
        _setupService = setupService;
        _setupService.OnStatusChanged += msg =>
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                StatusText = msg;
                LogText += msg + "\n";
            });
        };
        _setupService.OnProgressChanged += pct =>
        {
            App.Current.Dispatcher.Invoke(() => Progress = pct * 100);
        };

        IsComplete = _setupService.IsSetupComplete;
        CanRedownloadModels = _setupService.State.ModelsDownloaded;
    }

    [RelayCommand]
    private async Task RunSetupAsync()
    {
        IsRunning = true;
        HasError = false;
        ErrorText = "";
        LogText = "";

        try
        {
            await _setupService.RunFullSetupAsync();
            IsComplete = true;
            CanRedownloadModels = true;
            StatusText = "Setup complete! You can now generate speech.";
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorText = ex.Message;
            StatusText = "Setup failed. See error below.";
        }
        finally
        {
            IsRunning = false;
        }
    }

    [RelayCommand]
    private async Task RedownloadModelsAsync()
    {
        IsRunning = true;
        HasError = false;
        ErrorText = "";
        LogText = "";

        try
        {
            await _setupService.DownloadModelsAsync(force: true);
            StatusText = "Models re-downloaded successfully!";
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorText = ex.Message;
            StatusText = "Model download failed. See error below.";
        }
        finally
        {
            IsRunning = false;
        }
    }
}
