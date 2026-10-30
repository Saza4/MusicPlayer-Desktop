namespace MusicPlayer.Core.Models;

/// <summary>Resultado de escanear una carpeta: la biblioteca y cuántas pistas movidas se reubicaron.</summary>
public sealed record AddFolderResult(LibrarySnapshot Snapshot, int Relocated);