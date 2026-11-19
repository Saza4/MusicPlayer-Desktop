namespace MusicPlayer.Core.Services;

/// <summary>
/// Las escrituras en curso sobre archivos de audio (docs/specs/write-sessions.md). Toda herramienta que escribe en
/// un archivo de audio lo hace dentro de <see cref="Begin"/>: <c>using (writes.Begin(ids)) { await ... }</c>.
/// Al abrir una sesión, <see cref="Releasing"/> pide a quien reproduce que suelte esos archivos; solo cuando se
/// cierra la última, <see cref="AllFinished"/> le deja volver a cargarlos. Así una herramienta que termina antes
/// no reabre un archivo que otra sigue escribiendo.
/// No es seguro entre hilos: se usa desde el hilo de la interfaz.
/// </summary>
public sealed class WriteSessions
{
    private int _open;

    /// <summary>Hay al menos una escritura en curso: no se debe abrir ni preparar ningún archivo por adelantado.</summary>
    public bool IsOpen => _open > 0;

    /// <summary>Antes de escribir: los Id de las pistas cuyos archivos hay que soltar.</summary>
    public event Action<IReadOnlySet<Guid>>? Releasing;

    /// <summary>Se cerró la última sesión abierta, haya salido bien o mal la escritura.</summary>
    public event Action? AllFinished;

    public IDisposable Begin(IEnumerable<Guid> trackIds)
    {
        var ids = trackIds.ToHashSet();
        var scope = new Scope(this);
        _open++;
        try
        {
            Releasing?.Invoke(ids);
        }
        catch
        {
            // Quien llama no llega a recibir el Scope: se cierra aquí. Y como quien escucha pudo soltar el archivo
            // antes de fallar, se avisa igual si era la última.
            scope.Dispose();
            throw;
        }
        return scope;
    }

    private void End()
    {
        if (--_open == 0)
            AllFinished?.Invoke();
    }

    private sealed class Scope(WriteSessions owner) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return; // liberar dos veces no descuadra el contador
            _disposed = true;
            owner.End();
        }
    }
}
