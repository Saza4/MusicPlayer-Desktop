using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;

namespace MusicPlayer.Core.Tests;

public class LyricsTimingTests
{
    private static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);

    private static LyricBlock Synced(params (double At, string Text)[] lines) =>
        new(LyricsKind.Synced, lines.Select(l => new LyricLine(S(l.At), l.Text)).ToList());

    private static string Chars(int count) => new('a', count);

    // ---- LyricBlock.GetLineEnd ----

    [Fact]
    public void GetLineEnd_IsNextTimestamp_AndNullForLastOrInvalid()
    {
        var block = Synced((10, "A"), (15, "B"));

        Assert.Equal(S(15), block.GetLineEnd(0));
        Assert.Null(block.GetLineEnd(1));
        Assert.Null(block.GetLineEnd(-1));
        Assert.Null(block.GetLineEnd(5));
    }

    [Fact]
    public void GetLineEnd_PlainLyrics_IsNull()
    {
        var block = new LyricBlock(LyricsKind.Plain, [new LyricLine(TimeSpan.Zero, "A"), new LyricLine(TimeSpan.Zero, "B")]);

        Assert.Null(block.GetLineEnd(0));
    }

    // ---- FillDuration ----

    [Theory]
    [InlineData(25, 2.0)]   // 25 × 80 ms
    [InlineData(5, 1.0)]    // 0,4 s → mínimo de 1 s
    [InlineData(200, 8.0)]  // 16 s → máximo de 8 s
    public void FillDuration_EstimatesByLength_WithinLimits(int characters, double expected)
    {
        var block = Synced((0, Chars(characters)), (60, "siguiente"));

        Assert.Equal(S(expected), LyricsTiming.FillDuration(block, 0));
    }

    [Fact]
    public void FillDuration_NeverExceedsTimeUntilNextLine()
    {
        var block = Synced((10, Chars(50)), (11.5, "B")); // estimado 4 s, hay 1,5 s

        Assert.Equal(S(1.5), LyricsTiming.FillDuration(block, 0));
    }

    [Fact]
    public void FillDuration_IgnoresSurroundingSpaces()
    {
        var block = Synced((0, "   " + Chars(25) + "  "), (60, "B"));

        Assert.Equal(S(2), LyricsTiming.FillDuration(block, 0));
    }

    [Fact]
    public void FillDuration_LastLine_UsesEstimate()
    {
        var block = Synced((0, "A"), (10, Chars(25)));

        Assert.Equal(S(2), LyricsTiming.FillDuration(block, 1));
    }

    [Fact]
    public void FillDuration_SameTimestampAsNext_IsZero()
    {
        var block = Synced((10, "A"), (10, "B"), (20, "C"));

        Assert.Equal(TimeSpan.Zero, LyricsTiming.FillDuration(block, 0));
    }

    [Fact]
    public void FillDuration_BlankLine_IsZero()
    {
        var block = Synced((10, "   "), (30, "B"));

        Assert.Equal(TimeSpan.Zero, LyricsTiming.FillDuration(block, 0));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void FillDuration_InvalidIndex_IsZero(int index)
    {
        var block = Synced((0, "A"), (5, "B"));

        Assert.Equal(TimeSpan.Zero, LyricsTiming.FillDuration(block, index));
    }

    // ---- Fill ----

    [Theory]
    [InlineData(9.0, 0.0)]   // antes de empezar
    [InlineData(10.0, 0.0)]
    [InlineData(11.0, 0.5)]  // duración 2 s (25 caracteres)
    [InlineData(12.0, 1.0)]
    [InlineData(19.0, 1.0)]  // ya cantada: queda llena
    public void Fill_FollowsPosition(double position, double expected)
    {
        var block = Synced((10, Chars(25)), (20, "B"));

        Assert.Equal(expected, LyricsTiming.Fill(block, 0, S(position)), 6);
    }

    [Fact]
    public void Fill_SeekBackwards_RecomputesFromPosition()
    {
        var block = Synced((10, Chars(25)), (20, "B"));

        Assert.Equal(1.0, LyricsTiming.Fill(block, 0, S(15)));
        Assert.Equal(0.25, LyricsTiming.Fill(block, 0, S(10.5)), 6); // sin restos del estado anterior
    }

    [Fact]
    public void Fill_ZeroDuration_IsFullOnceStarted()
    {
        var block = Synced((10, "A"), (10, "B"));

        Assert.Equal(1.0, LyricsTiming.Fill(block, 0, S(10)));
    }

    [Fact]
    public void Fill_PlainLyrics_IsZero()
    {
        var block = new LyricBlock(LyricsKind.Plain, [new LyricLine(TimeSpan.Zero, "A")]);

        Assert.Equal(0, LyricsTiming.Fill(block, 0, S(5)));
    }

    [Fact]
    public void FillEnd_IsStartPlusDuration()
    {
        var block = Synced((10, Chars(25)), (20, "B"));

        Assert.Equal(S(12), LyricsTiming.FillEnd(block, 0));
    }

    // ---- Pausas instrumentales ----

    [Fact]
    public void Gaps_LongSilenceBetweenLines_IsGap()
    {
        var block = Synced((0, Chars(25)), (20, "B")); // la primera acaba en 2 s; 18 s sin letra

        var gap = Assert.Single(LyricsTiming.GetInstrumentalGaps(block));
        Assert.Equal(new InstrumentalGap(S(2), S(20)), gap);
    }

    [Fact]
    public void Gaps_ShortSilence_IsNotGap()
    {
        var block = Synced((0, Chars(25)), (9.9, "B")); // 7,9 s < 8 s

        Assert.Empty(LyricsTiming.GetInstrumentalGaps(block));
    }

    [Fact]
    public void Gaps_ExactlyThreshold_IsGap()
    {
        var block = Synced((0, Chars(25)), (10, "B")); // de 2 a 10: 8 s justos

        Assert.Single(LyricsTiming.GetInstrumentalGaps(block));
    }

    [Fact]
    public void Gaps_IntroAndOutro_UseSameThreshold()
    {
        var block = Synced((12, Chars(25)), (16, Chars(25)));

        var gaps = LyricsTiming.GetInstrumentalGaps(block, trackDuration: S(40));

        Assert.Equal([new InstrumentalGap(S(0), S(12)), new InstrumentalGap(S(18), S(40))], gaps);
    }

    [Fact]
    public void Gaps_ShortIntro_AndUnknownDuration_GiveNoGaps()
    {
        var block = Synced((2, Chars(25)), (5, Chars(25)));

        Assert.Empty(LyricsTiming.GetInstrumentalGaps(block));
    }

    [Fact]
    public void Gaps_ExplicitBlankLine_IsGapEvenIfShort()
    {
        var block = Synced((0, Chars(10)), (1, ""), (4, "B"));

        var gap = Assert.Single(LyricsTiming.GetInstrumentalGaps(block));
        Assert.Equal(new InstrumentalGap(S(1), S(4)), gap);
    }

    [Fact]
    public void Gaps_AdjacentGaps_AreMerged()
    {
        // Silencio largo hasta una línea vacía explícita, que a su vez dura hasta la siguiente.
        var block = Synced((0, Chars(25)), (15, " "), (20, "B"));

        var gap = Assert.Single(LyricsTiming.GetInstrumentalGaps(block));
        Assert.Equal(new InstrumentalGap(S(2), S(20)), gap);
    }

    [Fact]
    public void Gaps_ChicagoTimings_FindOnlyTheBreakBefore213()
    {
        // Marcas y longitudes reales de «Michael Jackson - Chicago.lrc» (texto sustituido): el intro de 2,59 s y
        // el final de ~3,9 s no llegan al umbral; la única pausa es la de antes de 02:13.00.
        var block = Synced(
            (2.59, Chars(34)), (6.37, Chars(23)), (9.91, Chars(41)),
            (119.43, Chars(7)), (121.78, Chars(21)), (133.00, Chars(40)), (137.30, Chars(28)),
            (233.68, Chars(53)), (236.39, Chars(29)), (238.69, Chars(37)));

        var gaps = LyricsTiming.GetInstrumentalGaps(block, trackDuration: S(245.5278));

        // 01:59.43 → ya no hay hueco largo; 02:01.78 + 21 × 80 ms = 02:03.46, y la siguiente a 02:13.00.
        var gap = Assert.Single(gaps.Where(g => g.Start > S(100) && g.End < S(200)));
        Assert.Equal(S(123.46), gap.Start);
        Assert.Equal(S(133.00), gap.End);
        Assert.DoesNotContain(gaps, g => g.Start < S(3));        // sin pausa de entrada
        Assert.DoesNotContain(gaps, g => g.End == S(245.5278));  // sin pausa de salida
    }

    [Fact]
    public void Gaps_PlainOrEmpty_AreEmpty()
    {
        Assert.Empty(LyricsTiming.GetInstrumentalGaps(LyricBlock.Empty));
        Assert.Empty(LyricsTiming.GetInstrumentalGaps(
            new LyricBlock(LyricsKind.Plain, [new LyricLine(TimeSpan.Zero, "A")])));
    }

    [Fact]
    public void GapAt_FindsContainingGap_AndProgress()
    {
        var block = Synced((0, Chars(25)), (20, "B")); // pausa de 2 a 20
        var gaps = LyricsTiming.GetInstrumentalGaps(block);

        Assert.Null(LyricsTiming.GapAt(gaps, S(1)));
        var gap = LyricsTiming.GapAt(gaps, S(11));
        Assert.NotNull(gap);
        Assert.Equal(0.5, gap.Progress(S(11)), 6);
        Assert.Null(LyricsTiming.GapAt(gaps, S(20))); // el final es de la línea siguiente
    }
}
