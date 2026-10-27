using MusicPlayer.Core.Services;

namespace MusicPlayer.Core.Tests;

public class LruCacheTests
{
    [Fact]
    public void Set_BeyondCapacity_EvictsLeastRecentlyUsed()
    {
        var cache = new LruCache<string, int>(2);
        cache.Set("a", 1);
        cache.Set("b", 2);
        cache.TryGet("a", out _); // "a" pasa a ser la más reciente
        cache.Set("c", 3);        // expulsa "b"

        Assert.True(cache.TryGet("a", out _));
        Assert.False(cache.TryGet("b", out _));
        Assert.True(cache.TryGet("c", out _));
        Assert.Equal(2, cache.Count);
    }

    [Fact]
    public void Set_ExistingKey_ReplacesValueWithoutGrowing()
    {
        var cache = new LruCache<string, int>(2);
        cache.Set("a", 1);
        cache.Set("a", 99);

        Assert.True(cache.TryGet("a", out var value));
        Assert.Equal(99, value);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void StoresNullValues_SoMissesCanBeRemembered()
    {
        var cache = new LruCache<string, string?>(2);
        cache.Set("a", null);

        Assert.True(cache.TryGet("a", out var value));
        Assert.Null(value);
    }

    [Fact]
    public void Constructor_NonPositiveCapacity_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LruCache<string, int>(0));
    }
}