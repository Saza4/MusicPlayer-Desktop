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

        _current = new LibrarySnapshot(Order(byPath.Values), folders);
        await repository.SaveAsync(_current, ct);
        return _current;
    }

    /// <summary>Quita de la biblioteca las pistas cuyo archivo ya no existe.</summary>
    public async Task<LibrarySnapshot> PruneMissingAsync(CancellationToken ct = default)
    {
        var existing = await Task.Run(
            () => _current.Tracks.Where(t => File.Exists(t.FilePath)).ToList(), ct);

        if (existing.Count == _current.Tracks.Count)
            return _current;

        _current = _current with { Tracks = existing };
        await repository.SaveAsync(_current, ct);
        return _current;
    }

    private static IReadOnlyList<Track> Order(IEnumerable<Track> tracks) =>
        tracks.OrderBy(t => t.Artist, StringComparer.CurrentCultureIgnoreCase)
              .ThenBy(t => t.Album, StringComparer.CurrentCultureIgnoreCase)
              .ThenBy(t => t.TrackNumber)
              .ThenBy(t => t.Title, StringComparer.CurrentCultureIgnoreCase)
              .ToList();
}