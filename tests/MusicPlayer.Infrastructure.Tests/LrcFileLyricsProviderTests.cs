using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;
using MusicPlayer.Infrastructure;

namespace MusicPlayer.Infrastructure.Tests;

public class LrcFileLyricsProviderTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("lrc-tests").FullName;
    private readonly LrcFileLyricsProvider _provider = new(new LrcLyricParser());

    private Track NewTrack() => new() { FilePath = Path.Combine(_dir, "song.mp3"), Title = "song" };

    [Fact]
    public async Task ReadsSyncedLrcNextToAudio()
    {
        await File.WriteAllTextAsync(Path.Combine(_dir, "song.lrc"), "[00:01.00]Hola\n[00:03.00]Mundo");

        var block = await _provider.GetLyricsAsync(NewTrack());

        Assert.Equal(LyricsKind.Synced, block.Kind);
        Assert.Equal(2, block.Lines.Count);
    }

    [Fact]
    public async Task MissingLrc_ReturnsUnavailable()
    {
        Assert.False((await _provider.GetLyricsAsync(NewTrack())).IsAvailable);
    }

    [Fact]
    public async Task LrcWithoutTimestamps_IsPlainText()
    {
        await File.WriteAllTextAsync(Path.Combine(_dir, "song.lrc"), "Línea uno\nLínea dos");

        var block = await _provider.GetLyricsAsync(NewTrack());

        Assert.Equal(LyricsKind.Plain, block.Kind);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);
}