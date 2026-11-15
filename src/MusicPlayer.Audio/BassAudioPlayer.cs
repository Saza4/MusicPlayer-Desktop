using ManagedBass;
using ManagedBass.Mix;
using MusicPlayer.Core.Interfaces;
using CorePlaybackState = MusicPlayer.Core.Interfaces.PlaybackState;

namespace MusicPlayer.Audio;

/// <summary>
/// Reproduce a través de un mezclador (BASSmix) que no se detiene entre pistas: la siguiente se añade
/// como fuente justo cuando la actual termina de mezclarse, así no hay silencio entre ambas.
/// </summary>
public sealed class BassAudioPlayer : IAudioPlayer
{
    private sealed record Source(int Stream, string FilePath, TimeSpan Duration);

    private static readonly string[] PluginFiles = OperatingSystem.IsWindows()
        ? new[] { "bassflac.dll", "bass_aac.dll" }
        : new[] { "libbassflac.so", "libbass_aac.so" };

    // Referencias vivas: si el GC(Recolector de Basura) recoge un delegado que BASS aún usa, el proceso se cae.
    private readonly SyncProcedure _mixedEndSync;
    private readonly SyncProcedure _heardEndSync;
    private readonly object _stateLock = new();

    // Protege los cambios de _mixer, _current, _chained y _next entre el hilo de la interfaz y los callbacks de BASS
    // (docs/specs/audit-3.md, H2). Dentro solo se cambian campos y se disparan los avisos de fin de pista (quien
    // los escucha solo encola trabajo): nunca se llama a BASS para liberar, porque BASS espera a que termine el
    // callback en curso y ese callback podría estar esperando este candado.
    private readonly object _sync = new();

    private volatile int _mixer;
    private volatile Source? _current;   // la pista que se oye ahora
    private Source? _chained;            // ya está en el mezclador, pero todavía no se oye
    private Source? _next;               // preparada, aún fuera del mezclador
    private volatile string? _lastPath;  // para volver a abrirla si terminó sola y se pulsa Reproducir

    private double _volume = 1.0;
    private CorePlaybackState _state = CorePlaybackState.Stopped;
    private bool _disposed;

    public BassAudioPlayer()
    {
        _mixedEndSync = OnMixedEnd;
        _heardEndSync = OnHeardEnd;

        try { _ = BassMix.Version; }
        catch (DllNotFoundException ex)
        {
            throw new InvalidOperationException(
                "Falta bassmix.dll / libbassmix.so. Colócalo en src/MusicPlayer.Audio/native (ver README).", ex);
        }

        if (!Bass.Init() && Bass.LastError != Errors.Already)
            throw new InvalidOperationException($"No se pudo inicializar BASS: {Bass.LastError}");

        LoadPlugins();
    }

    public CorePlaybackState State => _state;
    public TimeSpan Duration => _current?.Duration ?? TimeSpan.Zero;

    public TimeSpan CurrentTime
    {
        get
        {
            var current = _current;
            if (current is null) return TimeSpan.Zero;

            // Posición "oída": descuenta lo que el mezclador tiene almacenado.
            var bytes = BassMix.ChannelGetPosition(current.Stream);
            return bytes < 0
                ? TimeSpan.Zero
                : TimeSpan.FromSeconds(Bass.ChannelBytes2Seconds(current.Stream, bytes));
        }
    }

    public double Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0.0, 1.0);
            if (_mixer != 0)
                Bass.ChannelSetAttribute(_mixer, ChannelAttribute.Volume, _volume);
        }
    }

    public event EventHandler<CorePlaybackState>? StateChanged;
    public event EventHandler? TrackEnded;
    public event EventHandler<string>? AdvancedToNext;

    public void Load(string filePath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Release();

        var source = Open(filePath);
        if (source is null)
            throw new InvalidOperationException($"No se pudo abrir '{filePath}': {Bass.LastError}");

        // El mezclador toma la frecuencia de la pista: ésta suena sin remuestrear.
        var mixer = BassMix.CreateMixerStream(Bass.ChannelGetInfo(source.Stream).Frequency, 2, BassFlags.Float);
        if (mixer == 0)
        {
            var error = Bass.LastError;
            Bass.StreamFree(source.Stream);
            throw new InvalidOperationException($"No se pudo crear el mezclador: {error}");
        }

        // AutoFree: BASS libera la fuente cuando termina o cuando se libera el mezclador.
        if (!BassMix.MixerAddChannel(mixer, source.Stream, BassFlags.AutoFree))
        {
            var error = Bass.LastError;
            Bass.StreamFree(source.Stream);
            Bass.StreamFree(mixer);
            throw new InvalidOperationException($"No se pudo añadir '{filePath}' al mezclador: {error}");
        }

        Bass.ChannelSetAttribute(mixer, ChannelAttribute.Volume, _volume);

        lock (_sync)
        {
            _mixer = mixer;
            _current = source;
            _lastPath = filePath;
        }
        Watch(source.Stream);

        SetState(CorePlaybackState.Stopped);
    }

    public void PrepareNext(string? filePath)
    {
        if (_disposed) return;

        if (filePath is null)
        {
            // Cancelar suelta todo lo preparado, también lo que ya entró al mezclador: quien lo pide va a
            // escribir o borrar ese archivo. Al terminar la actual se dispara TrackEnded y la cola sigue
            // por el camino normal (docs/specs/playback-resilience.md, criterio 4).
            Source? pending, chained;
            lock (_sync)
            {
                pending = _next;
                _next = null;
                chained = _chained;
                _chained = null;
            }

            Discard(pending);
            if (chained is not null)
            {
                BassMix.MixerRemoveChannel(chained.Stream);
                Bass.StreamFree(chained.Stream);
            }
            return;
        }

        // Ya hay una pista añadida al mezclador y a punto de oírse: la siguiente de esa se prepara
        // cuando llegue AdvancedToNext.
        Source? stale;
        bool hasCurrent;
        lock (_sync)
        {
            if (_chained is not null) return;
            if (_next is { } existing && string.Equals(existing.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
                return;

            stale = _next;
            _next = null;
            hasCurrent = _current is not null;
        }

        Discard(stale);
        if (!hasCurrent) return;

        // Si no se puede abrir, no pasa nada: al terminar la actual se usará el camino normal.
        var next = Open(filePath);
        if (next is null) return;

        // Mientras se abría, la actual pudo terminar (ya no hay a qué encadenarla): entonces se suelta.
        bool keep;
        lock (_sync)
        {
            keep = _current is not null && _chained is null && _next is null;
            if (keep) _next = next;
        }
        if (!keep) Discard(next);
    }

    public void Play()
    {
        if (_disposed) return;

        // Si la pista terminó sola, se vuelve a abrir desde el principio.
        if (_current is null)
        {
            if (_lastPath is null) return;
            Load(_lastPath);
        }

        if (Bass.ChannelPlay(_mixer, false))
            SetState(CorePlaybackState.Playing);
    }

    public void Pause()
    {
        if (_mixer == 0 || _state != CorePlaybackState.Playing) return;
        if (Bass.ChannelPause(_mixer))
            SetState(CorePlaybackState.Paused);
    }

    public void Unload()
    {
        Release();
        lock (_sync) _lastPath = null; // un fin de pista en curso no debe reponerla después
        SetState(CorePlaybackState.Stopped);
    }

    public void Seek(TimeSpan position)
    {
        if (_current is { } current)
            SeekCore(current, position);
    }

    // MixerReset descarta lo ya mezclado: sin él, tras saltar se oiría un instante de la posición anterior.
    private static void SeekCore(Source source, TimeSpan position)
    {
        var seconds = Math.Clamp(position.TotalSeconds, 0, source.Duration.TotalSeconds);
        BassMix.ChannelSetPosition(
            source.Stream,
            Bass.ChannelSeconds2Bytes(source.Stream, seconds),
            PositionFlags.Bytes | PositionFlags.MixerReset);
    }

    // ---- Callbacks (hilos de BASS) ----

    // Se ejecuta cuando el final de la pista actual acaba de mezclarse (todavía no se oye):
    // es el momento de añadir la siguiente para que continúe sin hueco.
    private void OnMixedEnd(int handle, int channel, int data, IntPtr user)
    {
        int mixer;
        Source? next;
        lock (_sync)
        {
            mixer = _mixer;
            if (mixer == 0 || _chained is not null) return;

            next = _next;
            if (next is null) return;
            _next = null;
            // Se publica antes de añadirla: si PrepareNext(null) o Release llegan ahora, la ven y la sueltan
            // (antes podía quedar abierta tras pedir que se soltara).
            _chained = next;
        }

        if (!BassMix.MixerAddChannel(mixer, next.Stream, BassFlags.AutoFree))
        {
            // Si entretanto alguien se la llevó, ya la liberó (por eso falló): no se libera dos veces.
            bool mine;
            lock (_sync)
            {
                mine = ReferenceEquals(_chained, next);
                if (mine) _chained = null;
            }
            if (mine) Bass.StreamFree(next.Stream);
            return;
        }

        // Los syncs de la nueva fuente se registran fuera del callback.
        Task.Run(() => Watch(next.Stream));
    }

    // Se ejecuta cuando el final de la pista ya se oyó.
    private void OnHeardEnd(int handle, int channel, int data, IntPtr user)
    {
        // El cambio de pista y su aviso van juntos dentro del candado: así Load y Unload ocurren del todo antes o
        // del todo después, y quien escucha puede saber si el aviso es anterior a su última orden.
        lock (_sync)
        {
            var current = _current;
            if (current is null || channel != current.Stream) return;

            var chained = _chained;
            if (chained is not null)
            {
                _chained = null;
                _current = chained;
                _lastPath = chained.FilePath;
                AdvancedToNext?.Invoke(this, chained.FilePath);
                return;
            }

            // No había siguiente preparada: la reproducción terminó de verdad.
            _lastPath = current.FilePath;
            _current = null;
            SetState(CorePlaybackState.Stopped);
            TrackEnded?.Invoke(this, EventArgs.Empty);
        }
    }

    // ---- Internos ----

    private static Source? Open(string filePath)
    {
        // Decodificación pura: el mezclador es quien reproduce.
        var stream = Bass.CreateStream(filePath, 0, 0, BassFlags.Decode | BassFlags.Float);
        if (stream == 0) return null;

        var seconds = Bass.ChannelBytes2Seconds(stream, Bass.ChannelGetLength(stream));
        return new Source(stream, filePath, TimeSpan.FromSeconds(seconds));
    }

    private void Watch(int source)
    {
        BassMix.ChannelSetSync(source, SyncFlags.End | SyncFlags.Mixtime, 0, _mixedEndSync);
        BassMix.ChannelSetSync(source, SyncFlags.End, 0, _heardEndSync);
    }

    private static void Discard(Source? source)
    {
        if (source is not null)
            Bass.StreamFree(source.Stream);
    }

    private void Release()
    {
        int mixer;
        Source? current, chained, next;
        lock (_sync)
        {
            mixer = _mixer;
            _mixer = 0;
            current = _current;
            _current = null;
            chained = _chained;
            _chained = null;
            next = _next;
            _next = null;
        }

        if (mixer != 0) Bass.StreamFree(mixer);

        // Las fuentes con AutoFree ya se liberaron con el mezclador; esto cubre cualquier resto
        // (y suelta el archivo para poder borrarlo en Windows).
        Discard(current);
        Discard(chained);
        Discard(next);
    }

    private void SetState(CorePlaybackState state)
    {
        lock (_stateLock)
        {
            if (_state == state) return;
            _state = state;
        }
        StateChanged?.Invoke(this, state);
    }

    private static void LoadPlugins()
    {
        foreach (var file in PluginFiles)
        {
            var path = Path.Combine(AppContext.BaseDirectory, file);
            // Un plugin ausente no es fatal: solo se pierde ese formato.
            if (File.Exists(path))
                Bass.PluginLoad(path);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Release();
        Bass.Free();
    }
}