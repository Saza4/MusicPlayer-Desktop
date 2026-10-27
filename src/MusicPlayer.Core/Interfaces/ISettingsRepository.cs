using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Interfaces;

public interface ISettingsRepository
{
    /// <summary>Devuelve <see cref="AppSettings.Default"/> si no hay archivo o está corrupto.</summary>
    Task<AppSettings> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(AppSettings settings, CancellationToken ct = default);
}