namespace MusicPlayer.Core.Services;

/// <summary>
/// Cuenta las órdenes que cambian lo que hay cargado en el reproductor (cargar una pista, descargarla). Los avisos
/// de fin de pista salen de otro hilo y se atienden más tarde: cada uno anota la cuenta del momento en que salió
/// y, si al atenderlo ya no coincide, es de una pista anterior a la última orden del usuario y se descarta
/// (docs/specs/audit-3.md, H1). Seguro entre hilos.
/// </summary>
public sealed class PlaybackEpoch
{
    private int _value;

    /// <summary>La cuenta de ahora: lo que anota un aviso al salir.</summary>
    public int Current => Volatile.Read(ref _value);

    /// <summary>Una orden nueva: los avisos anotados antes dejan de valer.</summary>
    public void Advance() => Interlocked.Increment(ref _value);

    /// <summary>True si no hubo ninguna orden desde que el aviso anotó <paramref name="stamp"/>.</summary>
    public bool IsCurrent(int stamp) => stamp == Current;
}
