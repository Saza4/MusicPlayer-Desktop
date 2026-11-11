using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;

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
            // Mismo tope que al incrustar: un .lrc mayor se trata como si no estuviera
            // (docs/specs/untrusted-input-limits.md, criterio 1).
            if (new FileInfo(path).Length > LyricsEditor.MaxBytes)
                return LyricBlock.Empty;

            var text = await File.ReadAllTextAsync(path, ct);
            return await Task.Run(() => parser.Parse(text), ct); // el análisis, fuera del hilo de la interfaz
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return LyricBlock.Empty;
        }
    }
}