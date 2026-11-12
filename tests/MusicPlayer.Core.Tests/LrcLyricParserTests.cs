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

    [Fact]
    public void Parse_MixedLineEndingsInHeader_IgnoresHeaderAndKeepsLyrics()
    {
        // Con el formato de "Chicago.lrc" (cabecera real, letra inventada): la primera cabecera lleva espacio tras ':'
        // y termina en \n; el resto, en \r\n.
        var content = "[id: bccjinbd]\n[ti:Chicago]\r\n[ar:Michael Jackson]\r\n[ve:4.00]\r\n\r\n" +
                      "[00:02.59]Primera línea inventada\r\n[00:06.37]Segunda línea inventada\r\n";

        var block = _parser.Parse(content);

        Assert.Equal(LyricsKind.Synced, block.Kind);
        Assert.Equal(2, block.Lines.Count);
        Assert.Equal("Primera línea inventada", block.Lines[0].Text);
        Assert.Equal(TimeSpan.FromMilliseconds(2590), block.Lines[0].Timestamp);
    }

    [Fact]
    public void Parse_BlankLinesBetweenStanzas_AreDroppedNotTurnedIntoEmptyLines()
    {
        var block = _parser.Parse("[00:10.00]A\r\n\r\n[00:20.00]B\r\n\r\n\r\n[00:30.00]C");

        Assert.Equal(3, block.Lines.Count);
        Assert.All(block.Lines, l => Assert.False(string.IsNullOrWhiteSpace(l.Text)));
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

    // ---- Límites (docs/specs/untrusted-input-limits.md, criterios 4 y 6) ----

    private static string Marks(int count) =>
        string.Concat(Enumerable.Range(0, count).Select(i => $"[{i / 60:00}:{i % 60:00}.00]"));

    [Fact]
    public void Parse_ExactlyTheMaximumLeadingMarks_KeepsThemAll()
    {
        var block = _parser.Parse(Marks(LrcLyricParser.MaxLeadingMarks) + "Estribillo");

        Assert.Equal(LrcLyricParser.MaxLeadingMarks, block.Lines.Count);
    }

    [Fact]
    public void Parse_MoreLeadingMarksThanTheMaximum_UsesTheFirstOnes_AndKeepsTheTextClean()
    {
        var block = _parser.Parse(Marks(40) + "Estribillo");

        Assert.Equal(32, block.Lines.Count);
        Assert.All(block.Lines, line => Assert.Equal("Estribillo", line.Text));
        Assert.Equal(TimeSpan.FromSeconds(31), block.Lines[^1].Timestamp);
    }

    [Fact]
    public void Parse_ThousandsOfLeadingAndWordMarks_StaysBounded()
    {
        var words = string.Concat(Enumerable.Range(0, 2000).Select(i => $"<{i / 60:00}:{i % 60:00}.00>la "));

        var block = _parser.Parse(Marks(2000) + words);

        Assert.Equal(32, block.Lines.Count);
        Assert.Equal(2000, block.Lines[0].Words!.Count);
    }

    [Fact]
    public void Parse_TextWithNullCharacters_IsNotLyrics()
    {
        Assert.False(_parser.Parse("[00:01.00]Hola\n\0\0binario").IsAvailable);
        Assert.False(_parser.Parse("ID3\0\0\0cualquier cosa").IsAvailable);
    }
}