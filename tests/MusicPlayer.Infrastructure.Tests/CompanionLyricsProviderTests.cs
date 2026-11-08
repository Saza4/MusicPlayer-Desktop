using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;
using MusicPlayer.Infrastructure;

namespace MusicPlayer.Infrastructure.Tests;

public class CompanionLyricsProviderTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("companion-tests").FullName;
    private readonly LrcLyricParser _parser = new();

    private sealed class FixedProvider(LyricBlock block) : ILyricsProvider
    {
        public Task<LyricBlock> GetLyricsAsync(Track track, CancellationToken ct = default) =>
            Task.FromResult(block);
    }

    private Track NewTrack() => new() { FilePath = Path.Combine(_dir, "song.mp3"), Title = "song" };

    private LyricBlock Original() => _parser.Parse("[00:01.00]Hello\n[00:03.00]World");

    private CompanionLyricsProvider NewProvider(LyricBlock inner, string language = "es", string? culture = null) =>
        new(new FixedProvider(inner), _parser, new TagLibLyricsLayerReader(), language, culture);

    private Task Write(string name, string content) =>
        File.WriteAllTextAsync(Path.Combine(_dir, name), content);

    [Fact]
    public async Task MergesTranslationAndPronunciationFromSiblingFiles()
    {
        await Write("song.es.lrc", "[00:01.00]Hola\n[00:03.00]Mundo");
        await Write("song.rom.lrc", "[00:01.00]Helo\n[00:03.00]Wurudo");

        var block = await NewProvider(Original()).GetLyricsAsync(NewTrack());

        Assert.Equal("Hola", block.Lines[0].Translation);
        Assert.Equal("Wurudo", block.Lines[1].Romanization);
        Assert.Equal("Hello", block.Lines[0].Text);
    }

    // Criterio 10 (docs/specs/lyrics-layers.md): antes se ignoraba; ahora, sin la del sistema, se muestra otra.
    [Fact]
    public async Task OnlyAnotherLanguage_ShowsIt()
    {
        await Write("song.fr.lrc", "[00:01.00]Bonjour\n[00:03.00]Monde");

        var block = await NewProvider(Original()).GetLyricsAsync(NewTrack());

        Assert.Equal("Bonjour", block.Lines[0].Translation);
    }

    [Fact]
    public async Task SeveralTranslations_PrefersTheSystemLanguage()
    {
        await Write("song.en.lrc", "[00:01.00]Hi");
        await Write("song.es.lrc", "[00:01.00]Hola");
        await Write("song.fr.lrc", "[00:01.00]Salut");

        var block = await NewProvider(Original(), "es", "es-MX").GetLyricsAsync(NewTrack());

        Assert.Equal("Hola", block.Lines[0].Translation);
    }

    [Fact]
    public async Task SeveralTranslations_WithoutTheSystemLanguage_TakesTheFirstAlphabetically()
    {
        await Write("song.fr.lrc", "[00:01.00]Salut");
        await Write("song.en.lrc", "[00:01.00]Hi");

        var block = await NewProvider(Original(), "es", "es-ES").GetLyricsAsync(NewTrack());

        Assert.Equal("Hi", block.Lines[0].Translation);
    }

    [Fact]
    public async Task RegionalCodes_ExactCultureFirst_ThenTheSameLanguage()
    {
        await Write("song.es-ES.lrc", "[00:01.00]Vale");
        await Write("song.es-419.lrc", "[00:01.00]Órale");
        await Write("song.en.lrc", "[00:01.00]Hi");

        Assert.Equal("Vale", (await NewProvider(Original(), "es", "es-ES").GetLyricsAsync(NewTrack())).Lines[0].Translation);
        Assert.Equal("Órale", (await NewProvider(Original(), "es", "es-MX").GetLyricsAsync(NewTrack())).Lines[0].Translation);
    }

    // Spec, «Compatibilidad con los nombres de hoy»: lo que hoy se muestra se sigue mostrando, también en Linux.
    [Fact]
    public async Task UppercaseSuffixAndExtension_AreFoundOnEveryOS()
    {
        await Write("song.ES.lrc", "[00:01.00]Hola");
        await Write("SONG.ROM.LRC", "[00:01.00]Ola");

        var block = await NewProvider(Original()).GetLyricsAsync(NewTrack());

        Assert.Equal("Hola", block.Lines[0].Translation);
        Assert.Equal("Ola", block.Lines[0].Romanization);
    }

    [Fact]
    public async Task ThreeLetterSystemCode_IsStillRead()
    {
        await Write("song.fil.lrc", "[00:01.00]Kumusta");

        var block = await NewProvider(Original(), "fil", "fil-PH").GetLyricsAsync(NewTrack());

        Assert.Equal("Kumusta", block.Lines[0].Translation);
    }

    [Fact]
    public async Task ThreeLetterCodeOfAnotherLanguage_IsNotATranslation()
    {
        await Write("song.fil.lrc", "[00:01.00]Kumusta");
        await Write("song.alt.lrc", "[00:01.00]Alternativa");
        await Write("song.mix.lrc", "[00:01.00]Mezcla");

        var block = await NewProvider(Original()).GetLyricsAsync(NewTrack());

        Assert.Null(block.Lines[0].Translation);
    }

    [Fact]
    public async Task NoCompanionFiles_ReturnsOriginalUntouched()
    {
        var original = Original();

        var block = await NewProvider(original).GetLyricsAsync(NewTrack());

        Assert.Same(original, block);
    }

    [Fact]
    public async Task NoOriginalLyrics_ReturnsUnavailableEvenIfCompanionsExist()
    {
        await Write("song.es.lrc", "[00:01.00]Hola");

        var block = await NewProvider(LyricBlock.Empty).GetLyricsAsync(NewTrack());

        Assert.False(block.IsAvailable);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);
}