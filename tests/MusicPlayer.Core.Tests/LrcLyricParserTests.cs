using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;

namespace MusicPlayer.Core.Tests;

public class LrcLyricParserTests
{
    private readonly LrcLyricParser _parser = new();

    [Fact]
    public void Parse_SyncedLrc_ReturnsTimestamps()
    {
        var block = _parser.Parse("[00:12.00]Hola\n[00:15.50]Mundo");

        Assert.Equal(LyricsKind.Synced, block.Kind);
        Assert.Equal(2, block.Lines.Count);
        Assert.Equal(TimeSpan.FromSeconds(12), block.Lines[0].Timestamp);
        Assert.Equal(TimeSpan.FromMilliseconds(15500), block.Lines[1].Timestamp);
        Assert.Equal("Mundo", block.Lines[1].Text);
    }

    [Fact]
    public void Parse_MultipleTagsOnOneLine_CreatesOneLinePerTag()
    {
        var block = _parser.Parse("[00:10.00][00:30.00]Estribillo");

        Assert.Equal(2, block.Lines.Count);
        Assert.All(block.Lines, l => Assert.Equal("Estribillo", l.Text));
    }

    [Fact]
    public void Parse_IgnoresMetadataTags()
    {
        var block = _parser.Parse("[ti:Canción]\n[ar:Artista]\n[00:01.00]Línea");

        Assert.Single(block.Lines);
    }

    [Fact]
    public void Parse_PlainText_ReturnsPlainBlock()
    {
        var block = _parser.Parse("Primera línea\nSegunda línea");

        Assert.Equal(LyricsKind.Plain, block.Kind);
        Assert.Equal(2, block.Lines.Count);
        Assert.True(block.IsAvailable);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n  ")]
    public void Parse_EmptyContent_ReturnsUnavailable(string content)
    {
        var block = _parser.Parse(content);

        Assert.False(block.IsAvailable);
    }

    [Theory]
    [InlineData(0, -1)]
    [InlineData(10, 0)]
    [InlineData(14, 0)]
    [InlineData(15, 1)]
    [InlineData(99, 1)]
    public void GetActiveLineIndex_ReturnsExpectedLine(int seconds, int expected)
    {
        var block = _parser.Parse("[00:10.00]A\n[00:15.00]B");

        Assert.Equal(expected, block.GetActiveLineIndex(TimeSpan.FromSeconds(seconds)));
    }
}