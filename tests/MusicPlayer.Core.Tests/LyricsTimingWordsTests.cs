using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;

namespace MusicPlayer.Core.Tests;

/// <summary>Relleno por palabra de un .lrc enhanced (docs/specs/lrc-enhanced-fill.md). Letra inventada.</summary>
public class LyricsTimingWordsTests
{
    private static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);

    /// <summary>Palabra con su inicio, su fin opcional y si va pegada a la anterior (sílaba) o separada por un espacio.</summary>
    private sealed record W(string Text, double Start, double? End = null, bool Glued = false);

    private static LyricLine Enhanced(double at, double? lineEnd, params W[] words)
    {
        var text = "";
        var list = new List<LyricWord>();
        foreach (var w in words)
        {
            if (text.Length > 0 && !w.Glued) text += " ";
            list.Add(new LyricWord(w.Text, S(w.Start), w.End is { } e ? S(e) : null, text.Length));
            text += w.Text;
        }
        return new LyricLine(S(at), text, Words: list, End: lineEnd is { } le ? S(le) : null);
    }

    private static LyricBlock Block(params LyricLine[] lines) => new(LyricsKind.Synced, lines.ToList());

    private static LyricLine Normal(double at, string text) => new(S(at), text);

    // ---- FillPoint ----

    [Fact]
    public void FillPoint_FollowsEachWord()
    {
        var block = Block(Enhanced(10, 13, new W("Oh", 10, 11), new W("it", 11, 12), new W("was", 12, 13)));

        var first = LyricsTiming.FillPoint(block, 0, S(10.5))!.Value;
        Assert.Equal((0, 3), (first.Index, first.Length)); // «Oh » (el tramo incluye el espacio)
        Assert.Equal(0.5, first.Fraction, 6);

        var second = LyricsTiming.FillPoint(block, 0, S(11.5))!.Value;
        Assert.Equal((3, 3), (second.Index, second.Length));
        Assert.Equal(0.5, second.Fraction, 6);

        var third = LyricsTiming.FillPoint(block, 0, S(12.25))!.Value;
        Assert.Equal((6, 3), (third.Index, third.Length));
    }

    [Fact]
    public void FillPoint_StartAndEnd_AreZeroAndOne()
    {
        var block = Block(Enhanced(10, 12, new W("Oh", 10, 11), new W("it", 11, 12)));

        Assert.Equal(0, LyricsTiming.FillPoint(block, 0, S(10))!.Value.Fraction, 6);
        Assert.Equal(0, LyricsTiming.FillPoint(block, 0, S(9))!.Value.Fraction, 6); // antes de la línea
        var end = LyricsTiming.FillPoint(block, 0, S(12))!.Value;
        Assert.Equal(1, end.Fraction, 6);
        Assert.Equal(block.Lines[0].Text.Length, end.CharPosition, 6);
        Assert.Equal(1, LyricsTiming.FillPoint(block, 0, S(99))!.Value.Fraction, 6); // mucho después
    }

    [Fact]
    public void FillPoint_RealPauseBetweenWords_StaysAtTheEndOfThePrevious()
    {
        // 0,6 s de silencio (> WordPauseHold): se queda quieto al final del texto de «Oh».
        var block = Block(Enhanced(10, 13, new W("Oh", 10, 10.4), new W("it", 11, 12)));

        var gap = LyricsTiming.FillPoint(block, 0, S(10.7))!.Value;

        Assert.Equal((0, 2), (gap.Index, gap.Length));
        Assert.Equal(1, gap.Fraction, 6);
        Assert.Equal(2, gap.CharPosition, 6);
    }

    [Fact]
    public void FillPoint_GluedSyllables_ContinueWithoutGap()
    {
        // «Hel» + «lo» = «Hello»: el fin de la primera es el inicio de la segunda.
        var block = Block(Enhanced(10, 11, new W("Hel", 10, 10.3), new W("lo", 10.3, 11, Glued: true)));

        Assert.Equal("Hello", block.Lines[0].Text);
        var end = LyricsTiming.FillPoint(block, 0, S(10.299))!.Value;
        var start = LyricsTiming.FillPoint(block, 0, S(10.3))!.Value;

        Assert.Equal(3, start.Index);
        Assert.Equal(end.CharPosition, start.CharPosition, 1); // sin salto
        Assert.Equal(3, start.CharPosition, 6);
    }

    [Fact]
    public void FillPoint_UnorderedTimes_NeverGoBackwards()
    {
        // [00:48.29]So <00:48.07>beat <00:49.00>it : «beat» empieza antes que la línea.
        var block = Block(Enhanced(48.29, 50, new W("So", 48.29, 48.07), new W("beat", 48.07, 49), new W("it", 49, 50)));

        var previous = double.MinValue;
        for (var t = 48.0; t <= 50.5; t += 0.01)
        {
            var point = LyricsTiming.FillPoint(block, 0, S(t))!.Value;
            Assert.True(point.CharPosition >= previous - 1e-9, $"retrocede en {t:F2}");
            previous = point.CharPosition;
        }

        // «beat» entra a la vez que la línea (inicio efectivo 48,29) y recorre su tramo.
        var inside = LyricsTiming.FillPoint(block, 0, S(48.29 + 0.355))!.Value;
        Assert.Equal((3, 5), (inside.Index, inside.Length));
        Assert.Equal(0.5, inside.Fraction, 2);
    }

    [Fact]
    public void FillPoint_WordThatGoesBackInTime_WaitsForTheRunningMaximum()
    {
        // «c» dice 10,5 pero «b» ya empieza en 11: «c» no puede empezar antes (máximo acumulado de los inicios).
        var block = Block(Enhanced(10, 14, new W("a", 10, 11), new W("b", 11, 12), new W("c", 10.5, 14)));

        Assert.Equal(0, LyricsTiming.FillPoint(block, 0, S(10.7))!.Value.Index); // sigue en «a»
        var later = LyricsTiming.FillPoint(block, 0, S(11.2))!.Value;
        Assert.Equal(4, later.Index); // «c», que entra a 11
    }

    [Fact]
    public void FillPoint_ZeroDurationWord_JumpsToItsEnd()
    {
        var block = Block(Enhanced(10, 12, new W("a", 10, 10), new W("b", 10, 12)));

        var point = LyricsTiming.FillPoint(block, 0, S(10))!.Value;

        Assert.Equal(2, point.Index); // «b» ya ha empezado: «a» no se queda a medias
        Assert.Equal(0, point.Fraction, 6);
    }

    [Fact]
    public void FillPoint_EndBeforeStart_IsTreatedAsZeroDuration()
    {
        var block = Block(Enhanced(10, 13, new W("a", 10, 9), new W("b", 11, 13)));

        var point = LyricsTiming.FillPoint(block, 0, S(10.5))!.Value;

        Assert.Equal(0, point.Index);
        Assert.Equal(1, point.Fraction, 6);
    }

    [Fact]
    public void FillPoint_LineWithoutWords_IsNull()
    {
        var block = Block(Normal(10, "hola mundo"), Normal(20, "otra"));

        Assert.Null(LyricsTiming.FillPoint(block, 0, S(11)));
    }

    [Fact]
    public void FillPoint_MixedBlock_EachLineUsesItsOwnPath()
    {
        var block = Block(Normal(10, "hola mundo"), Enhanced(20, 22, new W("uno", 20, 21), new W("dos", 21, 22)));

        Assert.Null(LyricsTiming.FillPoint(block, 0, S(11)));
        Assert.NotNull(LyricsTiming.FillPoint(block, 1, S(20.5)));
    }

    [Fact]
    public void FillPoint_InvalidInputs_AreNull()
    {
        var block = Block(Enhanced(10, 12, new W("a", 10, 12)));

        Assert.Null(LyricsTiming.FillPoint(block, -1, S(10)));
        Assert.Null(LyricsTiming.FillPoint(block, 3, S(10)));
        Assert.Null(LyricsTiming.FillPoint(new LyricBlock(LyricsKind.Plain, block.Lines), 0, S(10)));
        Assert.Null(LyricsTiming.FillPoint(Block(Enhanced(10, 12, new W(" ", 10, 12))), 0, S(10))); // línea en blanco
    }

    [Fact]
    public void FillPoint_OffsetsOutsideTheText_AreClamped()
    {
        var line = new LyricLine(S(10), "hola",
            Words: [new LyricWord("mundo", S(10), S(11), 2), new LyricWord("x", S(11), S(12), 99)],
            End: S(12));
        var block = Block(line);

        var first = LyricsTiming.FillPoint(block, 0, S(10.5))!.Value;
        var second = LyricsTiming.FillPoint(block, 0, S(11.5))!.Value;

        Assert.Equal((2, 2), (first.Index, first.Length));
        Assert.Equal((4, 0), (second.Index, second.Length));
    }

    // ---- FillDuration, Fill y FillEnd con palabras ----

    [Fact]
    public void FillDuration_UsesTheLineEnd_NotTheEstimate()
    {
        // 25 caracteres estimarían 2 s; la línea dura 5.
        var block = Block(Enhanced(10, 15, new W("a", 10, 12), new W("b", 12, 15)), Normal(60, "siguiente"));

        Assert.Equal(S(5), LyricsTiming.FillDuration(block, 0));
        Assert.Equal(S(15), LyricsTiming.FillEnd(block, 0));
    }

    [Fact]
    public void FillDuration_WithoutLineEnd_UsesTheLastWordPlusEstimate()
    {
        var block = Block(Enhanced(10, null, new W("a", 10), new W("hola", 11)), Normal(60, "siguiente"));

        // última palabra: 4 caracteres × 80 ms, sin el mínimo de 1 s de la línea entera.
        Assert.Equal(S(1.32), LyricsTiming.FillDuration(block, 0));
    }

    [Fact]
    public void FillDuration_WithWords_NeverExceedsTimeUntilNextLine()
    {
        var block = Block(Enhanced(10, 20, new W("a", 10, 15), new W("b", 15, 20)), Normal(12, "siguiente"));

        Assert.Equal(S(2), LyricsTiming.FillDuration(block, 0));
    }

    [Fact]
    public void FillDuration_WithWords_SameTimestampAsNext_IsZero()
    {
        var block = Block(Enhanced(10, 15, new W("a", 10, 15)), Normal(10, "B"), Normal(30, "C"));

        Assert.Equal(TimeSpan.Zero, LyricsTiming.FillDuration(block, 0));
    }

    [Fact]
    public void Fill_WithWords_ReachesOneAtTheLineEnd()
    {
        var block = Block(Enhanced(10, 14, new W("a", 10, 12), new W("b", 12, 14)), Normal(60, "siguiente"));

        Assert.Equal(0, LyricsTiming.Fill(block, 0, S(9)), 6);
        Assert.Equal(0.5, LyricsTiming.Fill(block, 0, S(12)), 6);
        Assert.Equal(1, LyricsTiming.Fill(block, 0, S(14)), 6);
    }

    [Fact]
    public void FillDuration_LineWithoutWords_IsUnchanged()
    {
        var block = Block(Normal(0, new string('a', 25)), Normal(60, "B"));

        Assert.Equal(S(2), LyricsTiming.FillDuration(block, 0));
    }

    // ---- Pausas instrumentales con letra enhanced ----

    [Fact]
    public void InstrumentalGap_StartsWhereTheEnhancedLineEnds()
    {
        // La estimación (3 caracteres → 1 s) pondría la pausa en 6; el fin real es 12.
        var block = Block(Enhanced(5, 12, new W("a", 5, 11), new W("b", 11, 12)), Normal(30, "siguiente"));

        var gap = Assert.Single(LyricsTiming.GetInstrumentalGaps(block));

        Assert.Equal(S(12), gap.Start);
        Assert.Equal(S(30), gap.End);
    }

    [Fact]
    public void InstrumentalGap_LongEnhancedLine_DoesNotCreateAFalseGap()
    {
        // Línea de 15 s con tiempos reales, siguiente a 2 s después: no hay pausa.
        var block = Block(Enhanced(5, 20, new W("a", 5, 15), new W("b", 15, 20)), Normal(22, "siguiente"));

        Assert.Empty(LyricsTiming.GetInstrumentalGaps(block));
    }

    // ---- LyricsTimeline ----

    [Fact]
    public void Timeline_Fill_WithEnhancedLine_UsesTheRealDuration()
    {
        var block = Block(Enhanced(10, 14, new W("a", 10, 12), new W("b", 12, 14)), Normal(60, "siguiente"));
        var timeline = LyricsTimeline.Build(block);
        var line = timeline.Entries.ToList().FindIndex(e => e.Kind == TimelineEntryKind.Line && e.LineIndex == 0);

        Assert.Equal(0.5, timeline.FillAt(line, S(12)), 6);
        Assert.Equal(1, timeline.FillAt(line, S(14)), 6);
    }

    [Fact]
    public void Timeline_FillPointAt_ReturnsThePointOfEnhancedLines_AndNullForTheRest()
    {
        var block = Block(
            Normal(1, "normal"),
            Enhanced(5, 7, new W("a", 5, 6), new W("b", 6, 7)),
            Normal(40, "siguiente"));
        var timeline = LyricsTimeline.Build(block);
        int Entry(int lineIndex) => timeline.Entries.ToList().FindIndex(e => e.Kind == TimelineEntryKind.Line && e.LineIndex == lineIndex);

        var point = timeline.FillPointAt(Entry(1), S(6.5));
        Assert.NotNull(point);
        Assert.Equal(2, point.Value.Index); // «b»
        Assert.Null(timeline.FillPointAt(Entry(1), S(4)));    // aún no empieza
        Assert.Null(timeline.FillPointAt(Entry(0), S(2)));    // línea normal
        Assert.Null(timeline.FillPointAt(-1, S(6)));
        Assert.Null(timeline.FillPointAt(99, S(6)));

        var gap = timeline.Entries.ToList().FindIndex(e => e.Kind == TimelineEntryKind.Gap);
        Assert.True(gap >= 0);
        Assert.Null(timeline.FillPointAt(gap, S(20)));        // pausa «♪»
    }

    // ---- Tramo continuo (M2b) ----

    [Fact]
    public void FillPoint_ShortPauseBetweenWords_ContinuesThroughTheSpace()
    {
        // Pausa de exactamente WordPauseHold (250 ms): cuenta como continua.
        var block = Block(Enhanced(10, 13, new W("Oh", 10, 10.75), new W("it", 11, 12)));

        var mid = LyricsTiming.FillPoint(block, 0, S(10.875))!.Value; // a mitad del tramo estirado (10 → 11)

        Assert.Equal((0, 3), (mid.Index, mid.Length));
        Assert.Equal(0.875, mid.Fraction, 6);
    }

    [Fact]
    public void FillPoint_JustOverTheHold_StopsAtTheEndOfTheWord()
    {
        var gap = LyricsTiming.WordPauseHold + TimeSpan.FromMilliseconds(1);
        var endOfOh = 10.5;
        var block = Block(Enhanced(10, 13, new W("Oh", 10, endOfOh), new W("it", endOfOh + gap.TotalSeconds, 12)));

        var point = LyricsTiming.FillPoint(block, 0, S(endOfOh + 0.1))!.Value;

        Assert.Equal((0, 2), (point.Index, point.Length));
        Assert.Equal(1, point.Fraction, 6);
    }

    [Fact]
    public void FillPoint_ContiguousWords_PositionIsContinuousAcrossTheWholeLine()
    {
        // Sin pausas, CharPosition crece sin saltos entre palabras, con espacios y con sílabas pegadas.
        var block = Block(Enhanced(10, 14,
            new W("Hel", 10, 10.6), new W("lo", 10.6, 11.4, Glued: true), new W("big", 11.4, 12.2), new W("world", 12.2, 14)));
        Assert.Equal("Hello big world", block.Lines[0].Text);

        double? previous = null;
        for (var t = 10.0; t <= 14.0; t += 0.001)
        {
            var position = LyricsTiming.FillPoint(block, 0, S(t))!.Value.CharPosition;
            if (previous is { } p)
            {
                Assert.True(position >= p - 1e-9, $"retrocede en {t:F3}");
                Assert.True(position - p < 0.2, $"salta {position - p:F3} caracteres en {t:F3}"); // 0,001 s → como mucho unas décimas
            }
            previous = position;
        }
        Assert.Equal(block.Lines[0].Text.Length, previous!.Value, 6); // acaba en el final del texto
    }

    [Fact]
    public void FillPoint_LastWord_EndsAtTheEndOfTheText()
    {
        var block = Block(Enhanced(10, 12, new W("a", 10, 11), new W("bc", 11, 12)));

        var end = LyricsTiming.FillPoint(block, 0, S(12))!.Value;

        Assert.Equal((2, 2), (end.Index, end.Length));
        Assert.Equal(4, end.CharPosition, 6);
    }

    // ---- M3: pausas y línea de tiempo con letra enhanced ----

    private static int LineEntry(LyricsTimeline timeline, int lineIndex) =>
        timeline.Entries.ToList().FindIndex(e => e.Kind == TimelineEntryKind.Line && e.LineIndex == lineIndex);

    [Fact]
    public void InstrumentalGap_AfterTheLastEnhancedLine_StartsAtItsEnd()
    {
        var block = Block(Enhanced(5, 12, new W("a", 5, 8), new W("b", 8, 12)));

        var gap = Assert.Single(LyricsTiming.GetInstrumentalGaps(block, S(30)).Where(g => g.Start >= S(5)));

        Assert.Equal((S(12), S(30)), (gap.Start, gap.End));
    }

    [Fact]
    public void Timeline_EnhancedLines_PlaceTheGapAfterTheRealEnd()
    {
        var block = Block(Enhanced(5, 12, new W("a", 5, 8), new W("b", 8, 12)), Normal(30, "siguiente"));
        var timeline = LyricsTimeline.Build(block);

        var gapEntry = timeline.Entries.ToList().FindIndex(e => e.Kind == TimelineEntryKind.Gap && e.Start == S(12));
        Assert.True(gapEntry >= 0, "falta la pausa «♪» a partir del fin real de la línea");

        Assert.Equal(0.5, timeline.FillAt(gapEntry, S(21)), 6);                 // mitad de 12 → 30
        Assert.Equal(gapEntry, timeline.ActiveEntryAt(S(15)));
        Assert.Equal(S(30), timeline.NextStartAfter(S(15)));
        Assert.Equal(1, timeline.FillAt(LineEntry(timeline, 0), S(13)), 6);     // la línea ya está rellena
    }

    [Fact]
    public void Gap_FollowsTheDeclaredLineEnd_WhenItIsLaterThanTheLastWord()
    {
        // La última palabra acaba en 8 pero la línea declara fin en 11: la pausa empieza en 11.
        var block = Block(Enhanced(5, 11, new W("a", 5, 6), new W("b", 6, 8)), Normal(30, "siguiente"));

        Assert.Equal(S(11), LyricsTiming.FillEnd(block, 0));
        Assert.Contains(LyricsTiming.GetInstrumentalGaps(block), g => g.Start == S(11) && g.End == S(30));
    }

    [Fact]
    public void FillEnd_WordsThatRunPastTheNextLine_AreCappedAndCreateNoGap()
    {
        var block = Block(Enhanced(5, 40, new W("a", 5, 20), new W("b", 20, 40)), Normal(12, "siguiente"));

        Assert.Equal(S(12), LyricsTiming.FillEnd(block, 0));
        Assert.DoesNotContain(LyricsTiming.GetInstrumentalGaps(block), g => g.Start >= S(5) && g.Start < S(12));
    }

    [Fact]
    public void Gap_AfterEnhancedLineWithoutEnd_StartsAfterTheLastWordEstimate()
    {
        // Última palabra «hola» (4 × 80 ms) desde 10: fin en 10,32.
        var block = Block(Enhanced(5, null, new W("a", 5), new W("hola", 10)), Normal(30, "siguiente"));

        Assert.Equal(S(10.32), LyricsTiming.FillEnd(block, 0));
        Assert.Contains(LyricsTiming.GetInstrumentalGaps(block), g => g.Start == S(10.32) && g.End == S(30));
    }

    [Fact]
    public void Gap_ExplicitBlankLineAfterAnEnhancedLine_StillWorks()
    {
        var block = Block(Enhanced(5, 8, new W("a", 5, 8)), Normal(8, ""), Normal(30, "siguiente"));

        var gap = Assert.Single(LyricsTiming.GetInstrumentalGaps(block).Where(g => g.Start >= S(5)));

        Assert.Equal((S(8), S(30)), (gap.Start, gap.End));
        // Una sola entrada para esa pausa: la línea vacía, sin duplicar con una «♪» deducida.
        var timeline = LyricsTimeline.Build(block);
        Assert.Equal(1, timeline.Entries.Count(e => e.Start == S(8)));
    }

    [Fact]
    public void FillEnd_EnhancedLineSharingItsMarkWithTheNext_IsItsOwnStart()
    {
        var block = Block(Enhanced(10, 15, new W("a", 10, 15)), Normal(10, "B"), Normal(40, "C"));

        Assert.Equal(S(10), LyricsTiming.FillEnd(block, 0));
        Assert.Equal(TimeSpan.Zero, LyricsTiming.FillDuration(block, 0));
    }

    [Fact]
    public void Timeline_MixedBlock_UsesEachLinePath()
    {
        var block = Block(Normal(5, new string('a', 25)), Enhanced(20, 24, new W("x", 20, 22), new W("y", 22, 24)), Normal(60, "fin"));
        var timeline = LyricsTimeline.Build(block);

        // Normal: 25 × 80 ms = 2 s. Enhanced: su fin real (4 s).
        Assert.Equal(0.5, timeline.FillAt(LineEntry(timeline, 0), S(6)), 6);
        Assert.Equal(0.5, timeline.FillAt(LineEntry(timeline, 1), S(22)), 6);
        Assert.Null(timeline.FillPointAt(LineEntry(timeline, 0), S(6)));
        Assert.NotNull(timeline.FillPointAt(LineEntry(timeline, 1), S(22)));
    }
}
