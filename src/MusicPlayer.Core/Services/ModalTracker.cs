namespace MusicPlayer.Core.Services;

/// <summary>
/// Sabe si la app tiene abierto un selector de archivos o un diálogo propio. Todo selector o diálogo
/// se abre dentro de <see cref="Begin"/>: <c>using (modals.Begin()) { await ... }</c>.
/// Admite anidamiento; <see cref="Changed"/> solo avisa al abrirse el primero y al cerrarse el último.
/// No es seguro entre hilos: se usa desde el hilo de la interfaz.
/// </summary>
public sealed class ModalTracker
{
    private int _depth;

    public bool IsOpen => _depth > 0;

    public event EventHandler? Changed;

    public IDisposable Begin()
    {
        var scope = new Scope(this);
        if (++_depth != 1) return scope;

        try
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            // Quien llama no llega a recibir el Scope: sin esto el contador se quedaría subido para siempre
            // (docs/specs/audit-3.md, H10).
            _depth--;
            throw;
        }
        return scope;
    }

    private void End()
    {
        if (--_depth == 0)
            Changed?.Invoke(this, EventArgs.Empty);
    }

    private sealed class Scope(ModalTracker owner) : IDisposable
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
