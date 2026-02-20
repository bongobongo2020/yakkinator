using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IndexTTSStudio.Helpers;
using IndexTTSStudio.Services;

namespace IndexTTSStudio.ViewModels;

public partial class HistoryViewModel : ObservableObject
{
    private readonly AudioPlayerService _audioPlayer;

    [ObservableProperty] private List<OutputItem> _outputs = [];
    [ObservableProperty] private bool _isPlaying;
    [ObservableProperty] private string? _currentlyPlaying;

    public HistoryViewModel(AudioPlayerService audioPlayer)
    {
        _audioPlayer = audioPlayer;
        _audioPlayer.OnPlaybackStopped += () =>
            App.Current.Dispatcher.Invoke(() => { IsPlaying = false; CurrentlyPlaying = null; });
        Refresh();
    }

    [RelayCommand]
    private void Refresh()
    {
        var dir = PathHelper.OutputsDir;
        if (!Directory.Exists(dir)) { Outputs = []; return; }

        Outputs = Directory.GetFiles(dir, "*.wav")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.CreationTime)
            .Select(f => new OutputItem
            {
                FileName = f.Name,
                FilePath = f.FullName,
                Created = f.CreationTime,
                SizeKb = f.Length / 1024.0,
            })
            .ToList();
    }

    [RelayCommand]
    private void Play(OutputItem? item)
    {
        if (item == null) return;
        if (IsPlaying && CurrentlyPlaying == item.FilePath)
        {
            _audioPlayer.Stop();
            IsPlaying = false;
            CurrentlyPlaying = null;
        }
        else
        {
            _audioPlayer.Play(item.FilePath);
            IsPlaying = true;
            CurrentlyPlaying = item.FilePath;
        }
    }

    [RelayCommand]
    private void Delete(OutputItem? item)
    {
        if (item == null) return;
        if (File.Exists(item.FilePath)) File.Delete(item.FilePath);
        Refresh();
    }
}

public class OutputItem
{
    public string FileName { get; set; } = "";
    public string FilePath { get; set; } = "";
    public DateTime Created { get; set; }
    public double SizeKb { get; set; }
}
