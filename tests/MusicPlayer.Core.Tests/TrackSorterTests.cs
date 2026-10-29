using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;

namespace MusicPlayer.Core.Tests;

public class TrackSorterTests
{
    private static Track T(string title, string artist = "A", string album = "X", uint disc = 1, uint number = 1) =>
        new() { FilePath = title, Title = title, Artist = artist, Album = album, DiscNumber = disc, TrackNumber = number };

    [Fact]
    public void Sort_AlbumPlaysDiscByDiscThenByTrackNumber()
    {
        var sorted = TrackSorter.Sort(new[]
        {
            T("d2t1", disc: 2, number: 1),
            T("d1t2", disc: 1, number: 2),
            T("d1t1", disc: 1, number: 1)
        });

        Assert.Equal(new[] { "d1t1", "d1t2", "d2t1" }, sorted.Select(t => t.Title));
    }

    [Fact]
    public void Sort_MissingDiscNumberCountsAsDiscOne()
    {
        var sorted = TrackSorter.Sort(new[]
        {
            T("d2", disc: 2, number: 1),
            T("none", disc: 0, number: 2),
            T("d1", disc: 1, number: 1)
        });

        Assert.Equal(new[] { "d1", "none", "d2" }, sorted.Select(t => t.Title));
    }

    [Fact]
    public void Sort_GroupsByArtistThenAlbumIgnoringCase()
    {
        var sorted = TrackSorter.Sort(new[]
        {
            T("1", artist: "beta"),
            T("2", artist: "Alpha", album: "Z"),
            T("3", artist: "Alpha", album: "y")
        });

        Assert.Equal(new[] { "3", "2", "1" }, sorted.Select(t => t.Title));
    }

    [Fact]
    public void Sort_CompilationFollowsTrackNumberNotTrackArtist()
    {
        Track C(string title, string artist, uint number) => new()
        {
            FilePath = title,
            Title = title,
            Artist = artist,
            Album = "Hits",
            AlbumArtist = "Various Artists",
            TrackNumber = number
        };

        var sorted = TrackSorter.Sort(new[] { C("second", "Abe", 2), C("first", "Zed", 1) });

        Assert.Equal(new[] { "first", "second" }, sorted.Select(t => t.Title));
    }
}