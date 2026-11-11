using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Services;

public sealed class LibraryService(LibraryScanner scanner, ILibraryRepository repository)
{
    private LibrarySnapshot _current = LibrarySnapshot.Empty;
    private readonly SemaphoreSlim _gate = new(1, 1); // serializa lo que lee _current y lo reasigna
    private bool _loadFailed; // la última carga falló: guardar sobrescribiría datos que no se pudieron leer

    public LibrarySnapshot Current => _current;

    public async Task<LibrarySnapshot> LoadAsync(CancellationToken ct = default)
    {
        try
        {
            _current = await repository.LoadAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _loadFailed = true;
            throw;
        }
        _loadFailed = false;
        return _current;
    }

    private void EnsureWritable()
    {
        if (_loadFailed)
            throw new InvalidOperationException(
                "La biblioteca no se pudo cargar al arrancar, así que no se guarda nada para no sobrescribirla. Reinicia la app.");
    }

    // Una operación a la vez sobre _current, y ninguna si la carga inicial falló.
    private async Task<T> ExclusiveAsync<T>(Func<Task<T>> work, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            EnsureWritable();
            return await work();
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task<AddFolderResult> ScanFolderAsync(
        string folder, IProgress<int>? progress = null, CancellationToken ct = default) =>
        ExclusiveAsync(() => ScanFolderCoreAsync(folder, progress, ct), ct);

    private async Task<AddFolderResult> ScanFolderCoreAsync(
        string folder, IProgress<int>? progress, CancellationToken ct)
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

        _current = new LibrarySnapshot(TrackSorter.Sort(byPath.Values));
        await repository.SaveAsync(_current, ct);
        return new AddFolderResult(_current, relocated);
    }

    public Task<RemoveResult> RemoveTracksAsync(IEnumerable<Guid> ids, bool deleteFiles) =>
        ExclusiveAsync(() => RemoveTracksCoreAsync(ids, deleteFiles));

    private async Task<RemoveResult> RemoveTracksCoreAsync(IEnumerable<Guid> ids, bool deleteFiles)
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
    public Task<LibrarySnapshot> ApplyTagsAsync(Guid trackId, TrackTags tags, CancellationToken ct = default) =>
        ExclusiveAsync(() => ApplyTagsCoreAsync(trackId, tags, ct), ct);

    private async Task<LibrarySnapshot> ApplyTagsCoreAsync(Guid trackId, TrackTags tags, CancellationToken ct)
    {
        if (!_current.Tracks.Any(t => t.Id == trackId))
            return _current;

        var updated = _current.Tracks.Select(t => t.Id == trackId ? tags.ApplyTo(t) : t);
        _current = _current with { Tracks = TrackSorter.Sort(updated) };
        await repository.SaveAsync(_current, ct);
        return _current;
    }

    /// <summary>Aplica etiquetas a varias pistas con un único guardado y reordena.</summary>
    public Task<LibrarySnapshot> ApplyTagsBatchAsync(
        IReadOnlyDictionary<Guid, TrackTags> changes, CancellationToken ct = default) =>
        ExclusiveAsync(() => ApplyTagsBatchCoreAsync(changes, ct), ct);

    private async Task<LibrarySnapshot> ApplyTagsBatchCoreAsync(
        IReadOnlyDictionary<Guid, TrackTags> changes, CancellationToken ct)
    {
        if (changes.Count == 0)
            return _current;

        var updated = _current.Tracks.Select(t => changes.TryGetValue(t.Id, out var tags) ? tags.ApplyTo(t) : t);
        _current = _current with { Tracks = TrackSorter.Sort(updated) };
        await repository.SaveAsync(_current, ct);
        return _current;
    }

    /// <summary>Agrega archivos concretos (por ejemplo, los recién convertidos), conservando el Id de los ya conocidos.</summary>
    public Task<LibrarySnapshot> AddFilesAsync(IEnumerable<string> paths, CancellationToken ct = default) =>
        ExclusiveAsync(() => AddFilesCoreAsync(paths, ct), ct);

    private async Task<LibrarySnapshot> AddFilesCoreAsync(IEnumerable<string> paths, CancellationToken ct)
    {
        var found = await scanner.ReadFilesAsync(paths, ct);
        if (found.Count == 0)
            return _current;

        var byPath = _current.Tracks
            .DistinctBy(t => t.FilePath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(t => t.FilePath, StringComparer.OrdinalIgnoreCase);

        foreach (var track in found)
            byPath[track.FilePath] = byPath.TryGetValue(track.FilePath, out var known)
                ? track with { Id = known.Id, AddedAt = known.AddedAt }
                : track;

        _current = _current with { Tracks = TrackSorter.Sort(byPath.Values) };
        await repository.SaveAsync(_current, ct);
        return _current;
    }
}