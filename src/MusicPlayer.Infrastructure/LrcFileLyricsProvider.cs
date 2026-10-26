using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;

namespace MusicPlayer.Infrastructure;

/// <summary>Busca "cancion.lrc" junto a "cancion.mp3".</summary>
public sealed class LrcFileLyricsProvider(ILyricParser parser) : ILyricsProvider
{
    public async Task<LyricBlock> GetLyricsAsync(Track track, CancellationToken ct = default)
    {
        var path = Path.ChangeExtension(track.FilePath, ".lrc");
        if (!File.Exists(path))
            return LyricBlock.Empty;

        try
        {
            return parser.Parse(await File.ReadAllTextAsync(path, ct));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return LyricBlock.Empty;
        }
    }
}