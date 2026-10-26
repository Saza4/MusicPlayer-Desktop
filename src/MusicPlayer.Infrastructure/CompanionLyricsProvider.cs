using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;

namespace MusicPlayer.Infrastructure;

/// <summary>
/// Añade a la letra de otro proveedor la traducción ("cancion.es.lrc")
/// y la pronunciación ("cancion.rom.lrc") si existen junto al audio.
/// </summary>
public sealed class CompanionLyricsProvider(
    ILyricsProvider inner, ILyricParser parser, string translationLanguage) : ILyricsProvider
{
    private const string PronunciationSuffix = "rom";

    private readonly string _language = translationLanguage.Trim().TrimStart('.').ToLowerInvariant();

    public async Task<LyricBlock> GetLyricsAsync(Track track, CancellationToken ct = default)
    {
        var original = await inner.GetLyricsAsync(track, ct);
        if (!original.IsAvailable)
            return original;

        var translation = _language.Length > 0 && _language != PronunciationSuffix
            ? await ReadCompanionAsync(track, _language, ct)
            : null;
        var pronunciation = await ReadCompanionAsync(track, PronunciationSuffix, ct);

        return translation is null && pronunciation is null
            ? original
            : LyricsMerger.Merge(original, translation, pronunciation);
    }

    private async Task<LyricBlock?> ReadCompanionAsync(Track track, string suffix, CancellationToken ct)
    {
        var path = Path.ChangeExtension(track.FilePath, $".{suffix}.lrc");
        if (!File.Exists(path))
            return null;

        try
        {
            return parser.Parse(await File.ReadAllTextAsync(path, ct));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}