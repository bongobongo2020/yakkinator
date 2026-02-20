using NAudio.Wave;

namespace IndexTTSStudio.Services;

public class AudioPlayerService : IDisposable
{
    private WaveOutEvent? _waveOut;
    private AudioFileReader? _audioFile;

    public event Action? OnPlaybackStopped;
    public bool IsPlaying => _waveOut?.PlaybackState == PlaybackState.Playing;

    public void Play(string filePath)
    {
        Stop();
        _audioFile = new AudioFileReader(filePath);
        _waveOut = new WaveOutEvent();
        _waveOut.Init(_audioFile);
        _waveOut.PlaybackStopped += (_, _) => OnPlaybackStopped?.Invoke();
        _waveOut.Play();
    }

    public void Stop()
    {
        _waveOut?.Stop();
        _waveOut?.Dispose();
        _audioFile?.Dispose();
        _waveOut = null;
        _audioFile = null;
    }

    public void Dispose() => Stop();
}
