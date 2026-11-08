using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Services;

/// <summary>
/// Nombres de las capas (docs/specs/lyrics-layers.md): los archivos hermanos («cancion.es.lrc», «cancion.rom.lrc») y
/// los campos incrustados («LYRICS_TRANSLATION_ES», «LYRICS_PRONUNCIATION»), iguales en MP3, FLAC y M4A.
/// </summary>
public static class LyricsLayerNames
{
    public const string TranslationFieldPrefix = "LYRICS_TRANSLATION_";
    public const string PronunciationField = "LYRICS_PRONUNCIATION";

    /// <summary>Nombre del campo, siempre en mayúsculas: «LYRICS_TRANSLATION_PT-BR».</summary>
    public static string FieldName(LyricsLayerKind kind, string? language) => kind == LyricsLayerKind.Pronunciation
        ? PronunciationField
        : TranslationFieldPrefix + (language ?? throw new ArgumentNullException(nameof(language))).ToUpperInvariant();

    public static string FieldName(LyricsLayer layer) => FieldName(layer.Kind, layer.Language);

    /// <summary>
    /// Reconoce un campo de capa sin distinguir mayúsculas y devuelve el código normalizado («pt-BR», «zh-Hans»).
    /// False si el campo no es una capa o su código no es válido.
    /// </summary>
    public static bool TryParseField(string name, out LyricsLayerKind kind, out string? language)
    {
        kind = default;
        language = null;
        if (name.Equals(PronunciationField, StringComparison.OrdinalIgnoreCase))
        {
            kind = LyricsLayerKind.Pronunciation;
            return true;
        }
        if (!name.StartsWith(TranslationFieldPrefix, StringComparison.OrdinalIgnoreCase))
            return false;

        language = LyricsLanguage.FromField(name[TranslationFieldPrefix.Length..]);
        kind = LyricsLayerKind.Translation;
        return language is not null;
    }

    /// <summary>
    /// Capas hermanas de una letra: los archivos «&lt;nombre&gt;.&lt;código&gt;.lrc» y «&lt;nombre&gt;.rom.lrc» entre
    /// <paramref name="files"/>, donde «nombre» es el de <paramref name="lyricsOrAudioPath"/> sin extensión, como prefijo
    /// exacto y sin distinguir mayúsculas. Un código repetido («es» y «ES», en Linux) se toma una vez: el primero en orden
    /// ordinal. Orden de salida: pronunciación y luego traducciones por código.
    /// </summary>
    public static IReadOnlyList<LyricsLayerSource> FindSiblings(
        string lyricsOrAudioPath, IEnumerable<string> files, string systemLanguage)
    {
        var stem = Path.GetFileNameWithoutExtension(lyricsOrAudioPath) + ".";
        var found = new Dictionary<string, LyricsLayerSource>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files.OrderBy(f => Path.GetFileName(f), StringComparer.Ordinal))
        {
            var name = Path.GetFileName(file);
            if (name.Length <= stem.Length + ".lrc".Length ||        // «song.lrc», la propia letra principal
                !name.StartsWith(stem, StringComparison.OrdinalIgnoreCase) ||
                !name.EndsWith(".lrc", StringComparison.OrdinalIgnoreCase))
                continue;

            var suffix = name[stem.Length..^".lrc".Length];
            if (suffix.Length == 0 || suffix.Contains('.')) continue;

            LyricsLayerSource? source = suffix.Equals(LyricsLanguage.PronunciationSuffix, StringComparison.OrdinalIgnoreCase)
                ? new LyricsLayerSource(LyricsLayerKind.Pronunciation, null, file)
                : LyricsLanguage.FromSuffix(suffix, systemLanguage) is { } code
                    ? new LyricsLayerSource(LyricsLayerKind.Translation, code, file)
                    : null;

            if (source is not null)
                found.TryAdd(source.Language ?? "", source);
        }

        return found.Values
            .OrderBy(s => s.Kind == LyricsLayerKind.Translation)
            .ThenBy(s => s.Language, StringComparer.Ordinal)
            .ToList();
    }
}
