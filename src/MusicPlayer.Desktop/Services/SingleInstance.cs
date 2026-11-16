using System;
using System.Runtime.Versioning;
using System.Threading;

namespace MusicPlayer.Desktop.Services;

/// <summary>
/// Una sola instancia por sesión de Windows (docs/specs/background-playback.md, criterios 10 y 11). Con la ventana
/// oculta en la bandeja, abrir la app otra vez dejaría dos procesos escribiendo los mismos JSON: la segunda avisa
/// a la primera (que muestra su ventana) y termina sin cargar nada.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class SingleInstance : IDisposable
{
    // «Local\»: por sesión de Windows, así cada usuario con sesión abierta tiene su instancia.
    private const string MutexName = @"Local\MusicPlayer.Desktop.Instancia";
    private const string EventName = @"Local\MusicPlayer.Desktop.Mostrar";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _show;
    private volatile bool _disposed;

    private SingleInstance(Mutex mutex, EventWaitHandle show)
    {
        _mutex = mutex;
        _show = show;
    }

    /// <summary>
    /// Devuelve la instancia si este proceso es el primero. Si ya hay otro, le pide que muestre su ventana y
    /// devuelve <c>null</c>: quien llama debe terminar. Si los objetos con nombre no se pueden crear (permisos),
    /// <paramref name="otherRunning"/> queda en <c>false</c> y la app arranca sin instancia única, como antes.
    /// </summary>
    public static SingleInstance? TryAcquire(out bool otherRunning)
    {
        otherRunning = false;
        Mutex? mutex = null;
        try
        {
            mutex = new Mutex(initiallyOwned: false, MutexName, out var createdNew);
            if (createdNew)
                return new SingleInstance(mutex, new EventWaitHandle(false, EventResetMode.AutoReset, EventName));

            otherRunning = true;
            mutex.Dispose();
            SignalFirstInstance();
            return null;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.IO.IOException or WaitHandleCannotBeOpenedException)
        {
            mutex?.Dispose();
            return null;
        }
    }

    // La primera crea el evento justo después del mutex: si aún no existe, se reintenta un momento y se desiste.
    // Si la primera está colgada o arrancando, el aviso queda pendiente y esta instancia termina igual.
    private static void SignalFirstInstance()
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                using var show = EventWaitHandle.OpenExisting(EventName);
                show.Set();
                return;
            }
            catch (WaitHandleCannotBeOpenedException) { Thread.Sleep(100); }
            catch (UnauthorizedAccessException) { return; }
        }
    }

    /// <summary>
    /// Escucha en un hilo de fondo los avisos de otras instancias. <paramref name="onShowRequested"/> se llama en
    /// ese hilo: quien lo recibe debe pasar al hilo de la interfaz.
    /// </summary>
    public void Listen(Action onShowRequested)
    {
        var thread = new Thread(() =>
        {
            while (true)
            {
                try { _show.WaitOne(); }
                catch (ObjectDisposedException) { return; }
                if (_disposed) return;
                onShowRequested();
            }
        })
        { IsBackground = true, Name = "Instancia única" };
        thread.Start();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _show.Set(); } catch (ObjectDisposedException) { } // despierta al hilo para que termine
        _show.Dispose();
        _mutex.Dispose();
    }
}
