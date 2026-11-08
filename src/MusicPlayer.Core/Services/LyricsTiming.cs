using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Services;

/// <summary>Una pausa instrumental: tramo sin letra que la vista muestra con «♪».</summary>
public sealed record InstrumentalGap(TimeSpan Start, TimeSpan End)
{
    public TimeSpan Duration => End - Start;

    public bool Contains(TimeSpan position) => position >= Start && position < End;

    /// <summary>Avance dentro de la pausa, de 0 a 1.</summary>
    public double Progress(TimeSpan position) =>
        Duration <= TimeSpan.Zero ? 1 : Math.Clamp((position - Start) / Duration, 0, 1);
}

/// <summary>
/// Tiempos de la letra progresiva (docs/specs/lyrics-progressive.md). Un .lrc normal solo trae el inicio de cada
/// línea, así que lo que dura cantándose se estima por la longitud del texto, con el hueco hasta la siguiente
/// como tope. Todo es función de la posición: no hay estado que se acumule al saltar.
/// </summary>
public static class LyricsTiming
{
    /// <summary>Tiempo de canto estimado por carácter.</summary>
    public static readonly TimeSpan PerCharacter = TimeSpan.FromMilliseconds(80);

    /// <summary>Límites de la estimación: ni un destello en líneas cortas ni un relleno eterno en las largas.</summary>
    public static readonly TimeSpan MinFill = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan MaxFill = TimeSpan.FromSeconds(8);

    /// <summary>Un hueco sin letra a partir de esta duración es una pausa instrumental.</summary>
    public static readonly TimeSpan InstrumentalThreshold = TimeSpan.FromSeconds(8);

    /// <summary>Lo que tarda en rellenarse la línea. Cero si no existe o si comparte marca con la siguiente.</summary>
    public static TimeSpan FillDuration(LyricBlock block, int index)
    {
        if (block.Kind != LyricsKind.Synced || index < 0 || index >= block.Lines.Count) return TimeSpan.Zero;

        var line = block.Lines[index];
        if (IsBlank(line)) return TimeSpan.Zero;

        var characters = line.Text.Trim().Length;
        var estimated = Clamp(PerCharacter * characters, MinFill, MaxFill);

        if (block.GetLineEnd(index) is not { } next) return estimated; // última línea
        var room = next - line.Timestamp;
        if (room <= TimeSpan.Zero) return TimeSpan.Zero;
        return room < estimated ? room : estimated;
    }

    /// <summary>Cuánto está rellena la línea en <paramref name="position"/>, de 0 a 1.</summary>
    public static double Fill(LyricBlock block, int index, TimeSpan position)
    {
        if (block.Kind != LyricsKind.Synced || index < 0 || index >= block.Lines.Count) return 0;

        var start = block.Lines[index].Timestamp;
        if (position < start) return 0;

        var duration = FillDuration(block, index);
        return duration <= TimeSpan.Zero ? 1 : Math.Clamp((position - start) / duration, 0, 1);
    }

    /// <summary>Momento en que la línea queda rellena del todo.</summary>
    public static TimeSpan FillEnd(LyricBlock block, int index) =>
        index < 0 || index >= block.Lines.Count
            ? TimeSpan.Zero
            : block.Lines[index].Timestamp + FillDuration(block, index);

    /// <summary>
    /// Pausas instrumentales, en orden y sin solaparse. Hay pausa cuando del fin estimado de una línea a la
    /// siguiente pasan al menos <see cref="InstrumentalThreshold"/>, antes de la primera línea y después de la
    /// última (con la duración de la pista, si se conoce) con el mismo umbral, y siempre desde una línea con marca
    /// y texto vacío hasta la siguiente. Las pausas contiguas se unen.
    /// </summary>
    public static IReadOnlyList<InstrumentalGap> GetInstrumentalGaps(LyricBlock block, TimeSpan? trackDuration = null)
    {
        if (block.Kind != LyricsKind.Synced || block.Lines.Count == 0) return Array.Empty<InstrumentalGap>();

        var gaps = new List<InstrumentalGap>();
        var lines = block.Lines;

        AddIfLong(gaps, TimeSpan.Zero, lines[0].Timestamp);

        for (int i = 0; i < lines.Count; i++)
        {
            var next = block.GetLineEnd(i);
            if (IsBlank(lines[i]))
            {
                var end = next ?? trackDuration;
                if (end is { } e && e > lines[i].Timestamp)
                    gaps.Add(new InstrumentalGap(lines[i].Timestamp, e));
                continue;
            }

            var lineEnd = FillEnd(block, i);
            if (next is { } n) AddIfLong(gaps, lineEnd, n);
            else if (trackDuration is { } d) AddIfLong(gaps, lineEnd, d);
        }

        return Merge(gaps);
    }

    /// <summary>La pausa que contiene <paramref name="position"/>, o null.</summary>
    public static InstrumentalGap? GapAt(IReadOnlyList<InstrumentalGap> gaps, TimeSpan position)
    {
        foreach (var gap in gaps)
        {
            if (gap.Contains(position)) return gap;
            if (gap.Start > position) break; // están en orden
        }
        return null;
    }

    private static bool IsBlank(LyricLine line) => string.IsNullOrWhiteSpace(line.Text);

    private static void AddIfLong(List<InstrumentalGap> gaps, TimeSpan start, TimeSpan end)
    {
        if (end - start >= InstrumentalThreshold)
            gaps.Add(new InstrumentalGap(start, end));
    }

    private static IReadOnlyList<InstrumentalGap> Merge(List<InstrumentalGap> gaps)
    {
        gaps.Sort((a, b) => a.Start.CompareTo(b.Start));
        var merged = new List<InstrumentalGap>(gaps.Count);
        foreach (var gap in gaps)
        {
            if (merged.Count > 0 && gap.Start <= merged[^1].End)
            {
                var last = merged[^1];
                merged[^1] = last with { End = gap.End > last.End ? gap.End : last.End };
            }
            else merged.Add(gap);
        }
        return merged;
    }

    private static TimeSpan Clamp(TimeSpan value, TimeSpan min, TimeSpan max) =>
        value < min ? min : value > max ? max : value;
}
