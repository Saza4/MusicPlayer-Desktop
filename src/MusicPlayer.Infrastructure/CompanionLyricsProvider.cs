using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;

namespace MusicPlayer.Infrastructure;

/// <summary>
/// Añade a la letra de otro proveedor la traducción («cancion.es.lrc») y la pronunciación («cancion.rom.lrc») que haya
/// junto al audio. Si hay varias traducciones, se muestra una: la del idioma del sistema o, si no, la primera por
/// orden alfabético (docs/specs/lyrics-layers.md, criterio 10).
/// </summary>
/// <param name="systemLanguage">Código corto del sistema (TwoLetterISOLanguageName): vale como sufijo por compatibilidad.</param>
/// <param name="systemCulture">Nombre de la cultura («es-MX»), para elegir la traducción. Por defecto, el código corto.</param>
public sealed class CompanionLyricsProvider(
    ILyricsProvider inner, ILyricParser parser, string systemLanguage, string? systemCulture = null) : ILyricsProvider
{
    private readonly string _culture = systemCulture ?? systemLanguage;

    public async Task<LyricBlock> GetLyricsAsync(Track track, CancellationToken ct = default)
    {
        var original = await inner.GetLyricsAsync(track, ct);
        if (!original.IsAvailable)
            return original;

        var siblings = LyricsSiblingFiles.Find(track.FilePath, systemLanguage);
        var code = LyricsLanguage.Pick(
            siblings.Where(s => s.Kind == LyricsLayerKind.Translation).Select(s => s.Language!), _culture);

        var translation = await ReadAsync(siblings.FirstOrDefault(s => s.Kind == LyricsLayerKind.Translation && s.Language == code), ct);
        var pronunciation = await ReadAsync(siblings.FirstOrDefault(s => s.Kind == LyricsLayerKind.Pronunciation), ct);

        return translation is null && pronunciation is null
            ? original
            : LyricsMerger.Merge(original, translation, pronunciation);
    }

    private async Task<LyricBlock?> ReadAsync(LyricsLayerSource? source, CancellationToken ct)
    {
        if (source is null) return null;
        try
        {
            return parser.Parse(await File.ReadAllTextAsync(source.Path, ct));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
