using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;

namespace MusicPlayer.Infrastructure;

/// <summary>
/// Capas hermanas de una letra o de un audio («cancion.es.lrc», «cancion.rom.lrc»). Lo usan la lectura y la incrustación,
/// para que las dos vean los mismos archivos: se recorre la carpeta y se compara sin distinguir mayúsculas, también en
/// Linux, en vez de preguntar por un nombre exacto (docs/specs/lyrics-layers.md).
/// </summary>
public static class LyricsSiblingFiles
{
    // En Linux el patrón distingue mayúsculas por defecto: «*.lrc» no vería «SONG.ROM.LRC».
    private static readonly EnumerationOptions Options = new()
    {
        MatchCasing = MatchCasing.CaseInsensitive,
        IgnoreInaccessible = true,
        RecurseSubdirectories = false
    };

    public static IReadOnlyList<LyricsLayerSource> Find(string lyricsOrAudioPath, string systemLanguage)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(lyricsOrAudioPath));
        if (dir is null) return Array.Empty<LyricsLayerSource>();

        try
        {
            return LyricsLayerNames.FindSiblings(
                lyricsOrAudioPath, Directory.EnumerateFiles(dir, "*.lrc", Options).ToList(), systemLanguage);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Array.Empty<LyricsLayerSource>();
        }
    }
}
