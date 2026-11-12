using System.Text.Json;
using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;

namespace MusicPlayer.Infrastructure;

public sealed class JsonLibraryRepository(string filePath) : ILibraryRepository
{
    private const int CurrentVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    internal sealed record LibraryFile(int Version, List<TrackRecord>? Tracks);

    internal sealed record TrackRecord(
        Guid Id, string FilePath, string Title, string Artist, string Album,
        uint Year, string? Genre, uint TrackNumber, long DurationTicks, DateTimeOffset AddedAt, uint DiscNumber = 0, string? AlbumArtist = null);

    public async Task<LibrarySnapshot> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
            return LibrarySnapshot.Empty;

        try
        {
            await using var stream = File.OpenRead(filePath);
            var file = await JsonSerializer.DeserializeAsync<LibraryFile>(stream, JsonOptions, ct);
            if (file is null)
                return LibrarySnapshot.Empty;

            var tracks = (file.Tracks ?? new()).Select(r => new Track
            {
                Id = r.Id,
                FilePath = r.FilePath,
                Title = r.Title,
                Artist = r.Artist,
                Album = r.Album,
                Year = r.Year,
                Genre = r.Genre,
                TrackNumber = r.TrackNumber,
                Duration = TimeSpan.FromTicks(r.DurationTicks),
                AddedAt = r.AddedAt,
                DiscNumber = r.DiscNumber,
                AlbumArtist = r.AlbumArtist ?? ""
            }).ToList();

            return new LibrarySnapshot(tracks);
        }
        catch (JsonException)
        {
            // Archivo corrupto: se conserva como respaldo (con fecha, sin pisar los anteriores) y se empieza de cero.
            CorruptBackup.Move(filePath);
            return LibrarySnapshot.Empty;
        }
    }

    public async Task SaveAsync(LibrarySnapshot snapshot, CancellationToken ct = default)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(filePath))!;
        Directory.CreateDirectory(directory);

        var file = new LibraryFile(
            CurrentVersion,
            snapshot.Tracks.Select(t => new TrackRecord(
                t.Id, t.FilePath, t.Title, t.Artist, t.Album, t.Year,
                t.Genre, t.TrackNumber, t.Duration.Ticks, t.AddedAt, t.DiscNumber, t.AlbumArtist)).ToList());

        // Las escrituras del mismo archivo van en fila: en Windows, dos File.Move con sobrescritura a la vez
        // chocan (docs/specs/library-data-safety-2.md, criterio 3). El temporal es único por escritura.
        var gate = AtomicJson.GateFor(filePath);
        await gate.WaitAsync(ct);
        try
        {
            var tempPath = $"{filePath}.{Guid.NewGuid():N}.tmp";
            try
            {
                await using (var stream = File.Create(tempPath))
                    await JsonSerializer.SerializeAsync(stream, file, JsonOptions, ct);

                File.Move(tempPath, filePath, overwrite: true);
            }
            finally
            {
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch (IOException) { }
            }
        }
        finally
        {
            gate.Release();
        }
    }
}