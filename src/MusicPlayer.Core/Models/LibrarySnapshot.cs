namespace MusicPlayer.Core.Models;

/// <summary>Estado persistible de la biblioteca: las pistas.</summary>
public sealed record LibrarySnapshot(IReadOnlyList<Track> Tracks)
{
    public static LibrarySnapshot Empty { get; } = new(Array.Empty<Track>());
}