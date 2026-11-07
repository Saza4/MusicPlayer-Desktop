using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;

namespace MusicPlayer.Core.Tests;

public class LyricsTimelineTests
{
    private static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);

    private static LyricBlock Synced(params (double At, string Text)[] lines) =>
        new(LyricsKind.Synced, lines.Select(l => new LyricLine(S(l.At), l.Text)).ToList());

    private static string Chars(int count) => new('a', count);

    [Fact]
    public void Build_WithoutGaps_HasOneEntryPerLine()
    {
        var timeline = LyricsTimeline.Build(Synced((0, Chars(25)), (3, Chars(25)), (6, "C")));

        Assert.Equal([0, 1, 2], timeline.Entries.Select(e => e.LineIndex));
        Assert.All(timeline.Entries, e => Assert.Equal(TimelineEntryKind.Line, e.Kind));
    }

    [Fact]
    public void Build_InsertsDeducedGapBetweenItsLines()
    {
        var timeline = LyricsTimeline.Build(Synced((0, Chars(25)), (20, "B"))); // pausa de 2 a 20

        Assert.Equal(
            [new TimelineEntry(TimelineEntryKind.Line, 0, S(0)), new TimelineEntry(TimelineEntryKind.Gap, -1, S(2)),
             new TimelineEntry(TimelineEntryKind.Line, 1, S(20))],
            timeline.Entries);
    }

    [Fact]
    public void Build_IntroAndOutroGaps_AreEntries()
    {
        var timeline = LyricsTimeline.Build(Synced((12, Chars(25))), trackDuration: S(40));

        Assert.Equal([TimelineEntryKind.Gap, TimelineEntryKind.Line, TimelineEntryKind.Gap], timeline.Entries.Select(e => e.Kind));
        Assert.Equal(S(14), timeline.Entries[2].Start);
    }

    [Fact]
    public void Build_ZeroDuration_MeansUnknown()
    {
        var timeline = LyricsTimeline.Build(Synced((0, Chars(25))), trackDuration: TimeSpan.Zero);

        Assert.Single(timeline.Entries);
    }

    [Fact]
    public void Build_ExplicitBlankLine_IsNotDuplicatedAsGap()
    {
        var timeline = LyricsTimeline.Build(Synced((0, Chars(10)), (1, ""), (4, "B")));

        Assert.Equal(3, timeline.Entries.Count);
        Assert.All(timeline.Entries, e => Assert.Equal(TimelineEntryKind.Line, e.Kind));
    }

    [Fact]
    public void Build_PlainOrEmpty_IsEmpty()
    {
        Assert.Empty(LyricsTimeline.Build(LyricBlock.Empty).Entries);
        Assert.Empty(LyricsTimeline.Build(new LyricBlock(LyricsKind.Plain, [new LyricLine(TimeSpan.Zero, "A")])).Entries);
    }

    [Theory]
    [InlineData(-1, -1)]
    [InlineData(0, 0)]
    [InlineData(1.9, 0)]
    [InlineData(2, 1)]    // empieza la pausa
    [InlineData(19.9, 1)]
    [InlineData(20, 2)]
    [InlineData(99, 2)]
    public void ActiveEntryAt_FollowsPosition(double position, int expected)
    {
        var timeline = LyricsTimeline.Build(Synced((0, Chars(25)), (20, "B")));

        Assert.Equal(expected, timeline.ActiveEntryAt(S(position)));
    }

    [Fact]
    public void FillAt_Line_UsesLyricsTiming()
    {
        var timeline = LyricsTimeline.Build(Synced((10, Chars(25)), (20, "B")));

        // Antes de la línea hay una pausa de entrada de 10 s: la línea es la entrada 1.
        Assert.Equal(TimelineEntryKind.Gap, timeline.Entries[0].Kind);
        Assert.Equal(0.5, timeline.FillAt(1, S(11)), 6);
        Assert.Equal(0, timeline.FillAt(1, S(9)));
    }

    [Fact]
    public void FillAt_Gap_IsGapProgress()
    {
        var timeline = LyricsTimeline.Build(Synced((0, Chars(25)), (20, "B"))); // pausa de 2 a 20

        Assert.Equal(0, timeline.FillAt(1, S(2)), 6);
        Assert.Equal(0.5, timeline.FillAt(1, S(11)), 6);
        Assert.Equal(1, timeline.FillAt(1, S(25)), 6);
    }

    [Fact]
    public void FillAt_BlankLine_IsProgressOfItsGap()
    {
        var timeline = LyricsTimeline.Build(Synced((0, Chars(10)), (1, ""), (5, "B"))); // pausa explícita de 1 a 5

        Assert.Equal(0.5, timeline.FillAt(1, S(3)), 6);
    }

    [Fact]
    public void FillAt_InvalidIndex_IsZero()
    {
        var timeline = LyricsTimeline.Build(Synced((0, "A")));

        Assert.Equal(0, timeline.FillAt(-1, S(1)));
        Assert.Equal(0, timeline.FillAt(5, S(1)));
    }

    [Theory]
    [InlineData(-5, 0)]
    [InlineData(0, 2)]   // la pausa
    [InlineData(2, 20)]
    [InlineData(15, 20)]
    public void NextStartAfter_IsNextEntryStart(double position, double expected)
    {
        var timeline = LyricsTimeline.Build(Synced((0, Chars(25)), (20, "B")));

        Assert.Equal(S(expected), timeline.NextStartAfter(S(position)));
    }

    [Fact]
    public void NextStartAfter_SkipsEntriesSharingTheSameStart_AndIsNullAtTheEnd()
    {
        var timeline = LyricsTimeline.Build(Synced((0, "A"), (5, "B"), (5, "C"), (7, "D")));

        Assert.Equal(S(7), timeline.NextStartAfter(S(5)));
        Assert.Null(timeline.NextStartAfter(S(7)));
    }

    [Fact]
    public void ChicagoTimings_GapEntryGoesAfterItsLine()
    {
        // Marcas y longitudes reales de «Michael Jackson - Chicago.lrc» (texto sustituido) alrededor de la pausa.
        var timeline = LyricsTimeline.Build(
            Synced((116.80, Chars(30)), (119.43, Chars(7)), (121.78, Chars(21)), (133.00, Chars(40)), (137.30, Chars(28))));

        // La entrada 0 es la pausa de entrada (el fragmento empieza a 116,8 s); la otra es la de antes de 02:13.
        var gap = Assert.Single(timeline.Entries, e => e.Kind == TimelineEntryKind.Gap && e.Start > S(100));
        Assert.Equal(4, timeline.Entries.ToList().IndexOf(gap)); // tras la línea de 02:01.78
        Assert.Equal(S(123.46), gap.Start);
        Assert.Equal(4, timeline.ActiveEntryAt(S(128)));
        Assert.Equal(5, timeline.ActiveEntryAt(S(133)));
    }
}
