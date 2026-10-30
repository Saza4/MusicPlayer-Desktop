using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Tests;

public class TrackTagsTests
{
    private static readonly Track Song = new() { FilePath = "a.mp3", Title = "Old" };

    [Fact]
    public void ApplyTo_EmptyArtist_FallsBackToTheAlbumArtist()
    {
        var track = new TrackTags("T", "", "Various Artists", "Hits", 0, 0, 0).ApplyTo(Song);

        Assert.Equal("Various Artists", track.Artist);
    }

    [Fact]
    public void ApplyTo_NothingToFallBackOn_UsesPlaceholders()
    {
        var track = new TrackTags("T", "", "", "", 0, 0, 0).ApplyTo(Song);

        Assert.Equal(Track.UnknownArtist, track.Artist);
        Assert.Equal(Track.UnknownAlbum, track.Album);
    }

    [Fact]
    public void From_PlaceholdersBecomeEmptyFields()
    {
        var tags = TrackTags.From(Song); // Song usa los valores por defecto

        Assert.Equal("", tags.Artist);
        Assert.Equal("", tags.Album);
        Assert.Equal("Old", tags.Title);
    }

    [Fact]
    public void Normalized_TrimsEveryText()
    {
        var tags = new TrackTags("  T ", " A ", " AA ", " X ", 1, 2, 3).Normalized();

        Assert.Equal(new TrackTags("T", "A", "AA", "X", 1, 2, 3), tags);
    }
}