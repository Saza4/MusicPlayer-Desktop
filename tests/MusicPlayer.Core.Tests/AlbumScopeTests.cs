using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;

namespace MusicPlayer.Core.Tests;

public class AlbumScopeTests
{
    private static Track T(string file, string artist, string album, string albumArtist = "") =>
        new() { FilePath = file, Title = file, Artist = artist, Album = album, AlbumArtist = albumArtist };

    private static IEnumerable<string> Files(IEnumerable<Track> tracks) => tracks.Select(t => t.FilePath);

    [Fact]
    public void SameAlbumAndArtist_ReturnsAllItsTracks()
    {
        var one = T("1", "Ana", "Disco");
        var library = new[] { one, T("2", "Ana", "Disco"), T("3", "Ana", "Otro") };

        Assert.Equal(new[] { "1", "2" }, Files(AlbumScope.Of(one, library)));
    }

    [Fact]
    public void SameAlbumNameByAnotherArtist_IsNotMixed()
    {
        var one = T("1", "Ana", "Greatest Hits");
        var library = new[] { one, T("2", "Beto", "Greatest Hits") };

        Assert.Equal(new[] { "1" }, Files(AlbumScope.Of(one, library)));
    }

    [Fact]
    public void AlbumArtist_GroupsCompilations()
    {
        var one = T("1", "Ana", "Mix", "Various Artists");
        var library = new[] { one, T("2", "Beto", "Mix", "Various Artists") };

        Assert.Equal(new[] { "1", "2" }, Files(AlbumScope.Of(one, library)));
    }

    [Fact]
    public void UnknownAlbum_IsJustThatTrack()
    {
        var one = T("1", "Ana", Track.UnknownAlbum);
        var library = new[] { one, T("2", "Ana", Track.UnknownAlbum) };

        Assert.Equal(new[] { "1" }, Files(AlbumScope.Of(one, library)));
    }
}