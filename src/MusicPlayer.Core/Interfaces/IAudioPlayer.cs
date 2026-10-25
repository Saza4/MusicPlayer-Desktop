namespace MusicPlayer.Core.Interfaces;

public enum PlaybackState { Stopped, Playing, Paused }

public interface IAudioPlayer : IDisposable
{
    PlaybackState State { get; }
    TimeSpan CurrentTime { get; }
    TimeSpan Duration { get; }

    /// <summary>0.0 a 1.0</summary>
    double Volume { get; set; }

    void Load(string filePath);
    void Play();
    void Pause();
    void Stop();
    void Seek(TimeSpan position);

    event EventHandler<PlaybackState>? StateChanged;
    event EventHandler? TrackEnded;
}