using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;

namespace MusicPlayer.Infrastructure;

/// <summary>
/// Añade a la letra de otro proveedor la traducción y la pronunciación, de los archivos hermanos («cancion.es.lrc»,
/// «cancion.rom.lrc») o de las capas incrustadas en el audio (docs/specs/lyrics-layers.md, criterios 8-10):
/// <list type="bullet">
/// <item>Pronunciación: el hermano gana a la incrustada.</item>
/// <item>Traducción: una sola, elegida por idioma entre hermanas e incrustadas juntas (la del sistema; si no, la del
/// mismo idioma; si no, la primera alfabética). Dentro del mismo código, el hermano gana a la incrustada.</item>
/// </list>
/// Un hermano que no se puede leer, no da letra o da otro tipo que el original (texto sin marcas con un original
/// sincronizado, que <see cref="LyricsMerger"/> descartaría) cede su sitio a la incrustada del mismo tipo y código.
/// </summary>
/// <param name="systemLanguage">Código corto del sistema (TwoLetterISOLanguageName): vale como sufijo por compatibilidad.</param>
/// <param name="systemCulture">Nombre de la cultura («es-MX»), para elegir la traducción. Por defecto, el código corto.</param>
public sealed class CompanionLyricsProvider(
    ILyricsProvider inner, ILyricParser parser, ILyricsLayerReader embedded,
    string systemLanguage, string? systemCulture = null) : ILyricsProvider
{
    private readonly string _culture = systemCulture ?? systemLanguage;

    public async Task<LyricBlock> GetLyricsAsync(Track track, CancellationToken ct = default)
    {
        var original = await inner.GetLyricsAsync(track, ct);
        if (!original.IsAvailable)
            return original;

        var siblings = LyricsSiblingFiles.Find(track.FilePath, systemLanguage);
        var layers = await Task.Run(() => embedded.Read(track.FilePath), ct);

        var code = LyricsLanguage.Pick(
            siblings.Where(s => s.Kind == LyricsLayerKind.Translation).Select(s => s.Language!)
                    .Concat(layers.Where(l => l.Kind == LyricsLayerKind.Translation).Select(l => l.Language!)),
            _culture);

        var translation = code is null ? null : await LayerAsync(original.Kind, LyricsLayerKind.Translation, code, siblings, layers, ct);
        var pronunciation = await LayerAsync(original.Kind, LyricsLayerKind.Pronunciation, null, siblings, layers, ct);

        return translation is null && pronunciation is null
            ? original
            : LyricsMerger.Merge(original, translation, pronunciation) with
            {
                TranslationLanguage = translation is null ? null : code
            };
    }

    private async Task<LyricBlock?> LayerAsync(
        LyricsKind originalKind, LyricsLayerKind kind, string? code,
        IReadOnlyList<LyricsLayerSource> siblings, IReadOnlyList<LyricsLayer> layers,
        CancellationToken ct)
    {
        var sibling = siblings.FirstOrDefault(s => s.Kind == kind && Same(s.Language, code));
        if (sibling is not null && await ReadAsync(sibling.Path, ct) is { IsAvailable: true } fromFile && fromFile.Kind == originalKind)
            return fromFile;

        var layer = layers.FirstOrDefault(l => l.Kind == kind && Same(l.Language, code));
        return layer is not null && parser.Parse(layer.Text) is { IsAvailable: true } fromTag && fromTag.Kind == originalKind
            ? fromTag
            : null;
    }

    private async Task<LyricBlock?> ReadAsync(string path, CancellationToken ct)
    {
        try
        {
            return parser.Parse(await File.ReadAllTextAsync(path, ct));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool Same(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
