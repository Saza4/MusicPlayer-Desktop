using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;

namespace MusicPlayer.Core.Tests;

// docs/specs/lrc-enhanced.md. Letra inventada: no se suben letras reales.
public class LrcEnhancedParserTests
{
    private readonly LrcLyricParser _parser = new();

    private static TimeSpan T(double seconds) => TimeSpan.FromSeconds(seconds);

    private static string Slice(LyricLine line, LyricWord w) => line.Text.Substring(w.Offset, w.Text.Length);

    [Fact]
    public void SimpleLine_WithEnd_GivesCleanTextWordsAndEnd()
    {
        var line = _parser.Parse("[00:10.00]<00:10.00>Uno <00:10.50>dos <00:11.00>tres <00:11.80>").Lines[0];

        Assert.Equal("Uno dos tres", line.Text);
        Assert.Equal(T(11.8), line.End);
        Assert.Equal(3, line.Words!.Count);
        Assert.Equal(new[] { "Uno", "dos", "tres" }, line.Words.Select(w => w.Text));
        Assert.Equal(new[] { T(10), T(10.5), T(11) }, line.Words.Select(w => w.Start));
        Assert.Equal(new TimeSpan?[] { T(10.5), T(11), T(11.8) }, line.Words.Select(w => w.End));
        Assert.All(line.Words!, w => Assert.Equal(w.Text, Slice(line, w)));
    }

    [Fact]
    public void SimpleLine_WithoutEnd_LastWordHasNoEnd()
    {
        var line = _parser.Parse("[00:10.00]<00:10.00>Uno <00:10.50>dos").Lines[0];

        Assert.Equal("Uno dos", line.Text);
        Assert.Null(line.End);
        Assert.Null(line.Words![^1].End);
    }

    [Fact]
    public void Syllables_WithoutSpace_AreKeptTogetherInText()
    {
        var line = _parser.Parse("[00:52.00]<00:52.00>Hel<00:52.30>lo <00:52.60>mun<00:52.90>do").Lines[0];

        Assert.Equal("Hello mundo", line.Text);
        Assert.Equal(new[] { "Hel", "lo", "mun", "do" }, line.Words!.Select(w => w.Text));
        Assert.All(line.Words!, w => Assert.Equal(w.Text, Slice(line, w)));
    }

    [Fact]
    public void TextBeforeFirstTag_IsTheFirstWordAtTheLineTime()
    {
        var line = _parser.Parse("[00:41.65]Una <00:41.85>dos <00:42.26>tres").Lines[0];

        Assert.Equal("Una dos tres", line.Text);
        Assert.Equal(T(41.65), line.Words![0].Start);
        Assert.Equal("Una", line.Words[0].Text);
        Assert.Equal(0, line.Words[0].Offset);
    }

    [Fact]
    public void TimeLikeTextWithoutLineMark_IsLeftAlone()
    {
        var plain = _parser.Parse("Son las <12:30> en punto\nOtra línea");
        Assert.Equal(LyricsKind.Plain, plain.Kind);
        Assert.Equal("Son las <12:30> en punto", plain.Lines[0].Text);

        // Una línea sin marca en un archivo sincronizado se descarta, como siempre, sin interpretarla.
        var synced = _parser.Parse("[00:01.00]Hola\nSon las <12:30> en punto");
        Assert.Single(synced.Lines);
        Assert.Null(synced.Lines[0].Words);
    }

    [Fact]
    public void InvalidTagInTheMiddle_StaysAsTextAndRestOfLineIsRead()
    {
        var line = _parser.Parse("[00:12.00]<00:12.00>Oh <00:99.00>it <00:13.55>was").Lines[0];

        Assert.Equal("Oh <00:99.00>it was", line.Text);
        Assert.Equal(2, line.Words!.Count);
        Assert.Equal("Oh <00:99.00>it", line.Words[0].Text);
        Assert.Equal("was", line.Words[1].Text);
        Assert.All(line.Words!, w => Assert.Equal(w.Text, Slice(line, w)));
    }

    [Theory]
    [InlineData("<3> y <hola> <١٢:٣٠>")]
    [InlineData("<00:75.00> mal")]
    public void NotATag_IsLiteralText(string body)
    {
        var line = _parser.Parse("[00:05.00]" + body).Lines[0];

        Assert.Equal(body.Trim(), line.Text);
        Assert.Null(line.Words);
    }

    [Fact]
    public void MixedEnhancedAndNormalLines_AreReadSeparately()
    {
        var block = _parser.Parse("[00:01.00]Normal\n[00:02.00]<00:02.00>Con <00:02.40>marcas\n[00:03.00]Otra normal");

        Assert.Equal(new[] { "Normal", "Con marcas", "Otra normal" }, block.Lines.Select(l => l.Text));
        Assert.Null(block.Lines[0].Words);
        Assert.Equal(2, block.Lines[1].Words!.Count);
        Assert.Null(block.Lines[2].Words);
    }

    [Fact]
    public void SeveralLineMarks_ShiftWordsAndEndByTheDifferenceFromTheFirstMark()
    {
        var block = _parser.Parse("[00:10.00][00:30.00]<00:10.00>Uno <00:10.50>dos <00:11.00>");

        Assert.Equal(2, block.Lines.Count);
        var first = block.Lines[0];
        var second = block.Lines[1];
        Assert.Equal(T(10), first.Timestamp);
        Assert.Equal(T(10), first.Words![0].Start);
        Assert.Equal(T(11), first.End);
        Assert.Equal(T(30), second.Timestamp);
        Assert.Equal(T(30), second.Words![0].Start);
        Assert.Equal(T(30.5), second.Words[1].Start);
        Assert.Equal(T(31), second.End);
        Assert.Equal(T(31), second.Words[1].End);
    }

    [Fact]
    public void WordsBeforeTheLineOrOutOfOrder_AreKeptUntouched()
    {
        var line = _parser.Parse("[00:48.29]So <00:48.07>beat <00:47.00>it <00:49.00>now").Lines[0];

        Assert.Equal(T(48.29), line.Words![0].Start);
        Assert.Equal(T(48.07), line.Words[1].Start);
        Assert.Equal(T(47), line.Words[2].Start);
        Assert.Equal(T(49), line.Words[3].Start);
    }

    [Fact]
    public void OnlyAFinalTag_IsTheEndOfAnEmptyLine()
    {
        var line = _parser.Parse("[00:10.00]<00:11.00>").Lines[0];

        Assert.Equal("", line.Text);
        Assert.Null(line.Words);
        Assert.Equal(T(11), line.End);
    }

    [Fact]
    public void TwoTagsInARow_DropTheFirst()
    {
        var line = _parser.Parse("[00:12.00]<00:12.00><00:13.00>hola <00:14.00>adiós").Lines[0];

        Assert.Equal("hola adiós", line.Text);
        Assert.Equal(T(13), line.Words![0].Start);
        Assert.Equal(T(14), line.Words[1].Start);
    }

    [Fact]
    public void LineWithoutTags_IsExactlyAsBefore()
    {
        var line = _parser.Parse("[00:12.00] Hola  mundo ").Lines[0];

        Assert.Equal("Hola  mundo", line.Text);
        Assert.Null(line.Words);
        Assert.Null(line.End);
    }

    [Fact]
    public void EnhancedTranslation_MergesByCleanText()
    {
        var original = _parser.Parse("[00:10.00]<00:10.00>Uno <00:10.50>dos");
        var translation = _parser.Parse("[00:10.00]<00:10.00>One <00:10.50>two");

        var merged = LyricsMerger.Merge(original, translation, null);

        Assert.Equal("One two", merged.Lines[0].Translation);
        Assert.Equal("Uno dos", merged.Lines[0].Text);
        Assert.Equal(2, merged.Lines[0].Words!.Count);
    }

    [Fact]
    public void SyncedMarksAreStillUsedForTheActiveLine()
    {
        var block = _parser.Parse("[00:10.00]<00:10.00>Uno <00:10.50>dos\n[00:20.00]<00:20.00>Tres");

        Assert.Equal(LyricsKind.Synced, block.Kind);
        Assert.Equal(0, block.GetActiveLineIndex(T(15)));
        Assert.Equal(1, block.GetActiveLineIndex(T(25)));
    }
}
