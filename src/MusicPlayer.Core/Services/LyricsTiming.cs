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
/// Dónde está el relleno de una línea con tiempos por palabra: la palabra activa y lo recorrido de ella. La posición
/// continua en <c>LyricLine.Text</c> es <c>Index + Fraction × Length</c>; la vista la convierte en x.
/// </summary>
/// <param name="Index">Carácter de <c>LyricLine.Text</c> donde empieza el tramo (una palabra).</param>
/// <param name="Length">Longitud del tramo: la palabra y, si no hay pausa real, hasta la siguiente (espacio incluido).</param>
/// <param name="Fraction">Fracción recorrida del tramo, de 0 a 1.</param>
public readonly record struct FillPoint(int Index, int Length, double Fraction)
{
    public double CharPosition => Index + Fraction * Length;
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

    /// <summary>
    /// Pausa máxima entre una palabra y la siguiente (de un .lrc enhanced) para que el relleno no se detenga: hasta
    /// aquí es una sola pasada continua; con más es un silencio real y se queda al final de la palabra.
    /// </summary>
    public static readonly TimeSpan WordPauseHold = TimeSpan.FromMilliseconds(250);

    /// <summary>Un hueco sin letra a partir de esta duración es una pausa instrumental.</summary>
    public static readonly TimeSpan InstrumentalThreshold = TimeSpan.FromSeconds(8);

    /// <summary>Lo que tarda en rellenarse la línea. Cero si no existe o si comparte marca con la siguiente.</summary>
    public static TimeSpan FillDuration(LyricBlock block, int index)
    {
        if (block.Kind != LyricsKind.Synced || index < 0 || index >= block.Lines.Count) return TimeSpan.Zero;

        var line = block.Lines[index];
        if (IsBlank(line)) return TimeSpan.Zero;

        // Con tiempos por palabra (.lrc enhanced) la línea dura hasta su fin real; si no, se estima por la longitud.
        TimeSpan duration;
        if (EffectiveSpans(line) is { } spans)
        {
            var lineEnd = spans[^1].End;
            if (line.End is { } declared && declared > lineEnd) lineEnd = declared;
            duration = lineEnd > line.Timestamp ? lineEnd - line.Timestamp : TimeSpan.Zero;
        }
        else
        {
            duration = Clamp(PerCharacter * line.Text.Trim().Length, MinFill, MaxFill);
        }

        if (block.GetLineEnd(index) is not { } next) return duration; // última línea
        var room = next - line.Timestamp;
        if (room <= TimeSpan.Zero) return TimeSpan.Zero;
        return room < duration ? room : duration;
    }

    /// <summary>
    /// Dónde está el relleno de la línea en <paramref name="position"/> según los tiempos por palabra, o null si la
    /// línea no los tiene (entonces vale <see cref="Fill"/>). Entre dos palabras se queda al final de la anterior.
    /// </summary>
    public static FillPoint? FillPoint(LyricBlock block, int index, TimeSpan position)
    {
        if (block.Kind != LyricsKind.Synced || index < 0 || index >= block.Lines.Count) return null;

        var line = block.Lines[index];
        if (IsBlank(line) || EffectiveSpans(line) is not { } spans) return null;

        // Última palabra que ya empezó (los inicios efectivos no decrecen). Antes de la primera: la primera, sin avance.
        var current = 0;
        for (int i = 0; i < spans.Length; i++)
        {
            if (spans[i].Start <= position) current = i;
            else break;
        }

        var word = line.Words![current];
        var span = spans[current];

        // Los desplazamientos vienen de un archivo: no deben salirse del texto.
        var start = Math.Clamp(word.Offset, 0, line.Text.Length);
        var textEnd = start + Math.Clamp(word.Text.Length, 0, line.Text.Length - start);
        var timeEnd = span.End;

        // Sin pausa real hasta la siguiente palabra, el tramo la alcanza (espacio incluido): el relleno ni se
        // detiene ni salta el espacio, y la velocidad cambia de una palabra a otra sin escalón de posición.
        if (current + 1 < spans.Length && spans[current + 1].Start - span.End <= WordPauseHold)
        {
            timeEnd = spans[current + 1].Start;
            textEnd = Math.Clamp(line.Words[current + 1].Offset, textEnd, line.Text.Length);
        }

        double fraction;
        if (position < span.Start) fraction = 0;
        else if (position >= timeEnd) fraction = 1;
        else fraction = (position - span.Start) / (timeEnd - span.Start); // timeEnd > Start: si no, caería arriba

        return new FillPoint(start, textEnd - start, fraction);
    }

    /// <summary>
    /// Tiempos de cada palabra con el máximo acumulado de los inicios (los archivos reales traen palabras
    /// desordenadas y el relleno no debe retroceder). Null si la línea no tiene palabras.
    /// </summary>
    private static (TimeSpan Start, TimeSpan End)[]? EffectiveSpans(LyricLine line)
    {
        if (line.Words is not { Count: > 0 } words) return null;

        var spans = new (TimeSpan Start, TimeSpan End)[words.Count];
        var floor = TimeSpan.MinValue;
        for (int i = 0; i < words.Count; i++)
        {
            if (words[i].Start > floor) floor = words[i].Start;
            spans[i].Start = floor;
        }

        for (int i = 0; i < words.Count; i++)
        {
            var next = i + 1 < words.Count ? spans[i + 1].Start : (TimeSpan?)null;
            var end = words[i].End
                ?? next
                ?? line.End
                ?? spans[i].Start + PerCharacter * words[i].Text.Length; // sin tope Min/Max: es una palabra, no la línea

            if (next is { } n && end > n) end = n; // no se solapa con la siguiente
            if (end < spans[i].Start) end = spans[i].Start;
            spans[i].End = end;
        }

        return spans;
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
