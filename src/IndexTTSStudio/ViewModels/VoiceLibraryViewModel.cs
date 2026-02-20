using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IndexTTSStudio.Models;
using IndexTTSStudio.Services;
using Microsoft.Win32;

namespace IndexTTSStudio.ViewModels;

public partial class VoiceLibraryViewModel : ObservableObject
{
    private readonly VoiceLibraryService _voiceLibrary;
    private readonly AudioPlayerService _audioPlayer;

    [ObservableProperty] private List<VoiceProfile> _voices = [];
    [ObservableProperty] private bool _isPlaying;

    public VoiceLibraryViewModel(VoiceLibraryService voiceLibrary, AudioPlayerService audioPlayer)
    {
        _voiceLibrary = voiceLibrary;
        _audioPlayer = audioPlayer;
        _audioPlayer.OnPlaybackStopped += () =>
            App.Current.Dispatcher.Invoke(() => IsPlaying = false);
        Refresh();
    }

    [RelayCommand]
    private void Refresh()
    {
        Voices = _voiceLibrary.GetVoices();
    }

    [RelayCommand]
    private void AddVoice()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Audio Files|*.wav;*.mp3;*.flac",
            Title = "Add Voice to Library"
        };
        if (dlg.ShowDialog() == true)
        {
            var name = System.IO.Path.GetFileNameWithoutExtension(dlg.FileName);
            _voiceLibrary.SaveVoice(name, dlg.FileName);
            Refresh();
        }
    }

    [RelayCommand]
    private void DeleteVoice(VoiceProfile? voice)
    {
        if (voice == null) return;
        _voiceLibrary.DeleteVoice(voice.Name);
        Refresh();
    }

    [RelayCommand]
    private void PreviewVoice(VoiceProfile? voice)
    {
        if (voice == null) return;
        if (IsPlaying)
        {
            _audioPlayer.Stop();
            IsPlaying = false;
        }
        else
        {
            _audioPlayer.Play(voice.FilePath);
            IsPlaying = true;
        }
    }
}
