using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Services;

public sealed class CompositeLyricsProvider(IEnumerable<ILyricsProvider> providers) : ILyricsProvider
{
    private readonly IReadOnlyList<ILyricsProvider> _providers = providers.ToList();

    public async Task<LyricBlock> GetLyricsAsync(Track track, CancellationToken ct = default)
    {
        foreach (var provider in _providers)
        {
            ct.ThrowIfCancellationRequested();
            var block = await provider.GetLyricsAsync(track, ct);
            if (block.IsAvailable)
                return block;
        }
        return LyricBlock.Empty;
    }
}