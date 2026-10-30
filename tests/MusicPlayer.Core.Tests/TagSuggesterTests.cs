using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;

namespace MusicPlayer.Core.Tests;

public class TagSuggesterTests
{
    private static Track T(string file, string? title = null, string artist = Track.UnknownArtist,
        string album = Track.UnknownAlbum, uint number = 0, string albumArtist = "", string dir = "music") =>
        new()
        {
            FilePath = Path.Combine(dir, file),
            Title = title ?? Path.GetFileNameWithoutExtension(file), // como el lector cuando falta la etiqueta
            Artist = artist,
            Album = album,
            TrackNumber = number,
            AlbumArtist = albumArtist
        };

    private static TrackTags After(Track track, TagSuggestOptions? options = null) =>
        TagSuggester.Suggest(new[] { track }, options)[0].After;

    [Fact]
    public void UntaggedFile_YieldsArtistAndTitle_WithoutRipperJunk()
    {
        var after = After(T("Naruto_-_Sadness_and_Sorrow_KLICKAUD.mp3"));

        Assert.Equal("Naruto", after.Artist);
        Assert.Equal("Sadness and Sorrow", after.Title);
    }

    [Fact]
    public void LeadingTrackNumber_IsExtracted()
    {
        var after = After(T("01 - Hello.mp3"));

        Assert.Equal("Hello", after.Title);
        Assert.Equal(1u, after.TrackNumber);
        Assert.Equal("", after.Artist);
    }

    [Fact]
    public void NumberArtistTitle_AreAllExtracted()
    {
        var after = After(T("03. Queen - Song.mp3"));

        Assert.Equal(3u, after.TrackNumber);
        Assert.Equal("Queen", after.Artist);
        Assert.Equal("Song", after.Title);
    }

    [Fact]
    public void ExistingArtist_IsNeverOverwritten()
    {
        var after = After(T("Naruto - Sadness KLICKAUD.mp3", artist: "Toshio"));

        Assert.Equal("Toshio", after.Artist);
        Assert.Equal("Naruto - Sadness", after.Title);
    }

    [Fact]
    public void ExistingTitle_IsKept_ButAMissingTrackNumberIsFilled()
    {
        var after = After(T("01 - Other.mp3", title: "Real Title", artist: "A", album: "X"));

        Assert.Equal("Real Title", after.Title);
        Assert.Equal(1u, after.TrackNumber);
    }

    [Fact]
    public void CompleteTrack_ProducesNoSuggestion()
    {
        Assert.Empty(TagSuggester.Suggest(new[] { T("b.mp3", "Real", "A", "X", 1) }));
    }

    [Fact]
    public void JunkIsRemovedFromAnExistingTitle()
    {
        Assert.Equal("Song", After(T("b.mp3", "Song (Official Video)", "A", "X", 1)).Title);
    }

    [Fact]
    public void AlbumFromFolder_IsOffByDefault()
    {
        Assert.Empty(TagSuggester.Suggest(new[] { T("a.mp3", "Real", "A", dir: "Greatest") }));
    }

    [Fact]
    public void AlbumFromFolder_UsesTheFolderNameWhenEnabled()
    {
        var after = After(T("a.mp3", "Real", "A", dir: "Greatest"), new TagSuggestOptions(AlbumFromFolder: true));

        Assert.Equal("Greatest", after.Album);
    }

    [Fact]
    public void Compilation_WithManyArtistsAndNoAlbumArtist_GetsVariousArtists()
    {
        var result = TagSuggester.Suggest(new[]
        {
            T("1.mp3", "One", "A", "Hits", 1, dir: "comp"),
            T("2.mp3", "Two", "B", "Hits", 2, dir: "comp"),
            T("3.mp3", "Three", "C", "Hits", 3, dir: "comp")
        });

        Assert.Equal(3, result.Count);
        Assert.All(result, s => Assert.Equal(TagSuggester.VariousArtists, s.After.AlbumArtist));
    }

    [Fact]
    public void Compilation_PartiallyLabelled_FillsTheRest()
    {
        var result = TagSuggester.Suggest(new[]
        {
            T("1.mp3", "One", "A", "Hits", 1, "Various Artists", "comp"),
            T("2.mp3", "Two", "B", "Hits", 2, dir: "comp"),
            T("3.mp3", "Three", "C", "Hits", 3, dir: "comp")
        });

        Assert.Equal(2, result.Count); // la primera ya estaba bien
        Assert.All(result, s => Assert.Equal("Various Artists", s.After.AlbumArtist));
    }

    [Fact]
    public void RegularAlbumBySingleArtist_IsLeftAlone()
    {
        Assert.Empty(TagSuggester.Suggest(new[]
        {
            T("1.mp3", "One", "A", "X", 1), T("2.mp3", "Two", "A", "X", 2), T("3.mp3", "Three", "A", "X", 3)
        }));
    }

    [Fact]
    public void ExtraWhitespace_IsNormalised()
    {
        var after = After(T("x.mp3", "  A   B ", "C  D", "X", 1));

        Assert.Equal("A B", after.Title);
        Assert.Equal("C D", after.Artist);
    }
}