using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Services;

/// <summary>Mantiene los ajustes en memoria y los guarda con retraso (debounce).</summary>
public sealed class SettingsService(ISettingsRepository repository, TimeSpan? saveDelay = null)
{
    private readonly TimeSpan _delay = saveDelay ?? TimeSpan.FromMilliseconds(500);
    private readonly object _lock = new();
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private CancellationTokenSource? _pending;
    private bool _dirty;
    private bool _loadFailed; // la última carga falló: guardar sobrescribiría ajustes que no se pudieron leer

    public AppSettings Current { get; private set; } = AppSettings.Default;

    public async Task<AppSettings> LoadAsync(CancellationToken ct = default)
    {
        AppSettings loaded;
        try
        {
            loaded = await repository.LoadAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Como la biblioteca y las playlists: lo que no se pudo leer no se sobrescribe. Los cambios de la
            // sesión valen en memoria y no se guardan (docs/specs/audit-followup.md, criterio 5).
            lock (_lock) _loadFailed = true;
            throw;
        }

        lock (_lock)
        {
            Current = loaded;
            _loadFailed = false;
        }
        return loaded;
    }

    /// <summary>Aplica un cambio y programa el guardado. Si el valor no cambia, no hace nada.</summary>
    public void Update(Func<AppSettings, AppSettings> change)
    {
        CancellationToken token;
        lock (_lock)
        {
            var updated = change(Current);
            if (updated == Current) return;

            Current = updated;
            _dirty = true;
            _pending?.Cancel(); // reinicia la cuenta atrás
            _pending = new CancellationTokenSource();
            token = _pending.Token;
        }

        _ = SaveAfterDelayAsync(token);
    }

    /// <summary>Guarda de inmediato lo pendiente (úsalo al cerrar la app).</summary>
    public Task FlushAsync()
    {
        lock (_lock)
        {
            _pending?.Cancel();
            _pending = null;
        }
        return SaveNowAsync();
    }

    private async Task SaveAfterDelayAsync(CancellationToken ct)
    {
        try { await Task.Delay(_delay, ct); }
        catch (OperationCanceledException) { return; }

        await SaveNowAsync();
    }

    private async Task SaveNowAsync()
    {
        await _saveGate.WaitAsync();
        try
        {
            AppSettings snapshot;
            lock (_lock)
            {
                if (!_dirty || _loadFailed) return;
                _dirty = false;
                snapshot = Current;
            }

            await repository.SaveAsync(snapshot);
        }
        catch (Exception)
        {
            // Los ajustes no son críticos: se reintentará en el próximo cambio o al cerrar.
            lock (_lock) _dirty = true;
        }
        finally
        {
            _saveGate.Release();
        }
    }
}