using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Interfaces;

public interface ILyricsProvider
{
    /// <summary>Devuelve <see cref="LyricBlock.Empty"/> si no hay letra disponible.</summary>
    Task<LyricBlock> GetLyricsAsync(Track track, CancellationToken ct = default);
}