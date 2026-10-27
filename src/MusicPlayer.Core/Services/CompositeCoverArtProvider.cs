using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Services;

public sealed class CompositeCoverArtProvider(IEnumerable<ICoverArtProvider> providers) : ICoverArtProvider
{
    private readonly IReadOnlyList<ICoverArtProvider> _providers = providers.ToList();

    public async Task<byte[]?> GetCoverAsync(Track track, CancellationToken ct = default)
    {
        foreach (var provider in _providers)
        {
            ct.ThrowIfCancellationRequested();
            var cover = await provider.GetCoverAsync(track, ct);
            if (cover is { Length: > 0 })
                return cover;
        }
        return null;
    }
}