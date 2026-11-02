using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Services;

public sealed class CachedCoverArtProvider(ICoverArtProvider inner, int capacity = 32) : ICoverArtProvider, ICoverArtCache
{
    private readonly LruCache<string, byte[]?> _cache = new(capacity, StringComparer.OrdinalIgnoreCase);

    public async Task<byte[]?> GetCoverAsync(Track track, CancellationToken ct = default)
    {
        if (_cache.TryGet(track.FilePath, out var cached))
            return cached;

        var cover = await inner.GetCoverAsync(track, ct);
        _cache.Set(track.FilePath, cover); // también guarda los "sin carátula"
        return cover;
    }
    public void Invalidate(string filePath) => _cache.Remove(filePath);
}