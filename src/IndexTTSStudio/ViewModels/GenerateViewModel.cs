using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IndexTTSStudio.Models;
using IndexTTSStudio.Services;
using Microsoft.Win32;

namespace IndexTTSStudio.ViewModels;

public partial class GenerateViewModel : ObservableObject
{
    private readonly TTSApiClient _apiClient;
    private readonly AudioPlayerService _audioPlayer;
    private readonly VoiceLibraryService _voiceLibrary;

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

    // State
    [ObservableProperty] private bool _isGenerating;
    [ObservableProperty] private bool _isPlaying;
    [ObservableProperty] private string _statusText = "Ready";
    [ObservableProperty] private string? _lastOutputPath;

    // Voice library
    [ObservableProperty] private List<VoiceProfile> _savedVoices = [];

    public GenerateViewModel(TTSApiClient apiClient, AudioPlayerService audioPlayer, VoiceLibraryService voiceLibrary)
    {
        _apiClient = apiClient;
        _audioPlayer = audioPlayer;
        _voiceLibrary = voiceLibrary;
        _audioPlayer.OnPlaybackStopped += () =>
            App.Current.Dispatcher.Invoke(() => IsPlaying = false);
        RefreshVoices();
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

        IsGenerating = true;
        StatusText = "Generating speech...";

        try
        {
            var request = new TTSRequest
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
            };

            var result = await _apiClient.GenerateAsync(request);

            if (result.Success)
            {
                LastOutputPath = result.AudioFilePath;
                StatusText = $"Generation complete! ({result.JobId})";
            }
            else
            {
                StatusText = $"Generation failed: {result.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsGenerating = false;
        }
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
            _audioPlayer.Play(LastOutputPath);
            IsPlaying = true;
        }
    }

    [RelayCommand]
    private void SaveVoice()
    {
        if (string.IsNullOrWhiteSpace(VoiceFilePath)) return;
        var name = System.IO.Path.GetFileNameWithoutExtension(VoiceFilePath);
        _voiceLibrary.SaveVoice(name, VoiceFilePath);
        RefreshVoices();
    }

    private void RefreshVoices()
    {
        SavedVoices = _voiceLibrary.GetVoices();
    }
}
