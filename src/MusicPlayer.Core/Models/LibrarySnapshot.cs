namespace MusicPlayer.Core.Models;

/// <summary>Estado persistible de la biblioteca: las pistas y las carpetas agregadas.</summary>
public sealed record LibrarySnapshot(IReadOnlyList<Track> Tracks)
{
    public static LibrarySnapshot Empty { get; } = new(Array.Empty<Track>());

    /// <summary>
    /// Carpetas agregadas con «Agregar carpeta», que se revisan solas (docs/specs/auto-refresh-folders.md). Sin
    /// duplicados ni carpetas dentro de otras (<see cref="Services.LibraryFolders"/>).
    /// </summary>
    public IReadOnlyList<string> Folders { get; init; } = Array.Empty<string>();
}
