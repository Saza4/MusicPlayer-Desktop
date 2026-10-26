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

    private CompanionLyricsProvider NewProvider(LyricBlock inner) =>
        new(new FixedProvider(inner), _parser, "es");

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

    [Fact]
    public async Task IgnoresTranslationsInOtherLanguages()
    {
        await Write("song.fr.lrc", "[00:01.00]Bonjour\n[00:03.00]Monde");

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