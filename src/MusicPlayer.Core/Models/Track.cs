namespace MusicPlayer.Core.Models;

public sealed record Track
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string FilePath { get; init; }
    public required string Title { get; init; }
    public string Artist { get; init; } = "Artista desconocido";
    public string Album { get; init; } = "Álbum desconocido";
    public uint Year { get; init; }
    public string? Genre { get; init; }
    public uint TrackNumber { get; init; }
    public TimeSpan Duration { get; init; }
    public DateTimeOffset AddedAt { get; init; } = DateTimeOffset.UtcNow;
}