using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;

namespace MusicPlayer.Infrastructure;

/// <summary>Capas incrustadas en MP3, FLAC y M4A (docs/specs/lyrics-layers.md, criterio 8), con <see cref="TagLibLyricsLayerFields"/>.</summary>
public sealed class TagLibLyricsLayerReader : ILyricsLayerReader
{
    public IReadOnlyList<LyricsLayer> Read(string filePath)
    {
        if (!TagLibLyricsLayerFields.Supports(filePath))
            return [];
        try
        {
            using var file = TagLib.File.Create(filePath);
            return TagLibLyricsLayerFields.Read(file);
        }
        catch (Exception ex) when (ex is TagLib.CorruptFileException
                                      or TagLib.UnsupportedFormatException
                                      or IOException
                                      or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
