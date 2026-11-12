using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;

namespace MusicPlayer.Core.Tests;

public class SettingsServiceTests
{
    private sealed class InMemorySettingsRepository : ISettingsRepository
    {
        public AppSettings Stored { get; set; } = AppSettings.Default;
        public int SaveCount { get; private set; }

        public Task<AppSettings> LoadAsync(CancellationToken ct = default) => Task.FromResult(Stored);

        public Task SaveAsync(AppSettings settings, CancellationToken ct = default)
        {
            Stored = settings;
            SaveCount++;
            return Task.CompletedTask;
        }
    }

    // Un retraso largo evita que el temporizador interfiera: estas pruebas fuerzan el guardado con Flush.
    private static SettingsService NewService(InMemorySettingsRepository repo) =>
        new(repo, TimeSpan.FromMinutes(1));

    [Fact]
    public async Task LoadAsync_ReturnsStoredSettings()
    {
        var repo = new InMemorySettingsRepository { Stored = new AppSettings(Volume: 0.2) };
        var service = NewService(repo);

        await service.LoadAsync();

        Assert.Equal(0.2, service.Current.Volume);
    }

    [Fact]
    public async Task Update_ThenFlush_SavesLatestValue()
    {
        var repo = new InMemorySettingsRepository();
        var service = NewService(repo);

        service.Update(s => s with { Volume = 0.4 });
        await service.FlushAsync();

        Assert.Equal(0.4, repo.Stored.Volume);
        Assert.Equal(1, repo.SaveCount);
    }

    [Fact]
    public async Task Flush_WithoutChanges_DoesNotSave()
    {
        var repo = new InMemorySettingsRepository();

        await NewService(repo).FlushAsync();

        Assert.Equal(0, repo.SaveCount);
    }

    [Fact]
    public async Task Update_ToSameValue_DoesNotMarkAsChanged()
    {
        var repo = new InMemorySettingsRepository();
        var service = NewService(repo);

        service.Update(s => s with { Volume = AppSettings.Default.Volume });
        await service.FlushAsync();

        Assert.Equal(0, repo.SaveCount);
    }

    // Espera falsa: la prueba decide cuándo termina, así que no depende del reloj
    // (docs/specs/test-waits-and-docs.md). Se cancela como Task.Delay, con OperationCanceledException.
    private sealed class ManualDelay
    {
        private readonly List<TaskCompletionSource> _waits = new();

        public int Started => _waits.Count;
        public int Cancelled => _waits.Count(w => w.Task.IsCanceled);

        public Task Wait(TimeSpan delay, CancellationToken ct)
        {
            // Sin RunContinuationsAsynchronously: al completarla, el guardado corre antes de que Complete vuelva.
            var wait = new TaskCompletionSource();
            ct.Register(() => wait.TrySetCanceled(ct));
            _waits.Add(wait);
            return wait.Task;
        }

        public void CompleteLast() => _waits[^1].SetResult();
    }

    [Fact]
    public async Task Update_Burst_SavesOnlyOnceAfterTheDelay()
    {
        var repo = new InMemorySettingsRepository();
        var delay = new ManualDelay();
        var service = new SettingsService(repo, TimeSpan.FromMilliseconds(250), delay.Wait);

        for (int i = 1; i <= 5; i++)
        {
            var volume = i / 10.0;
            service.Update(s => s with { Volume = volume });
        }

        Assert.Equal(5, delay.Started);
        Assert.Equal(4, delay.Cancelled); // cada cambio reinicia la cuenta atrás
        Assert.Equal(0, repo.SaveCount);  // la espera no ha terminado

        delay.CompleteLast();

        Assert.Equal(1, repo.SaveCount);
        Assert.Equal(0.5, repo.Stored.Volume);

        await service.FlushAsync(); // ya no queda nada pendiente
        Assert.Equal(1, repo.SaveCount);
    }

    private sealed class FailingOnceSaveRepository : ISettingsRepository
    {
        private bool _failed;
        public AppSettings? Stored { get; private set; }

        public Task<AppSettings> LoadAsync(CancellationToken ct = default) => Task.FromResult(AppSettings.Default);

        public Task SaveAsync(AppSettings settings, CancellationToken ct = default)
        {
            if (!_failed)
            {
                _failed = true;
                return Task.FromException(new IOException("disco lleno"));
            }
            Stored = settings;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task SaveFails_NextFlushSavesTheSameChange()
    {
        var repo = new FailingOnceSaveRepository();
        var service = new SettingsService(repo, TimeSpan.FromMinutes(1), new ManualDelay().Wait);

        service.Update(s => s with { Volume = 0.4 });
        await service.FlushAsync(); // falla y no lanza: los ajustes no son críticos
        Assert.Null(repo.Stored);

        await service.FlushAsync();

        Assert.Equal(0.4, repo.Stored?.Volume);
    }

    // Como la biblioteca y las playlists: lo que no se pudo leer no se sobrescribe
    // (docs/specs/audit-followup.md, criterio 5).
    private sealed class FailingLoadRepository : ISettingsRepository
    {
        public bool Fail { get; set; } = true;
        public AppSettings Stored { get; } = new(Volume: 0.2, ThemeId: "medianoche");
        public int SaveCount { get; private set; }

        public Task<AppSettings> LoadAsync(CancellationToken ct = default) =>
            Fail ? Task.FromException<AppSettings>(new IOException("en uso")) : Task.FromResult(Stored);

        public Task SaveAsync(AppSettings settings, CancellationToken ct = default)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task LoadFails_UpdateChangesMemoryButNeverSaves()
    {
        var repo = new FailingLoadRepository();
        var service = new SettingsService(repo, TimeSpan.FromMinutes(1));

        await Assert.ThrowsAsync<IOException>(() => service.LoadAsync());
        service.Update(s => s with { Volume = 0.7 });
        await service.FlushAsync();

        Assert.Equal(0.7, service.Current.Volume); // la sesión sigue funcionando
        Assert.Equal(0, repo.SaveCount);
    }

    [Fact]
    public async Task LoadFails_ThenLoadsFine_SavesAgain()
    {
        var repo = new FailingLoadRepository();
        var service = new SettingsService(repo, TimeSpan.FromMinutes(1));

        await Assert.ThrowsAsync<IOException>(() => service.LoadAsync());
        repo.Fail = false;
        await service.LoadAsync();
        service.Update(s => s with { Volume = 0.7 });
        await service.FlushAsync();

        Assert.Equal("medianoche", service.Current.ThemeId);
        Assert.Equal(1, repo.SaveCount);
    }
}