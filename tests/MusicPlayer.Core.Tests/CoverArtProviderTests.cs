using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;

namespace MusicPlayer.Core.Tests;

public class CoverArtProviderTests
{
    private sealed class FakeProvider(byte[]? result) : ICoverArtProvider
    {
        public int Calls { get; private set; }

        public Task<byte[]?> GetCoverAsync(Track track, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(result);
        }
    }

    private static Track Song(string path) => new() { FilePath = path, Title = path };

    [Fact]
    public async Task Cached_SecondRequestForSameTrack_DoesNotHitInnerProvider()
    {
        var inner = new FakeProvider(new byte[] { 1 });
        var cached = new CachedCoverArtProvider(inner);

        await cached.GetCoverAsync(Song("a.mp3"));
        var second = await cached.GetCoverAsync(Song("A.MP3")); // misma ruta, otra capitalización

        Assert.Equal(1, inner.Calls);
        Assert.Equal(new byte[] { 1 }, second);
    }

    [Fact]
    public async Task Cached_RemembersTracksWithoutCover()
    {
        var inner = new FakeProvider(null);
        var cached = new CachedCoverArtProvider(inner);

        await cached.GetCoverAsync(Song("a.mp3"));
        await cached.GetCoverAsync(Song("a.mp3"));

        Assert.Equal(1, inner.Calls);
    }

    [Fact]
    public async Task Cached_DifferentTracks_AreCachedIndependently()
    {
        var inner = new FakeProvider(new byte[] { 1 });
        var cached = new CachedCoverArtProvider(inner);

        await cached.GetCoverAsync(Song("a.mp3"));
        await cached.GetCoverAsync(Song("b.mp3"));

        Assert.Equal(2, inner.Calls);
    }

    [Fact]
    public async Task Composite_FirstSourceWithCoverWins_AndLaterOnesAreNotCalled()
    {
        var first = new FakeProvider(new byte[] { 1 });
        var second = new FakeProvider(new byte[] { 2 });

        var cover = await new CompositeCoverArtProvider(new[] { first, second }).GetCoverAsync(Song("a.mp3"));

        Assert.Equal(new byte[] { 1 }, cover);
        Assert.Equal(0, second.Calls);
    }

    [Fact]
    public async Task Composite_FallsThroughWhenFirstReturnsNullOrEmpty()
    {
        var provider = new CompositeCoverArtProvider(new[]
        {
            new FakeProvider(Array.Empty<byte>()),
            new FakeProvider(null),
            new FakeProvider(new byte[] { 3 })
        });

        Assert.Equal(new byte[] { 3 }, await provider.GetCoverAsync(Song("a.mp3")));
    }

    [Fact]
    public async Task Composite_NoSourceHasCover_ReturnsNull()
    {
        var provider = new CompositeCoverArtProvider(new[] { new FakeProvider(null) });

        Assert.Null(await provider.GetCoverAsync(Song("a.mp3")));
    }
}