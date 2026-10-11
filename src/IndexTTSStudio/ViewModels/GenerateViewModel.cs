using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IndexTTSStudio.Helpers;
using IndexTTSStudio.Models;
using IndexTTSStudio.Services;
using Microsoft.Win32;

namespace IndexTTSStudio.ViewModels;

public partial class GenerateViewModel : ObservableObject
{
    private readonly TTSApiClient _apiClient;
    private readonly EchoBackendService _echo;
    private readonly AudioPlayerService _audioPlayer;
    private readonly VoiceLibraryService _voiceLibrary;
    private readonly MainWindowViewModel _mainVm;
    private readonly SettingsService _settingsService;
    private readonly SetupService _setupService;

    public IReadOnlyList<TtsEngineOption> Engines { get; } =
    [
        new(TtsEngine.IndexTTS, "IndexTTS-2"),
        new(TtsEngine.EchoTTS, "Echo-TTS"),
    ];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIndexTts), nameof(IsEcho), nameof(IsEngineReady), nameof(BackendStatus), nameof(EngineReadyText))]
    private TtsEngine _selectedEngine;

    public bool IsIndexTts => SelectedEngine == TtsEngine.IndexTTS;
    public bool IsEcho => SelectedEngine == TtsEngine.EchoTTS;

    // Echo-TTS loads on the first generation (it needs the voice up front), so it's "ready" once installed
    public bool IsEngineReady => IsEcho ? _setupService.IsEchoSetupComplete : _mainVm.IsBackendRunning;
    public string EngineReadyText => IsEcho ? "Echo-TTS ready" : "Backend ready";
    public string BackendStatus => IsEcho
        ? "Echo-TTS isn't installed yet. Install it from the Setup page."
        : _mainVm.BackendStatus;

    [ObservableProperty] private string _text = "";
    [ObservableProperty] private string _voiceFilePath = "";
    [ObservableProperty] private string _voiceFileName = "No voice selected";
    [ObservableProperty] private string _emotionMode = "none";
    [ObservableProperty] private string _emotionAudioPath = "";
    [ObservableProperty] private float _emotionAlpha = 0.6f;

    // Emotion sliders
    [ObservableProperty] private float _joy;
    [ObservableProperty] private float _anger;
    [ObservableProperty] private float _sadness;
    [ObservableProperty] private float _fear;
    [ObservableProperty] private float _disgust;
    [ObservableProperty] private float _melancholy;
    [ObservableProperty] private float _surprise;
    [ObservableProperty] private float _calm;

    // Generation params
    [ObservableProperty] private float _temperature = 1.0f;
    [ObservableProperty] private float _topP = 0.8f;
    [ObservableProperty] private int _topK = 30;
    [ObservableProperty] private int _maxTokens = 120;

    // Echo-TTS params (applied by relaunching its server, so changing them costs a model reload)
    [ObservableProperty] private int _echoSteps = 40;
    [ObservableProperty] private float _echoCfgText = 3.0f;
    [ObservableProperty] private float _echoCfgSpeaker = 8.0f;
    [ObservableProperty] private int _echoSeed;

    // State
    [ObservableProperty] private bool _isGenerating;
    [ObservableProperty] private bool _isPlaying;
    [ObservableProperty] private string _statusText = "Ready";
    [ObservableProperty] private string? _lastOutputPath;
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private string _errorMessage = "";
    [ObservableProperty] private double _generationProgress;

    // Voice library
    [ObservableProperty] private List<VoiceProfile> _savedVoices = [];

    public GenerateViewModel(TTSApiClient apiClient, EchoBackendService echo, AudioPlayerService audioPlayer,
        VoiceLibraryService voiceLibrary, MainWindowViewModel mainVm, SettingsService settingsService,
        SetupService setupService)
    {
        _apiClient = apiClient;
        _echo = echo;
        _audioPlayer = audioPlayer;
        _voiceLibrary = voiceLibrary;
        _mainVm = mainVm;
        _settingsService = settingsService;
        _setupService = setupService;
        _selectedEngine = mainVm.SelectedEngine;

        var settings = settingsService.Settings;
        _temperature = settings.DefaultTemperature;
        _topP = settings.DefaultTopP;
        _topK = settings.DefaultTopK;

        _mainVm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.IsBackendRunning))
            {
                OnPropertyChanged(nameof(IsEngineReady));
                if (_mainVm.IsBackendRunning && IsIndexTts)
                    StatusText = "Backend ready";
            }
            else if (e.PropertyName == nameof(MainWindowViewModel.BackendStatus))
                OnPropertyChanged(nameof(BackendStatus));
        };
        _setupService.OnEchoSetupCompleted += () => App.Current.Dispatcher.BeginInvoke(() =>
        {
            OnPropertyChanged(nameof(IsEngineReady));
            if (IsEcho) StatusText = "Echo-TTS ready";
        });
        _audioPlayer.OnPlaybackStopped += () =>
            App.Current.Dispatcher.Invoke(() => IsPlaying = false);
        _voiceLibrary.VoicesChanged += RefreshVoices;
        RefreshVoices();
    }

    partial void OnSelectedEngineChanged(TtsEngine value)
    {
        HasError = false;
        StatusText = value == TtsEngine.EchoTTS && _setupService.IsEchoSetupComplete
            ? "Echo-TTS loads on the first generation."
            : "Ready";
        _mainVm.SelectedEngine = value;
        _settingsService.Settings.Engine = value;
        try { _settingsService.Save(); }
        catch (Exception ex) { LogError($"Saving engine choice failed: {ex.Message}"); }
    }

    [RelayCommand]
    private void BrowseVoice()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Audio Files|*.wav;*.mp3;*.flac|All Files|*.*",
            Title = "Select Reference Voice"
        };
        if (dlg.ShowDialog() == true)
        {
            VoiceFilePath = dlg.FileName;
            VoiceFileName = System.IO.Path.GetFileName(dlg.FileName);
        }
    }

    [RelayCommand]
    private void BrowseEmotionAudio()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Audio Files|*.wav;*.mp3;*.flac|All Files|*.*",
            Title = "Select Emotion Reference Audio"
        };
        if (dlg.ShowDialog() == true)
        {
            EmotionAudioPath = dlg.FileName;
        }
    }

    [RelayCommand]
    private void SelectSavedVoice(VoiceProfile? profile)
    {
        if (profile == null) return;
        VoiceFilePath = profile.FilePath;
        VoiceFileName = profile.Name;
        EmotionMode = profile.EmotionMode;
        EmotionAlpha = profile.EmotionAlpha;
        var v = profile.EmotionVector;
        Joy        = v.Length > 0 ? v[0] : 0f;
        Anger      = v.Length > 1 ? v[1] : 0f;
        Sadness    = v.Length > 2 ? v[2] : 0f;
        Fear       = v.Length > 3 ? v[3] : 0f;
        Disgust    = v.Length > 4 ? v[4] : 0f;
        Melancholy = v.Length > 5 ? v[5] : 0f;
        Surprise   = v.Length > 6 ? v[6] : 0f;
        Calm       = v.Length > 7 ? v[7] : 0f;
    }

    [RelayCommand]
    private async Task GenerateAsync()
    {
        if (string.IsNullOrWhiteSpace(Text))
        {
            StatusText = "Please enter text to synthesize.";
            return;
        }
        if (string.IsNullOrWhiteSpace(VoiceFilePath) || !System.IO.File.Exists(VoiceFilePath))
        {
            StatusText = "Please select a reference voice file.";
            return;
        }
        if (IsEcho && !_setupService.IsEchoSetupComplete)
        {
            StatusText = "Install Echo-TTS from the Setup page first.";
            return;
        }
        if (IsIndexTts && EmotionMode == "audio" && (string.IsNullOrWhiteSpace(EmotionAudioPath) || !System.IO.File.Exists(EmotionAudioPath)))
        {
            StatusText = "Please select an emotion reference audio file.";
            return;
        }

        IsGenerating = true;
        HasError = false;
        GenerationProgress = 0;
        StatusText = "Generating speech...";

        using var progressCts = new CancellationTokenSource();
        var progressTask = AnimateProgressAsync(progressCts.Token);

        try
        {
            var result = IsEcho
                ? await _echo.GenerateAsync(Text, VoiceFilePath,
                    new EchoSamplerSettings(EchoSteps, EchoCfgText, EchoCfgSpeaker, EchoSeed),
                    status => App.Current.Dispatcher.BeginInvoke(() => StatusText = status))
                : await _apiClient.GenerateAsync(new TTSRequest
                {
                    Text = Text,
                    VoiceFilePath = VoiceFilePath,
                    EmotionMode = EmotionMode,
                    EmotionAudioPath = EmotionMode == "audio" ? EmotionAudioPath : null,
                    EmotionAlpha = EmotionAlpha,
                    EmotionVector = [Joy, Anger, Sadness, Fear, Disgust, Melancholy, Surprise, Calm],
                    Temperature = Temperature,
                    TopP = TopP,
                    TopK = TopK,
                    MaxTokens = MaxTokens,
                });

            progressCts.Cancel();
            await progressTask;

            if (result.Success)
            {
                GenerationProgress = 100;
                LastOutputPath = result.AudioFilePath;
                StatusText = $"Generation complete! ({result.JobId})";
                // Remember the voice and its emotion settings; a failure here must not
                // turn a successful generation into an error.
                try { SaveCurrentVoice(); }
                catch (Exception ex) { LogError($"Auto-saving voice failed: {ex.Message}"); }
            }
            else
            {
                ShowError(result.ErrorMessage ?? "Unknown error");
            }
        }
        catch (Exception ex)
        {
            progressCts.Cancel();
            await progressTask;
            ShowError(ex.Message);
        }
        finally
        {
            IsGenerating = false;
        }
    }

    private async Task AnimateProgressAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                // Continuation resumes on the UI thread, so no dispatcher hop is needed
                await Task.Delay(150, ct);
                GenerationProgress += (90.0 - GenerationProgress) * 0.06;
            }
        }
        catch (OperationCanceledException) { }
    }

    [RelayCommand]
    private void PlayOutput()
    {
        if (LastOutputPath == null || !System.IO.File.Exists(LastOutputPath)) return;

        if (IsPlaying)
        {
            _audioPlayer.Stop();
            IsPlaying = false;
        }
        else
        {
            try
            {
                _audioPlayer.Play(LastOutputPath);
                IsPlaying = true;
            }
            catch (Exception ex)
            {
                StatusText = $"Playback failed: {ex.Message}";
            }
        }
    }

    [RelayCommand]
    private void SaveVoice()
    {
        if (string.IsNullOrWhiteSpace(VoiceFilePath) || !File.Exists(VoiceFilePath)) return;
        SaveCurrentVoice();
        StatusText = $"Saved \"{VoiceFileName}\" to the voice library.";
    }

    private void SaveCurrentVoice()
    {
        var name = Path.GetFileNameWithoutExtension(VoiceFilePath);
        VoiceFilePath = _voiceLibrary.SaveVoice(name, VoiceFilePath, EmotionMode, EmotionAlpha,
            [Joy, Anger, Sadness, Fear, Disgust, Melancholy, Surprise, Calm]);
    }

    private void ShowError(string message)
    {
        ErrorMessage = message;
        HasError = true;
        StatusText = "Generation failed";
        LogError(message);
    }

    private static void LogError(string message)
    {
        try
        {
            PathHelper.EnsureDirectories();
            File.AppendAllText(PathHelper.LogFile,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR: {message}{Environment.NewLine}");
        }
        catch { /* log write failure is non-fatal */ }
    }

    private void RefreshVoices()
    {
        SavedVoices = _voiceLibrary.GetVoices();
    }
}
