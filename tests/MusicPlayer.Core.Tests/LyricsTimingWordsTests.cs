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
        Assert.Equal((0, 2), (first.Index, first.Length));
        Assert.Equal(0.5, first.Fraction, 6);

        var second = LyricsTiming.FillPoint(block, 0, S(11.5))!.Value;
        Assert.Equal((3, 2), (second.Index, second.Length));
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
    public void FillPoint_BetweenWords_StaysAtTheEndOfThePrevious()
    {
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
        Assert.Equal((3, 4), (inside.Index, inside.Length));
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
}
