using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;

namespace MusicPlayer.Core.Tests;

public class LyricsLayerNamesTests
{
    private static readonly string Dir = Path.Combine("musica", "album");
    private static string In(string name) => Path.Combine(Dir, name);

    [Theory]
    [InlineData(LyricsLayerKind.Translation, "es", "LYRICS_TRANSLATION_ES")]
    [InlineData(LyricsLayerKind.Translation, "pt-BR", "LYRICS_TRANSLATION_PT-BR")]
    [InlineData(LyricsLayerKind.Translation, "zh-Hans", "LYRICS_TRANSLATION_ZH-HANS")]
    [InlineData(LyricsLayerKind.Pronunciation, null, "LYRICS_PRONUNCIATION")]
    public void FieldName_IsUppercase(LyricsLayerKind kind, string? language, string expected) =>
        Assert.Equal(expected, LyricsLayerNames.FieldName(kind, language));

    [Theory]
    [InlineData("LYRICS_TRANSLATION_ES", LyricsLayerKind.Translation, "es")]
    [InlineData("lyrics_translation_es", LyricsLayerKind.Translation, "es")]
    [InlineData("LYRICS_TRANSLATION_PT-BR", LyricsLayerKind.Translation, "pt-BR")]
    [InlineData("Lyrics_Translation_zh-hans", LyricsLayerKind.Translation, "zh-Hans")]
    [InlineData("LYRICS_PRONUNCIATION", LyricsLayerKind.Pronunciation, null)]
    [InlineData("lyrics_pronunciation", LyricsLayerKind.Pronunciation, null)]
    public void TryParseField_IsCaseInsensitive_AndNormalizesTheCode(string name, LyricsLayerKind kind, string? language)
    {
        Assert.True(LyricsLayerNames.TryParseField(name, out var k, out var l));
        Assert.Equal(kind, k);
        Assert.Equal(language, l);
    }

    [Theory]
    [InlineData("LYRICS")]
    [InlineData("LYRICS_TRANSLATION_")]
    [InlineData("LYRICS_TRANSLATION_LIVE")]
    [InlineData("LYRICIST")]
    [InlineData("ENCODER")]
    public void TryParseField_RejectsOtherFields(string name) => Assert.False(LyricsLayerNames.TryParseField(name, out _, out _));

    [Fact]
    public void FieldNamesRoundTrip()
    {
        foreach (var code in new[] { "es", "pt-BR", "zh-Hans", "fil" })
        {
            Assert.True(LyricsLayerNames.TryParseField(LyricsLayerNames.FieldName(LyricsLayerKind.Translation, code), out _, out var back));
            Assert.Equal(code, back);
        }
    }

    [Fact]
    public void FindSiblings_TakesTranslationsAndPronunciation_InOrder()
    {
        var files = new[] { "song.mp3", "song.lrc", "song.rom.lrc", "song.es.lrc", "song.en.lrc", "song.pt-BR.lrc", "otra.es.lrc" }.Select(In);

        var found = LyricsLayerNames.FindSiblings(In("song.mp3"), files, "es");

        Assert.Equal(
            [new LyricsLayerSource(LyricsLayerKind.Pronunciation, null, In("song.rom.lrc")),
             new LyricsLayerSource(LyricsLayerKind.Translation, "en", In("song.en.lrc")),
             new LyricsLayerSource(LyricsLayerKind.Translation, "es", In("song.es.lrc")),
             new LyricsLayerSource(LyricsLayerKind.Translation, "pt-BR", In("song.pt-BR.lrc"))],
            found);
    }

    [Fact]
    public void FindSiblings_IgnoresWhatIsNotALanguage()
    {
        var files = new[] { "song.alt.lrc", "song.mix.lrc", "song.live.lrc", "song.fil.lrc", "song.es-latino.lrc", "song.x.es.lrc", "song.es.txt" }.Select(In);

        Assert.Empty(LyricsLayerNames.FindSiblings(In("song.mp3"), files, "es"));
    }

    [Fact]
    public void FindSiblings_KeepsTodaysThreeLetterSystemCode()
    {
        var found = LyricsLayerNames.FindSiblings(In("song.mp3"), [In("song.fil.lrc")], "fil");

        Assert.Equal("fil", Assert.Single(found).Language);
    }

    [Fact]
    public void FindSiblings_IsCaseInsensitive_AndTakesARepeatedCodeOnce()
    {
        var files = new[] { "Song.ES.lrc", "song.es.lrc", "SONG.ROM.LRC" }.Select(In);

        var found = LyricsLayerNames.FindSiblings(In("song.mp3"), files, "es");

        Assert.Equal(2, found.Count);
        Assert.Equal(In("SONG.ROM.LRC"), found[0].Path);
        Assert.Equal(("es", In("Song.ES.lrc")), (found[1].Language, found[1].Path)); // el primero en orden ordinal
    }

    [Fact]
    public void FindSiblings_NameWithDots_UsesTheWholeStem()
    {
        var files = new[] { "tema.en vivo.es.lrc", "tema.es.lrc" }.Select(In);

        var found = LyricsLayerNames.FindSiblings(In("tema.en vivo.mp3"), files, "es");

        Assert.Equal(In("tema.en vivo.es.lrc"), Assert.Single(found).Path);
    }

    [Fact]
    public void FindSiblings_FromAPickedLyricsFile_UsesItsName()
    {
        var files = new[] { "elegida.lrc", "elegida.es.lrc", "song.es.lrc" }.Select(In);

        var found = LyricsLayerNames.FindSiblings(In("elegida.lrc"), files, "es");

        Assert.Equal(In("elegida.es.lrc"), Assert.Single(found).Path);
    }
}
