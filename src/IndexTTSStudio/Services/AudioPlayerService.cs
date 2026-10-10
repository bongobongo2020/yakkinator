using NAudio.Wave;

namespace IndexTTSStudio.Services;

public class AudioPlayerService : IDisposable
{
    private WaveOutEvent? _waveOut;
    private AudioFileReader? _audioFile;

    /// <summary>
    /// Raised whenever playback ends, whether it finished naturally or was stopped
    /// (including by another page starting playback). Always raised on the UI thread.
    /// </summary>
    public event Action? OnPlaybackStopped;
    public bool IsPlaying => _waveOut?.PlaybackState == PlaybackState.Playing;
    public string? CurrentFile { get; private set; }

    public void Play(string filePath)
    {
        Stop();
        try
        {
            _audioFile = new AudioFileReader(filePath);
            _waveOut = new WaveOutEvent();
            _waveOut.Init(_audioFile);
            _waveOut.PlaybackStopped += OnWaveOutStopped;
            _waveOut.Play();
            CurrentFile = filePath;
        }
        catch
        {
            Release();
            throw;
        }
    }

    private void OnWaveOutStopped(object? sender, StoppedEventArgs e)
    {
        // Ignore late events from a device we've already replaced
        if (!ReferenceEquals(sender, _waveOut)) return;
        Release();
        OnPlaybackStopped?.Invoke();
    }

    public void Stop()
    {
        var wasActive = _waveOut != null;
        Release();
        // Notify synchronously: NAudio's own event is posted and would arrive after a
        // subsequent Play(), wrongly flipping the new playback's state back to stopped.
        if (wasActive) OnPlaybackStopped?.Invoke();
    }

    /// <summary>Stops playback if it's using the given file, so the file can be deleted or overwritten.</summary>
    public void ReleaseFile(string filePath)
    {
        if (CurrentFile != null && string.Equals(
                System.IO.Path.GetFullPath(CurrentFile), System.IO.Path.GetFullPath(filePath),
                StringComparison.OrdinalIgnoreCase))
            Stop();
    }

    private void Release()
    {
        if (_waveOut != null)
        {
            _waveOut.PlaybackStopped -= OnWaveOutStopped;
            _waveOut.Stop();
            _waveOut.Dispose();
        }
        _audioFile?.Dispose();
        _waveOut = null;
        _audioFile = null;
        CurrentFile = null;
    }

    public void Dispose() => Release();
}
