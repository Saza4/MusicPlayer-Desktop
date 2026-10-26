using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;

namespace MusicPlayer.Core.Tests;

public class TrackFilterTests
{
    private static readonly Track[] Library =
    {
        new() { FilePath = "1", Title = "Bohemian Rhapsody", Artist = "Queen", Album = "A Night at the Opera" },
        new() { FilePath = "2", Title = "Canción de cuna", Artist = "Café Tacvba", Album = "Re" },
        new() { FilePath = "3", Title = "Yesterday", Artist = "The Beatles", Album = "Help!" },
    };

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Filter_EmptyQuery_ReturnsEverything(string? query)
    {
        Assert.Equal(3, TrackFilter.Filter(Library, query).Count);
    }

    [Fact]
    public void Filter_IgnoresCaseAndAccents()
    {
        var result = TrackFilter.Filter(Library, "CANCION cafe");

        Assert.Equal("2", Assert.Single(result).FilePath);
    }

    [Theory]
    [InlineData("rhapsody")] // título
    [InlineData("queen")]    // artista
    [InlineData("opera")]    // álbum
    public void Filter_MatchesTitleArtistOrAlbum(string query)
    {
        Assert.Equal("1", Assert.Single(TrackFilter.Filter(Library, query)).FilePath);
    }

    [Fact]
    public void Filter_MultipleTerms_AllMustMatchAcrossFields()
    {
        Assert.Single(TrackFilter.Filter(Library, "queen opera"));
        Assert.Empty(TrackFilter.Filter(Library, "queen yesterday"));
    }

    [Fact]
    public void Filter_NoMatch_ReturnsEmpty()
    {
        Assert.Empty(TrackFilter.Filter(Library, "zzz"));
    }
}