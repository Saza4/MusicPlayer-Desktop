using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;

namespace MusicPlayer.Infrastructure;

public sealed class JsonSettingsRepository(string filePath) : ISettingsRepository
{
    public async Task<AppSettings> LoadAsync(CancellationToken ct = default) =>
        await AtomicJson.ReadAsync<AppSettings>(filePath, ct) ?? AppSettings.Default;

    public Task SaveAsync(AppSettings settings, CancellationToken ct = default) =>
        AtomicJson.WriteAsync(filePath, settings, ct);
}