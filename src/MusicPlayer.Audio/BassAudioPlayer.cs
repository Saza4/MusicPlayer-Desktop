using System.IO;
using ManagedBass;
using MusicPlayer.Core.Interfaces;
using CorePlaybackState = MusicPlayer.Core.Interfaces.PlaybackState;

namespace MusicPlayer.Audio;

public sealed class BassAudioPlayer : IAudioPlayer
{
    private static readonly string[] PluginFiles = OperatingSystem.IsWindows()
        ? new[] { "bassflac.dll", "bass_aac.dll" }
        : new[] { "libbassflac.so", "libbass_aac.so" };

    /// <summary>Plugins cargados con éxito (útil para diagnóstico).</summary>
    public IReadOnlyList<string> LoadedPlugins { get; }

    private static List<string> LoadPlugins()
    {
        var loaded = new List<string>();
        foreach (var file in PluginFiles)
        {
            var path = Path.Combine(AppContext.BaseDirectory, file);
            // Un plugin ausente no es fatal: solo se pierde ese formato.
            if (File.Exists(path) && Bass.PluginLoad(path) != 0)
                loaded.Add(file);
        }
        return loaded;
    }

    private int _stream;
    private double _volume = 1.0;
    private TimeSpan _duration;
    private CorePlaybackState _state = CorePlaybackState.Stopped;
    private SyncProcedure? _endSync; // referencia viva: si el GC la recoge, BASS crashea
    private bool _disposed;

    public BassAudioPlayer()
    {
        if (!Bass.Init() && Bass.LastError != Errors.Already)
            throw new InvalidOperationException($"No se pudo inicializar BASS: {Bass.LastError}");

        LoadedPlugins = LoadPlugins();
    }

    public CorePlaybackState State => _state;
    public TimeSpan Duration => _duration;

    public TimeSpan CurrentTime => _stream == 0
        ? TimeSpan.Zero
        : TimeSpan.FromSeconds(Bass.ChannelBytes2Seconds(_stream, Bass.ChannelGetPosition(_stream)));

    public double Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0.0, 1.0);
            if (_stream != 0)
                Bass.ChannelSetAttribute(_stream, ChannelAttribute.Volume, _volume);
        }
    }

    public event EventHandler<CorePlaybackState>? StateChanged;
    public event EventHandler? TrackEnded;

    public void Load(string filePath)
    {
        ThrowIfDisposed();
        ReleaseStream();

        // BassFlags.Prescan daría seek más exacto en MP3 VBR, a costa de más tiempo de carga.
        _stream = Bass.CreateStream(filePath);
        if (_stream == 0)
            throw new InvalidOperationException($"No se pudo abrir '{filePath}': {Bass.LastError}");

        _duration = TimeSpan.FromSeconds(
            Bass.ChannelBytes2Seconds(_stream, Bass.ChannelGetLength(_stream)));

        Bass.ChannelSetAttribute(_stream, ChannelAttribute.Volume, _volume);

        _endSync = OnStreamEnded;
        Bass.ChannelSetSync(_stream, SyncFlags.End, 0, _endSync);

        SetState(CorePlaybackState.Stopped);
    }

    public void Play()
    {
        if (_stream == 0) return;
        // Si estaba detenida (o terminó), reinicia desde el principio.
        if (Bass.ChannelPlay(_stream, _state == CorePlaybackState.Stopped))
            SetState(CorePlaybackState.Playing);
    }

    public void Pause()
    {
        if (_stream == 0 || _state != CorePlaybackState.Playing) return;
        if (Bass.ChannelPause(_stream))
            SetState(CorePlaybackState.Paused);
    }

    public void Stop()
    {
        if (_stream == 0) return;
        Bass.ChannelStop(_stream);
        Bass.ChannelSetPosition(_stream, 0);
        SetState(CorePlaybackState.Stopped);
    }

    public void Unload()
    {
        ReleaseStream();
        _duration = TimeSpan.Zero;
        SetState(CorePlaybackState.Stopped);
    }

    public void Seek(TimeSpan position)
    {
        if (_stream == 0) return;
        var clamped = TimeSpan.FromSeconds(Math.Clamp(position.TotalSeconds, 0, _duration.TotalSeconds));
        Bass.ChannelSetPosition(_stream, Bass.ChannelSeconds2Bytes(_stream, clamped.TotalSeconds));
    }

    // Se ejecuta en un hilo de BASS, no en el de la UI.
    private void OnStreamEnded(int handle, int channel, int data, IntPtr user)
    {
        SetState(CorePlaybackState.Stopped);
        TrackEnded?.Invoke(this, EventArgs.Empty);
    }

    private void SetState(CorePlaybackState state)
    {
        if (_state == state) return;
        _state = state;
        StateChanged?.Invoke(this, state);
    }

    private void ReleaseStream()
    {
        if (_stream == 0) return;
        Bass.StreamFree(_stream);
        _stream = 0;
        _endSync = null;
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ReleaseStream();
        Bass.Free();
    }
}