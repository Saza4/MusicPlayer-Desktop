using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;

namespace MusicPlayer.Core.Tests;

public class BrowseScopeTests
{
    private static Track T(string title, string artist = "Ana", string album = "Disco", string albumArtist = "") =>
        new() { FilePath = $"{artist}/{album}/{title}", Title = title, Artist = artist, Album = album, AlbumArtist = albumArtist };

    private static string Key(string artist, string album) =>
        LibraryGrouper.AlbumKey(T("x", artist, album));

    [Fact]
    public void NothingOpen_StaysClosed()
    {
        var one = T("1");
        Assert.Equal((null, null), BrowseScope.Resolve(null, null, new[] { one.Id }, new[] { one }));
    }

    [Fact]
    public void OnlyTitleChanged_KeepsArtistAndAlbum()
    {
        var before = T("1");
        var after = before with { Title = "Otro título" };

        var (artist, album) = BrowseScope.Resolve("Ana", Key("Ana", "Disco"), new[] { before.Id }, new[] { after });

        Assert.Equal("Ana", artist);
        Assert.Equal(Key("Ana", "Disco"), album);
    }

    [Fact]
    public void OneTrackLeavesAnAlbumThatStillHasOthers_StaysInTheAlbum()
    {
        var moved = T("1");
        var stays = T("2");
        var library = new[] { moved with { Album = "Otro" }, stays };

        var (_, album) = BrowseScope.Resolve(null, Key("Ana", "Disco"), new[] { moved.Id, stays.Id }, library);

        Assert.Equal(Key("Ana", "Disco"), album);
    }

    [Fact]
    public void AlbumRenamed_FollowsTheFirstShownTrack()
    {
        var one = T("1");
        var two = T("2");
        var library = new[] { two with { Album = "Disco (Remaster)" }, one with { Album = "Disco (Remaster)" } };

        var (artist, album) = BrowseScope.Resolve("Ana", Key("Ana", "Disco"), new[] { one.Id, two.Id }, library);

        Assert.Equal("Ana", artist);
        Assert.Equal(Key("Ana", "Disco (Remaster)"), album);
    }

    [Fact]
    public void AlbumMovedToAnotherArtist_ArtistFollowsToo()
    {
        var one = T("1");
        var library = new[] { one with { AlbumArtist = "Varios artistas" } };

        var (artist, album) = BrowseScope.Resolve("Ana", Key("Ana", "Disco"), new[] { one.Id }, library);

        Assert.Equal("Varios artistas", artist);
        Assert.Equal(Key("Varios artistas", "Disco"), album);
    }

    [Fact]
    public void AlbumOpenWithoutArtist_StaysWithoutArtist()
    {
        var one = T("1");
        var library = new[] { one with { Album = "Nuevo" } };

        var (artist, album) = BrowseScope.Resolve(null, Key("Ana", "Disco"), new[] { one.Id }, library);

        Assert.Null(artist);
        Assert.Equal(Key("Ana", "Nuevo"), album);
    }

    [Fact]
    public void ArtistRenamed_FollowsTheTrack()
    {
        var one = T("1");
        var library = new[] { one with { Artist = "Ana María" } };

        var (artist, album) = BrowseScope.Resolve("Ana", null, new[] { one.Id }, library);

        Assert.Equal("Ana María", artist);
        Assert.Null(album);
    }

    [Fact]
    public void ArtistMatchIgnoresCaseAndSpaces()
    {
        var one = T("1", artist: " ana ");

        var (artist, _) = BrowseScope.Resolve("Ana", null, new[] { one.Id }, new[] { one });

        Assert.Equal("Ana", artist);
    }

    [Fact]
    public void AlbumGoneAndNoShownTrackLeft_ArtistStaysIfItStillExists()
    {
        var gone = T("1");
        var other = T("2", album: "Otro");

        var (artist, album) = BrowseScope.Resolve("Ana", Key("Ana", "Disco"), new[] { gone.Id }, new[] { other });

        Assert.Equal("Ana", artist);
        Assert.Null(album);
    }

    [Fact]
    public void EverythingGone_ClosesBoth()
    {
        var gone = T("1");
        var other = T("2", artist: "Beto");

        Assert.Equal((null, null),
            BrowseScope.Resolve("Ana", Key("Ana", "Disco"), new[] { gone.Id }, new[] { other }));
    }
}
