using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Interfaces;

public interface IPlaylistRepository
{
    Task<IReadOnlyList<Playlist>> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(IReadOnlyList<Playlist> playlists, CancellationToken ct = default);
}