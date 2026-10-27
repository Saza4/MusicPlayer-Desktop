namespace MusicPlayer.Core.Models;

public sealed record Playlist
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Name { get; init; }
    public IReadOnlyList<Guid> TrackIds { get; init; } = Array.Empty<Guid>();
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}