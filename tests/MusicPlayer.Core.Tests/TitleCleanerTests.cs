using MusicPlayer.Core.Services;

namespace MusicPlayer.Core.Tests;

public class TitleCleanerTests
{
    [Theory]
    [InlineData("Song (Official Video)", "Song")]
    [InlineData("Song [Official Audio]", "Song")]
    [InlineData("Song [HD]", "Song")]
    [InlineData("Song - KLICKAUD", "Song")]
    [InlineData("Song (Remastered 2011)", "Song (Remastered 2011)")]
    [InlineData("  A   B ", "A B")]
    public void Clean_RemovesDownloadJunkAndKeepsRealInformation(string input, string expected)
    {
        Assert.Equal(expected, TitleCleaner.Clean(input));
    }
}