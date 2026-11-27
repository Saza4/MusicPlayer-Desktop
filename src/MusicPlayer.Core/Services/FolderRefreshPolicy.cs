namespace MusicPlayer.Core.Services;

/// <summary>
/// Cuándo se revisan las carpetas agregadas (docs/specs/auto-refresh-folders.md, criterio 11): nunca dos revisiones a
/// la vez y al menos <see cref="MinInterval"/> entre el final de una y el comienzo de la siguiente. El reloj llega como
/// parámetro para poder probarlo. Se usa desde el hilo de la interfaz.
/// </summary>
public sealed class FolderRefreshPolicy
{
    public static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(60);

    private DateTime? _lastFinishedUtc;
    private bool _running;

    /// <summary>True si puede empezar una revisión; entonces queda en curso hasta <see cref="Finish"/>.</summary>
    public bool TryStart(DateTime nowUtc)
    {
        if (_running) return false;
        // Si el reloj retrocedió (cambio de hora a mano), no se espera: mejor revisar de más que quedarse sin revisar.
        if (_lastFinishedUtc is { } last && nowUtc >= last && nowUtc - last < MinInterval) return false;
        _running = true;
        return true;
    }

    public void Finish(DateTime nowUtc)
    {
        _running = false;
        _lastFinishedUtc = nowUtc;
    }
}
