using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Services;

public sealed class PlaylistManager(IPlaylistRepository repository)
{
    private IReadOnlyList<Playlist> _playlists = Array.Empty<Playlist>();

    public IReadOnlyList<Playlist> Playlists => _playlists;

    public Playlist? Find(Guid id) => _playlists.FirstOrDefault(p => p.Id == id);

    public async Task<IReadOnlyList<Playlist>> LoadAsync(CancellationToken ct = default)
    {
        _playlists = await repository.LoadAsync(ct);
        return _playlists;
    }

    public async Task<Playlist> CreateAsync(string name, CancellationToken ct = default)
    {
        var playlist = new Playlist { Name = ValidateName(name, ignoreId: null) };
        await CommitAsync(_playlists.Append(playlist).ToList(), ct);
        return playlist;
    }

    public async Task<Playlist> RenameAsync(Guid id, string newName, CancellationToken ct = default)
    {
        var renamed = Get(id) with { Name = ValidateName(newName, ignoreId: id) };
        await CommitAsync(Replace(renamed), ct);
        return renamed;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        Get(id);
        await CommitAsync(_playlists.Where(p => p.Id != id).ToList(), ct);
    }

    /// <summary>Añade al final las pistas que aún no estén. Devuelve cuántas se añadieron.</summary>
    public async Task<int> AddTracksAsync(Guid id, IEnumerable<Guid> trackIds, CancellationToken ct = default)
    {
        var playlist = Get(id);
        var present = playlist.TrackIds.ToHashSet();
        var toAdd = new List<Guid>();
        foreach (var trackId in trackIds)
            if (present.Add(trackId))
                toAdd.Add(trackId);

        if (toAdd.Count == 0) return 0;

        await CommitAsync(Replace(playlist with { TrackIds = playlist.TrackIds.Concat(toAdd).ToList() }), ct);
        return toAdd.Count;
    }

    public async Task RemoveTrackAsync(Guid id, Guid trackId, CancellationToken ct = default)
    {
        var playlist = Get(id);
        if (!playlist.TrackIds.Contains(trackId)) return;

        await CommitAsync(
            Replace(playlist with { TrackIds = playlist.TrackIds.Where(t => t != trackId).ToList() }), ct);
    }

    /// <summary>Elementos de la playlist en su orden, omitiendo los que ya no están en la biblioteca.</summary>
    public static IReadOnlyList<T> Resolve<T>(Playlist playlist, IEnumerable<T> items, Func<T, Guid> idOf)
    {
        var byId = new Dictionary<Guid, T>();
        foreach (var item in items)
            byId.TryAdd(idOf(item), item);

        var result = new List<T>(playlist.TrackIds.Count);
        foreach (var id in playlist.TrackIds)
            if (byId.TryGetValue(id, out var item))
                result.Add(item);
        return result;
    }

    private Playlist Get(Guid id) =>
        Find(id) ?? throw new KeyNotFoundException("La playlist no existe.");

    private List<Playlist> Replace(Playlist updated) =>
        _playlists.Select(p => p.Id == updated.Id ? updated : p).ToList();

    private string ValidateName(string? name, Guid? ignoreId)
    {
        var clean = (name ?? "").Trim();

        if (clean.Length == 0)
            throw new ArgumentException("El nombre no puede estar vacío.");

        if (_playlists.Any(p => p.Id != ignoreId &&
                                string.Equals(p.Name, clean, StringComparison.InvariantCultureIgnoreCase)))
            throw new InvalidOperationException("Ya existe una playlist con ese nombre.");

        return clean;
    }

    private async Task CommitAsync(IReadOnlyList<Playlist> updated, CancellationToken ct)
    {
        await repository.SaveAsync(updated, ct);
        _playlists = updated;
    }
}