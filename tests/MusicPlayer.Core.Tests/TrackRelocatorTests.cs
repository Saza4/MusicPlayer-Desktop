using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;

namespace MusicPlayer.Core.Tests;

public class TrackRelocatorTests
{
    private static Track T(string path, string title = "Song", string artist = "A",
                           string album = "X", double seconds = 200) =>
        new()
        {
            FilePath = path,
            Title = title,
            Artist = artist,
            Album = album,
            Duration = TimeSpan.FromSeconds(seconds)
        };

    [Fact]
    public void Match_UniquePairWithSameMetadata_IsRelocated()
    {
        var old = T("old/song.mp3");
        var moved = T("new/song.mp3");

        var pair = Assert.Single(TrackRelocator.Match(new[] { old }, new[] { moved }));

        Assert.Same(old, pair.Old);
        Assert.Same(moved, pair.New);
    }

    [Fact]
    public void Match_IgnoresCaseAndSurroundingSpaces()
    {
        var result = TrackRelocator.Match(
            new[] { T("old", title: "Song", artist: "Queen") },
            new[] { T("new", title: " SONG ", artist: "queen") });

        Assert.Single(result);
    }

    [Fact]
    public void Match_DurationWithinTolerance_IsRelocated()
    {
        Assert.Single(TrackRelocator.Match(
            new[] { T("old", seconds: 200) }, new[] { T("new", seconds: 200.8) }));
    }

    [Fact]
    public void Match_DurationBeyondTolerance_IsNotRelocated()
    {
        Assert.Empty(TrackRelocator.Match(
            new[] { T("old", seconds: 200) }, new[] { T("new", seconds: 230) }));
    }

    [Fact]
    public void Match_DifferentAlbum_IsNotRelocated()
    {
        Assert.Empty(TrackRelocator.Match(
            new[] { T("old", album: "Studio") }, new[] { T("new", album: "Live") }));
    }

    [Fact]
    public void Match_TwoCandidatesForOneMissing_IsAmbiguousAndSkipped()
    {
        Assert.Empty(TrackRelocator.Match(
            new[] { T("old") }, new[] { T("new1"), T("new2") }));
    }

    [Fact]
    public void Match_TwoMissingForOneCandidate_IsAmbiguousAndSkipped()
    {
        Assert.Empty(TrackRelocator.Match(
            new[] { T("old1"), T("old2") }, new[] { T("new") }));
    }
}