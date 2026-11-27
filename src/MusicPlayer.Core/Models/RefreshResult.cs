namespace MusicPlayer.Core.Models;

/// <summary>
/// Resultado de revisar las carpetas agregadas (docs/specs/auto-refresh-folders.md): la biblioteca, cuántas pistas
/// nuevas entraron, cuántas movidas se reubicaron y cuántas carpetas no se pudieron revisar.
/// </summary>
public sealed record RefreshResult(LibrarySnapshot Snapshot, int Added, int Relocated, int FoldersUnreachable)
{
    public bool Changed => Added > 0 || Relocated > 0;
}
