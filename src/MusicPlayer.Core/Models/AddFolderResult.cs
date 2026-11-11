namespace MusicPlayer.Core.Models;

/// <summary>
/// Resultado de escanear una carpeta: la biblioteca, cuántas pistas movidas se reubicaron y cuántos temporales
/// antiguos de la app se borraron.
/// </summary>
public sealed record AddFolderResult(LibrarySnapshot Snapshot, int Relocated, int TemporariesRemoved = 0);