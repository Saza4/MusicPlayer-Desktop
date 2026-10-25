namespace MusicPlayer.Core.Models;

public enum LyricsKind { None, Plain, Synced }

/// <summary>Letra completa de una canción.</summary>
public sealed record LyricBlock(
    LyricsKind Kind,
    IReadOnlyList<LyricLine> Lines,
    string? Language = null)
{
    public static LyricBlock Empty { get; } = new(LyricsKind.None, Array.Empty<LyricLine>());

    /// <summary>Si es false, la UI deshabilita/atenúa el botón de letra.</summary>
    public bool IsAvailable => Kind != LyricsKind.None && Lines.Count > 0;

    /// <summary>Índice de la línea activa (búsqueda binaria). -1 si aún no empieza.</summary>
    public int GetActiveLineIndex(TimeSpan position)
    {
        if (Kind != LyricsKind.Synced) return -1;

        int lo = 0, hi = Lines.Count - 1, result = -1;
        while (lo <= hi)
        {
            int mid = (lo + hi) / 2;
            if (Lines[mid].Timestamp <= position) { result = mid; lo = mid + 1; }
            else hi = mid - 1;
        }
        return result;
    }
}