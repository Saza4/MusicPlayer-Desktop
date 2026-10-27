using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;

namespace MusicPlayer.Infrastructure;

public sealed class JsonPlaylistRepository(string filePath) : IPlaylistRepository
{
    private const int CurrentVersion = 1;

    internal sealed record PlaylistFile(int Version, List<PlaylistRecord>? Playlists);

    internal sealed record PlaylistRecord(Guid Id, string Name, List<Guid>? TrackIds, DateTimeOffset CreatedAt);

    public async Task<IReadOnlyList<Playlist>> LoadAsync(CancellationToken ct = default)
    {
        var file = await AtomicJson.ReadAsync<PlaylistFile>(filePath, ct);
        if (file?.Playlists is null)
            return Array.Empty<Playlist>();

        return file.Playlists.Select(r => new Playlist
        {
            Id = r.Id,
            Name = string.IsNullOrWhiteSpace(r.Name) ? "Sin nombre" : r.Name,
            TrackIds = r.TrackIds ?? new(),
            CreatedAt = r.CreatedAt
        }).ToList();
    }

    public Task SaveAsync(IReadOnlyList<Playlist> playlists, CancellationToken ct = default)
    {
        var file = new PlaylistFile(
            CurrentVersion,
            playlists.Select(p => new PlaylistRecord(p.Id, p.Name, p.TrackIds.ToList(), p.CreatedAt)).ToList());

        return AtomicJson.WriteAsync(filePath, file, ct);
    }
}