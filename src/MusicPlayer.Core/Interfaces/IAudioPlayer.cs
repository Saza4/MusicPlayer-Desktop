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

    /// <summary>Detiene y libera el archivo actual (necesario para poder borrarlo en Windows).</summary>
    void Unload();
    /// <summary>
    /// Deja lista la pista que seguirá a la actual para que empiece sin silencio al terminar esta.
    /// Null cancela la preparada. Si no se puede preparar, la transición sigue funcionando, solo que con un pequeño silencio.
    /// </summary>
    void PrepareNext(string? filePath);
    void Seek(TimeSpan position);

    event EventHandler<PlaybackState>? StateChanged;
    event EventHandler? TrackEnded;
    /// <summary>La reproducción pasó sola a la pista preparada. El argumento es la ruta de esa pista.</summary>
    event EventHandler<string>? AdvancedToNext;
}