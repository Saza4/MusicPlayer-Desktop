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
        string folder, IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var found = await scanner.ScanAsync(folder, progress, ct);

        var byPath = _current.Tracks
            .DistinctBy(t => t.FilePath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(t => t.FilePath, StringComparer.OrdinalIgnoreCase);

        foreach (var track in found)
        {
            byPath[track.FilePath] = byPath.TryGetValue(track.FilePath, out var old)
                ? track with { Id = old.Id, AddedAt = old.AddedAt }
                : track;
        }

        var folders = _current.Folders.Contains(folder, StringComparer.OrdinalIgnoreCase)
            ? _current.Folders
            : _current.Folders.Append(folder).ToList();

        _current = new LibrarySnapshot(TrackSorter.Sort(byPath.Values), folders);
        await repository.SaveAsync(_current, ct);
        return _current;
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
}