using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;

namespace MusicPlayer.Core.Tests;

public class CompositeLyricsProviderTests
{
    private static readonly Track Song = new() { FilePath = "x.mp3", Title = "x" };

    private sealed class FakeProvider(LyricBlock block) : ILyricsProvider
    {
        public int Calls { get; private set; }

        public Task<LyricBlock> GetLyricsAsync(Track track, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(block);
        }
    }

    private static LyricBlock Plain(string text) =>
        new(LyricsKind.Plain, new[] { new LyricLine(TimeSpan.Zero, text) });

    [Fact]
    public async Task FirstAvailableSourceWins_AndLaterOnesAreNotCalled()
    {
        var first = new FakeProvider(Plain("uno"));
        var second = new FakeProvider(Plain("dos"));

        var block = await new CompositeLyricsProvider(new[] { first, second }).GetLyricsAsync(Song);

        Assert.Equal("uno", block.Lines[0].Text);
        Assert.Equal(0, second.Calls);
    }

    [Fact]
    public async Task FallsThroughWhenFirstHasNoLyrics()
    {
        var provider = new CompositeLyricsProvider(new[]
        {
            new FakeProvider(LyricBlock.Empty),
            new FakeProvider(Plain("dos"))
        });

        var block = await provider.GetLyricsAsync(Song);

        Assert.Equal("dos", block.Lines[0].Text);
    }

    [Fact]
    public async Task NoSourceHasLyrics_ReturnsUnavailable()
    {
        var provider = new CompositeLyricsProvider(new[] { new FakeProvider(LyricBlock.Empty) });

        Assert.False((await provider.GetLyricsAsync(Song)).IsAvailable);
    }
}