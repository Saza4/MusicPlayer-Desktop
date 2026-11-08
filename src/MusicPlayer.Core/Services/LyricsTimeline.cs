using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Services;

public enum TimelineEntryKind
{
    /// <summary>Una línea de la letra (si su texto está vacío, es una pausa instrumental explícita).</summary>
    Line,

    /// <summary>Una pausa instrumental deducida por un hueco largo (criterio 5): se muestra como «♪».</summary>
    Gap,
}

/// <summary>Lo que muestra la letra progresiva, en orden: líneas y pausas.</summary>
/// <param name="LineIndex">Índice en <see cref="LyricBlock.Lines"/>; -1 para una pausa deducida.</param>
public sealed record TimelineEntry(TimelineEntryKind Kind, int LineIndex, TimeSpan Start);

/// <summary>
/// Secuencia de la letra progresiva (docs/specs/lyrics-progressive.md, criterios 1 y 5): las líneas sincronizadas
/// más las pausas instrumentales de <see cref="LyricsTiming.GetInstrumentalGaps"/> que no son ya una línea vacía.
/// Todo se calcula a partir de la posición, así que sirve igual al avanzar, al saltar o en pausa.
/// </summary>
public sealed class LyricsTimeline
{
    private readonly LyricBlock _block;
    private readonly InstrumentalGap?[] _entryGaps; // la pausa que rellena cada entrada (pausas y líneas vacías)

    private LyricsTimeline(LyricBlock block, IReadOnlyList<InstrumentalGap> gaps, List<TimelineEntry> entries)
    {
        _block = block;
        Entries = entries;

        _entryGaps = new InstrumentalGap?[entries.Count];
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry.Kind == TimelineEntryKind.Gap || IsBlank(entry))
                _entryGaps[i] = LyricsTiming.GapAt(gaps, entry.Start);
        }
    }

    public static LyricsTimeline Empty { get; } = new(LyricBlock.Empty, Array.Empty<InstrumentalGap>(), []);

    public IReadOnlyList<TimelineEntry> Entries { get; }

    /// <param name="trackDuration">Duración de la pista, para la pausa final. Null o cero si no se conoce.</param>
    public static LyricsTimeline Build(LyricBlock block, TimeSpan? trackDuration = null)
    {
        if (block.Kind != LyricsKind.Synced || block.Lines.Count == 0) return Empty;

        var duration = trackDuration is { } d && d > TimeSpan.Zero ? d : (TimeSpan?)null;
        var gaps = LyricsTiming.GetInstrumentalGaps(block, duration);

        var entries = new List<TimelineEntry>(block.Lines.Count + gaps.Count);
        for (int i = 0; i < block.Lines.Count; i++)
            entries.Add(new TimelineEntry(TimelineEntryKind.Line, i, block.Lines[i].Timestamp));

        // Una pausa que empieza justo en una línea vacía ya la representa esa línea: no se duplica.
        foreach (var gap in gaps)
        {
            var startsAtBlankLine = block.Lines.Any(l => l.Timestamp == gap.Start && string.IsNullOrWhiteSpace(l.Text));
            if (!startsAtBlankLine)
                entries.Add(new TimelineEntry(TimelineEntryKind.Gap, -1, gap.Start));
        }

        // Orden por inicio; con empate, la línea antes que la pausa (estable: conserva el orden de las líneas).
        var ordered = entries
            .Select((entry, order) => (entry, order))
            .OrderBy(x => x.entry.Start)
            .ThenBy(x => x.entry.Kind)
            .ThenBy(x => x.order)
            .Select(x => x.entry)
            .ToList();

        return new LyricsTimeline(block, gaps, ordered);
    }

    /// <summary>La entrada que suena en <paramref name="position"/>: la última que ya empezó. -1 antes de la primera.</summary>
    public int ActiveEntryAt(TimeSpan position)
    {
        int lo = 0, hi = Entries.Count - 1, result = -1;
        while (lo <= hi)
        {
            int mid = (lo + hi) / 2;
            if (Entries[mid].Start <= position) { result = mid; lo = mid + 1; }
            else hi = mid - 1;
        }
        return result;
    }

    /// <summary>
    /// Cuánto está rellena la entrada en <paramref name="position"/>, de 0 a 1. Las líneas, según
    /// <see cref="LyricsTiming.Fill"/>; las pausas (y las líneas vacías), según lo que llevan de pausa.
    /// </summary>
    public double FillAt(int entryIndex, TimeSpan position)
    {
        if (entryIndex < 0 || entryIndex >= Entries.Count) return 0;

        var entry = Entries[entryIndex];
        if (position < entry.Start) return 0;

        if (_entryGaps[entryIndex] is { } gap)
            return gap.Progress(position);

        return entry.Kind == TimelineEntryKind.Line
            ? LyricsTiming.Fill(_block, entry.LineIndex, position)
            : 1;
    }

    /// <summary>Inicio de la primera entrada posterior a <paramref name="position"/>, para despertar el reloj.</summary>
    public TimeSpan? NextStartAfter(TimeSpan position)
    {
        var next = ActiveEntryAt(position) + 1;
        // Varias entradas pueden compartir inicio: la siguiente es la primera que empieza después.
        while (next < Entries.Count && Entries[next].Start <= position) next++;
        return next < Entries.Count ? Entries[next].Start : null;
    }

    private bool IsBlank(TimelineEntry entry) =>
        entry.Kind == TimelineEntryKind.Line && string.IsNullOrWhiteSpace(_block.Lines[entry.LineIndex].Text);
}
