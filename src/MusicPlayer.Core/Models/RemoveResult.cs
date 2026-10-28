namespace MusicPlayer.Core.Models;

/// <summary>Resultado de quitar pistas: lo que se quitó y lo que no se pudo borrar del disco.</summary>
public sealed record RemoveResult(
    LibrarySnapshot Snapshot,
    IReadOnlyList<Track> Removed,
    IReadOnlyList<string> Errors);