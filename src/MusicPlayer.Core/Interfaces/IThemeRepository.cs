using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Interfaces;

public interface IThemeRepository
{
    /// <summary>Lee los temas del usuario. No lanza: los archivos rotos salen en <see cref="ThemeLoadReport.Errors"/>.</summary>
    Task<ThemeLoadReport> LoadAllAsync(CancellationToken ct = default);
}
