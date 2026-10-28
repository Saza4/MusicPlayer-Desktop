using System.Text.Json;
using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;

namespace MusicPlayer.Infrastructure;

public sealed class JsonLibraryRepository(string filePath) : ILibraryRepository
{
    private const int CurrentVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>%APPDATA%\MusicPlayer en Windows, ~/.config/MusicPlayer en Linux.</summary>
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MusicPlayer", "library.json");

    internal sealed record LibraryFile(int Version, List<string>? Folders, List<TrackRecord>? Tracks);

    internal sealed record TrackRecord(
        Guid Id, string FilePath, string Title, string Artist, string Album,
        uint Year, string? Genre, uint TrackNumber, long DurationTicks, DateTimeOffset AddedAt, uint DiscNumber = 0);

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
                DiscNumber = r.DiscNumber
            }).ToList();

            return new LibrarySnapshot(tracks, file.Folders ?? new());
        }
        catch (JsonException)
        {
            // Archivo corrupto: se conserva como respaldo y se empieza de cero.
            File.Move(filePath, filePath + ".corrupt", overwrite: true);
            return LibrarySnapshot.Empty;
        }
    }

    public async Task SaveAsync(LibrarySnapshot snapshot, CancellationToken ct = default)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(filePath))!;
        Directory.CreateDirectory(directory);

        var file = new LibraryFile(
            CurrentVersion,
            snapshot.Folders.ToList(),
            snapshot.Tracks.Select(t => new TrackRecord(
                t.Id, t.FilePath, t.Title, t.Artist, t.Album, t.Year,
                t.Genre, t.TrackNumber, t.Duration.Ticks, t.AddedAt, t.DiscNumber)).ToList());

        var tempPath = filePath + ".tmp";
        await using (var stream = File.Create(tempPath))
            await JsonSerializer.SerializeAsync(stream, file, JsonOptions, ct);

        File.Move(tempPath, filePath, overwrite: true);
    }
}