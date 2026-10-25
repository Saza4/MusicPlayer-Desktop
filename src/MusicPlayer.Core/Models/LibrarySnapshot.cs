namespace MusicPlayer.Core.Models;

/// <summary>Estado persistible de la biblioteca: pistas y carpetas raíz.</summary>
public sealed record LibrarySnapshot(IReadOnlyList<Track> Tracks, IReadOnlyList<string> Folders)
{
    public static LibrarySnapshot Empty { get; } =
        new(Array.Empty<Track>(), Array.Empty<string>());
}