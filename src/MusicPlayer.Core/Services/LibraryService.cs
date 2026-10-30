using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Services;

public sealed class LibraryService(LibraryScanner scanner, ILibraryRepository repository)
{
    private LibrarySnapshot _current = LibrarySnapshot.Empty;

    public LibrarySnapshot Current => _current;

    public async Task<LibrarySnapshot> LoadAsync(CancellationToken ct = default)
    {
        _current = await repository.LoadAsync(ct);
        return _current;
    }

    public async Task<LibrarySnapshot> AddFolderAsync(
    string folder, IProgress<int>? progress = null, CancellationToken ct = default) =>
    (await ScanFolderAsync(folder, progress, ct)).Snapshot;

    public async Task<AddFolderResult> ScanFolderAsync(
        string folder, IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var found = await scanner.ScanAsync(folder, progress, ct);

        var byPath = _current.Tracks
            .DistinctBy(t => t.FilePath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(t => t.FilePath, StringComparer.OrdinalIgnoreCase);

        // Rutas que la biblioteca aún no conocía: candidatas a ser pistas movidas.
        var fresh = found.Where(t => !byPath.ContainsKey(t.FilePath)).ToList();
        var missing = fresh.Count == 0
            ? new List<Track>()
            : await Task.Run(() => byPath.Values.Where(t => !File.Exists(t.FilePath)).ToList(), ct);

        foreach (var track in found)
        {
            byPath[track.FilePath] = byPath.TryGetValue(track.FilePath, out var known)
                ? track with { Id = known.Id, AddedAt = known.AddedAt }
                : track;
        }

        var relocated = 0;
        foreach (var (oldTrack, movedTrack) in TrackRelocator.Match(missing, fresh))
        {
            byPath.Remove(oldTrack.FilePath);
            byPath[movedTrack.FilePath] = movedTrack with { Id = oldTrack.Id, AddedAt = oldTrack.AddedAt };
            relocated++;
        }

        var folders = _current.Folders.Contains(folder, StringComparer.OrdinalIgnoreCase)
            ? _current.Folders
            : _current.Folders.Append(folder).ToList();

        _current = new LibrarySnapshot(TrackSorter.Sort(byPath.Values), folders);
        await repository.SaveAsync(_current, ct);
        return new AddFolderResult(_current, relocated);
    }

    public async Task<RemoveResult> RemoveTracksAsync(IEnumerable<Guid> ids, bool deleteFiles)
    {
        var wanted = ids.ToHashSet();
        var targets = _current.Tracks.Where(t => wanted.Contains(t.Id)).ToList();
        var removed = new List<Track>();
        var errors = new List<string>();

        if (targets.Count == 0)
            return new RemoveResult(_current, removed, errors);

        await Task.Run(() =>
        {
            foreach (var track in targets)
            {
                if (deleteFiles)
                {
                    try { File.Delete(track.FilePath); } // no lanza si el archivo ya no existe
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        errors.Add($"{track.Title}: {ex.Message}");
                        continue; // sigue en la biblioteca: el archivo no se pudo borrar
                    }
                }
                removed.Add(track);
            }
        });

        if (removed.Count == 0)
            return new RemoveResult(_current, removed, errors);

        var gone = removed.Select(t => t.Id).ToHashSet();
        _current = _current with { Tracks = _current.Tracks.Where(t => !gone.Contains(t.Id)).ToList() };
        await repository.SaveAsync(_current);

        return new RemoveResult(_current, removed, errors);
    }

    /// <summary>Pistas de la biblioteca cuyo archivo ya no existe. No modifica nada.</summary>
    public Task<IReadOnlyList<Track>> FindMissingAsync(CancellationToken ct = default)
    {
        var tracks = _current.Tracks; // el snapshot es inmutable: se captura antes de salir del hilo
        return Task.Run<IReadOnlyList<Track>>(
            () => tracks.Where(t => !File.Exists(t.FilePath)).ToList(), ct);
    }

    /// <summary>Quita de la biblioteca (no del disco) las pistas cuyo archivo ya no existe.</summary>
    public async Task<RemoveResult> PruneMissingAsync(CancellationToken ct = default)
    {
        var missing = await FindMissingAsync(ct);
        return await RemoveTracksAsync(missing.Select(t => t.Id), deleteFiles: false);
    }

    /// <summary>Aplica las etiquetas a una pista de la biblioteca, conservando su Id, y reordena.</summary>
    public async Task<LibrarySnapshot> ApplyTagsAsync(Guid trackId, TrackTags tags, CancellationToken ct = default)
    {
        if (!_current.Tracks.Any(t => t.Id == trackId))
            return _current;

        var updated = _current.Tracks.Select(t => t.Id == trackId ? tags.ApplyTo(t) : t);
        _current = _current with { Tracks = TrackSorter.Sort(updated) };
        await repository.SaveAsync(_current, ct);
        return _current;
    }
}