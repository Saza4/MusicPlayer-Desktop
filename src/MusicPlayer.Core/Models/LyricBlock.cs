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

    /// <summary>
    /// Fin del hueco de la línea: la marca de la siguiente. Null si es la última, si no está sincronizada o si el
    /// índice no existe. Con dos líneas en la misma marca, la primera tiene un hueco de duración cero.
    /// </summary>
    public TimeSpan? GetLineEnd(int index)
    {
        if (Kind != LyricsKind.Synced || index < 0 || index + 1 >= Lines.Count) return null;
        return Lines[index + 1].Timestamp;
    }
}