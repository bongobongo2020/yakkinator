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
        _voiceLibrary.VoicesChanged += Refresh;
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
        }
    }

    [RelayCommand]
    private void DeleteVoice(VoiceProfile? voice)
    {
        if (voice == null) return;
        _audioPlayer.ReleaseFile(voice.FilePath);
        _voiceLibrary.DeleteVoice(voice);
    }

    [RelayCommand]
    private void PreviewVoice(VoiceProfile? voice)
    {
        if (voice == null) return;
        // Clicking the voice that's playing stops it; clicking another one switches to it
        if (IsPlaying && _audioPlayer.CurrentFile == voice.FilePath)
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
