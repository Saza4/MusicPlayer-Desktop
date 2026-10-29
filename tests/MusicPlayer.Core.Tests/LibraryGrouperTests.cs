using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;

namespace MusicPlayer.Core.Tests;

public class LibraryGrouperTests
{
    private static Track T(string title, string artist = "A", string album = "X",
                       uint disc = 1, uint number = 1, uint year = 0, string albumArtist = "") =>
    new()
    {
        FilePath = $"{artist}/{album}/{title}",
        Title = title,
        Artist = artist,
        Album = album,
        DiscNumber = disc,
        TrackNumber = number,
        Year = year,
        AlbumArtist = albumArtist
    };

    [Fact]
    public void GroupAlbums_SameAlbumIgnoringCaseAndSpaces_IsOneGroup()
    {
        var albums = LibraryGrouper.GroupAlbums(new[]
        {
            T("1", artist: "Queen", album: "Opera"),
            T("2", artist: "queen ", album: " OPERA")
        });

        Assert.Equal(2, Assert.Single(albums).Tracks.Count);
    }

    [Fact]
    public void GroupAlbums_SameTitleByDifferentArtists_AreSeparate()
    {
        var albums = LibraryGrouper.GroupAlbums(new[]
        {
            T("1", artist: "A", album: "Greatest Hits"),
            T("2", artist: "B", album: "Greatest Hits")
        });

        Assert.Equal(2, albums.Count);
    }

    [Fact]
    public void GroupAlbums_TracksPlayDiscByDiscThenByTrackNumber()
    {
        var album = Assert.Single(LibraryGrouper.GroupAlbums(new[]
        {
            T("d2t1", disc: 2, number: 1),
            T("d1t2", disc: 1, number: 2),
            T("d1t1", disc: 1, number: 1)
        }));

        Assert.Equal(new[] { "d1t1", "d1t2", "d2t1" }, album.Tracks.Select(t => t.Title));
    }

    [Fact]
    public void GroupAlbums_YearIsTheEarliestKnownOrZero()
    {
        var albums = LibraryGrouper.GroupAlbums(new[]
        {
            T("1", album: "WithYears", year: 2001), T("2", album: "WithYears", year: 0),
            T("3", album: "WithYears", year: 1999),
            T("4", album: "NoYear")
        });

        Assert.Equal(1999u, albums.Single(a => a.Album == "WithYears").Year);
        Assert.Equal(0u, albums.Single(a => a.Album == "NoYear").Year);
    }

    [Fact]
    public void GroupAlbums_OfOneArtist_AreOrderedByYearWithUnknownLast()
    {
        var albums = LibraryGrouper.GroupAlbums(new[]
        {
            T("1", album: "Late", year: 2010),
            T("2", album: "Unknown"),
            T("3", album: "Early", year: 1990)
        });

        Assert.Equal(new[] { "Early", "Late", "Unknown" }, albums.Select(a => a.Album));
    }

    [Fact]
    public void GroupArtists_GroupsAlbumsAndCountsTracks()
    {
        var artists = LibraryGrouper.GroupArtists(new[]
        {
            T("1", artist: "A", album: "X"), T("2", artist: "A", album: "X"),
            T("3", artist: "A", album: "Y"),
            T("4", artist: "B", album: "Z")
        });

        var a = artists.Single(x => x.Name == "A");
        Assert.Equal(2, a.Albums.Count);
        Assert.Equal(3, a.TrackCount);
        Assert.Equal(2, artists.Count);
    }

    [Fact]
    public void GroupArtists_AreOrderedByNameIgnoringCase()
    {
        var artists = LibraryGrouper.GroupArtists(new[]
        {
            T("1", artist: "beta"), T("2", artist: "Alpha"), T("3", artist: "charlie")
        });

        Assert.Equal(new[] { "Alpha", "beta", "charlie" }, artists.Select(a => a.Name));
    }

    [Fact]
    public void EmptyInput_ReturnsEmptyLists()
    {
        Assert.Empty(LibraryGrouper.GroupAlbums(Array.Empty<Track>()));
        Assert.Empty(LibraryGrouper.GroupArtists(Array.Empty<Track>()));
    }

    [Fact]
    public void AlbumKey_IgnoresCaseAndSurroundingSpaces()
    {
        Assert.Equal(
            LibraryGrouper.AlbumKey(T("1", artist: "Queen ", album: "Opera")),
            LibraryGrouper.AlbumKey(T("2", artist: "queen", album: " OPERA")));
    }

    [Fact]
    public void GroupAlbums_Compilation_IsOneAlbumUnderTheAlbumArtistInTrackOrder()
    {
        var album = Assert.Single(LibraryGrouper.GroupAlbums(new[]
        {
        T("second", artist: "Abe", album: "Hits", number: 2, albumArtist: "Various Artists"),
        T("first", artist: "Zed", album: "Hits", number: 1, albumArtist: "Various Artists"),
    }));

        Assert.Equal("Various Artists", album.Artist);
        Assert.Equal(new[] { "first", "second" }, album.Tracks.Select(t => t.Title));
    }

    [Fact]
    public void GroupAlbums_SomeTracksWithoutAlbumArtist_SplitTheAlbum()
    {
        // Limitación conocida: la etiqueta debe estar en todas las pistas del álbum.
        var albums = LibraryGrouper.GroupAlbums(new[]
        {
        T("1", artist: "A", album: "Hits", albumArtist: "Various Artists"),
        T("2", artist: "B", album: "Hits")
    });

        Assert.Equal(2, albums.Count);
    }

    [Fact]
    public void GroupArtists_GroupsByAlbumArtist()
    {
        var artists = LibraryGrouper.GroupArtists(new[]
        {
        T("1", artist: "A", album: "Hits", albumArtist: "Various Artists"),
        T("2", artist: "B", album: "Hits", albumArtist: "Various Artists"),
        T("3", artist: "A", album: "Solo")
    });

        Assert.Equal(new[] { "A", "Various Artists" }, artists.Select(a => a.Name));
        Assert.Equal(2, artists.Single(a => a.Name == "Various Artists").TrackCount);
    }

    [Fact]
    public void AlbumKey_UsesTheAlbumArtistWhenPresent()
    {
        Assert.Equal(
            LibraryGrouper.AlbumKey(T("1", artist: "A", album: "Hits", albumArtist: "Various Artists")),
            LibraryGrouper.AlbumKey(T("2", artist: "B", album: "Hits", albumArtist: " various artists")));
    }
}