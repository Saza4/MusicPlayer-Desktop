using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;

namespace MusicPlayer.Core.Tests;

public class LyricsMergerTests
{
    private static LyricBlock Synced(params (double Seconds, string Text)[] lines) =>
        new(LyricsKind.Synced,
            lines.Select(l => new LyricLine(TimeSpan.FromSeconds(l.Seconds), l.Text)).ToList());

    private static LyricBlock Plain(params string[] lines) =>
        new(LyricsKind.Plain, lines.Select(l => new LyricLine(TimeSpan.Zero, l)).ToList());

    [Fact]
    public void Merge_Synced_MatchesByTimestampWithinTolerance()
    {
        var original = Synced((10, "Hello"), (15, "World"));
        var translation = Synced((10, "Hola"), (15.1, "Mundo")); // 100 ms de diferencia

        var merged = LyricsMerger.Merge(original, translation, null);

        Assert.Equal(LyricsKind.Synced, merged.Kind);
        Assert.Equal("Hello", merged.Lines[0].Text);
        Assert.Equal("Hola", merged.Lines[0].Translation);
        Assert.Equal("Mundo", merged.Lines[1].Translation);
        Assert.Null(merged.Lines[0].Romanization);
    }

    [Fact]
    public void Merge_Synced_BeyondTolerance_LeavesNull()
    {
        var merged = LyricsMerger.Merge(Synced((10, "Hello")), Synced((10.5, "Hola")), null);

        Assert.Null(merged.Lines[0].Translation);
    }

    [Fact]
    public void Merge_Synced_PicksNearestCandidate()
    {
        var merged = LyricsMerger.Merge(
            Synced((10.15, "Hello")),
            Synced((10.0, "A"), (10.2, "B")),
            null);

        Assert.Equal("B", merged.Lines[0].Translation);
    }

    [Fact]
    public void Merge_Plain_MatchesByIndexWhenCountsEqual()
    {
        var merged = LyricsMerger.Merge(Plain("one", "two"), Plain("uno", "dos"), Plain("wan", "tu"));

        Assert.Equal("dos", merged.Lines[1].Translation);
        Assert.Equal("wan", merged.Lines[0].Romanization);
    }

    [Fact]
    public void Merge_Plain_DifferentCount_LeavesNull()
    {
        var merged = LyricsMerger.Merge(Plain("one", "two"), Plain("uno"), null);

        Assert.All(merged.Lines, l => Assert.Null(l.Translation));
    }

    [Fact]
    public void Merge_KindMismatch_LeavesNull()
    {
        var merged = LyricsMerger.Merge(Synced((1, "Hello")), Plain("Hola"), null);

        Assert.Null(merged.Lines[0].Translation);
    }

    [Fact]
    public void Merge_NoCompanions_KeepsOriginalIntact()
    {
        var original = Synced((1, "Hello"));

        var merged = LyricsMerger.Merge(original, null, null);

        Assert.Equal("Hello", merged.Lines[0].Text);
        Assert.Null(merged.Lines[0].Translation);
        Assert.True(merged.IsAvailable);
    }

    [Fact]
    public void Merge_BlankCompanionText_IsIgnored()
    {
        var merged = LyricsMerger.Merge(Synced((1, "Hello")), Synced((1, "")), null);

        Assert.Null(merged.Lines[0].Translation);
    }
}