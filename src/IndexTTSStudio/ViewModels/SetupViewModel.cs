using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IndexTTSStudio.Models;
using IndexTTSStudio.Services;

namespace IndexTTSStudio.ViewModels;

public partial class SetupViewModel : ObservableObject
{
    private readonly SetupService _setupService;
    private readonly EchoBackendService _echo;
    private readonly IndicBackendService _indic;
    private readonly VoiceDesignService _designer;

    [ObservableProperty] private string _statusText = "Ready to set up IndexTTS-2";
    [ObservableProperty] private double _progress;
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private bool _isComplete;
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private string _errorText = "";
    [ObservableProperty] private string _logText = "";
    [ObservableProperty] private bool _canRedownloadModels;
    [ObservableProperty] private bool _isEchoInstalled;
    [ObservableProperty] private bool _isIndicInstalled;
    [ObservableProperty] private bool _isVoiceDesignInstalled;
    [ObservableProperty] private bool _isMossDesignInstalled;
    [ObservableProperty] private bool _isVoxCpmDesignInstalled;
    /// <summary>Hugging Face read token, used once for the gated download and never saved.</summary>
    [ObservableProperty] private string _hfToken = "";

    public string IndicModelUrl => SetupService.IndicModelUrl;
    public string HfTokenUrl => SetupService.HfTokenUrl;

    public SetupViewModel(SetupService setupService, EchoBackendService echo, IndicBackendService indic,
        VoiceDesignService designer)
    {
        _setupService = setupService;
        _echo = echo;
        _indic = indic;
        _designer = designer;
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
        IsEchoInstalled = _setupService.IsEchoSetupComplete;
        IsIndicInstalled = _setupService.IsIndicSetupComplete;
        IsVoiceDesignInstalled = _setupService.IsVoiceDesignSetupComplete;
        IsMossDesignInstalled = _setupService.IsDesignerSetupComplete(VoiceDesigner.Moss);
        IsVoxCpmDesignInstalled = _setupService.IsDesignerSetupComplete(VoiceDesigner.VoxCpm);
    }

    [RelayCommand]
    private Task InstallVoiceDesignAsync() => InstallDesignerAsync(VoiceDesigner.Qwen, IsVoiceDesignInstalled,
        () => IsVoiceDesignInstalled = true);

    [RelayCommand]
    private Task InstallMossDesignAsync() => InstallDesignerAsync(VoiceDesigner.Moss, IsMossDesignInstalled,
        () => IsMossDesignInstalled = true);

    [RelayCommand]
    private Task InstallVoxCpmDesignAsync() => InstallDesignerAsync(VoiceDesigner.VoxCpm, IsVoxCpmDesignInstalled,
        () => IsVoxCpmDesignInstalled = true);

    private async Task InstallDesignerAsync(VoiceDesigner id, bool reinstall, Action markInstalled)
    {
        var name = VoiceDesigners.Get(id).Name;
        IsRunning = true;
        HasError = false;
        ErrorText = "";
        LogText = "";

        try
        {
            // A running server keeps the model files open
            await _designer.StopAsync();
            await _setupService.InstallDesignerAsync(id, reinstall);
            markInstalled();
            StatusText = $"{name} installed! Open the Voice Library page to design a voice.";
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorText = ex.Message;
            StatusText = $"{name} install failed. See error below.";
        }
        finally
        {
            IsRunning = false;
        }
    }

    [RelayCommand]
    private async Task InstallIndicAsync()
    {
        var force = IsIndicInstalled;
        IsRunning = true;
        HasError = false;
        ErrorText = "";
        LogText = "";

        try
        {
            // A running server keeps the model files open
            await _indic.StopAsync();
            await _setupService.InstallIndicAsync(HfToken, force);
            IsIndicInstalled = true;
            HfToken = "";
            StatusText = "Indic-Speak installed! Pick it from the engine menu on the Generate page.";
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorText = ex.Message;
            StatusText = "Indic-Speak install failed. See error below.";
        }
        finally
        {
            IsRunning = false;
        }
    }

    [RelayCommand]
    private async Task InstallEchoAsync()
    {
        // A reinstall re-downloads everything; a first install resumes whatever is missing
        var force = IsEchoInstalled;
        IsRunning = true;
        HasError = false;
        ErrorText = "";
        LogText = "";

        try
        {
            // A running server keeps its DLLs locked
            await _echo.StopAsync();
            await _setupService.InstallEchoAsync(force);
            IsEchoInstalled = true;
            StatusText = "Echo-TTS installed! Pick it from the engine menu on the Generate page.";
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorText = ex.Message;
            StatusText = "Echo-TTS install failed. See error below.";
        }
        finally
        {
            IsRunning = false;
        }
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
