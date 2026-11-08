namespace MusicPlayer.Core.Services;

/// <summary>
/// Códigos de idioma de las traducciones (docs/specs/lyrics-layers.md, criterios 1 y 10). Lista fija ISO 639-1, sin
/// depender de las culturas del sistema (cambian con la versión de ICU).
/// </summary>
public static class LyricsLanguage
{
    /// <summary>Sufijo reservado de la pronunciación («cancion.rom.lrc»).</summary>
    public const string PronunciationSuffix = "rom";

    // ISO 639-1: 183 códigos.
    private static readonly HashSet<string> Iso6391 = new(
        ("aa ab ae af ak am an ar as av ay az ba be bg bi bm bn bo br bs ca ce ch co cr cs cu cv cy da de dv dz ee " +
         "el en eo es et eu fa ff fi fj fo fr fy ga gd gl gn gu gv ha he hi ho hr ht hu hy hz ia id ie ig ii ik io " +
         "is it iu ja jv ka kg ki kj kk kl km kn ko kr ks ku kv kw ky la lb lg li ln lo lt lu lv mg mh mi mk ml mn " +
         "mr ms mt my na nb nd ne ng nl nn no nr nv ny oc oj om or os pa pi pl ps pt qu rm rn ro ru rw sa sc sd se " +
         "sg si sk sl sm sn so sq sr ss st su sv sw ta te tg th ti tk tl tn to tr ts tt tw ty ug uk ur uz ve vi vo " +
         "wa wo xh yi yo za zh zu").Split(' '),
        StringComparer.Ordinal);

    public static int KnownLanguageCount => Iso6391.Count;

    /// <summary>
    /// Normaliza un código ISO 639-1 con región («pt-BR», dos letras; «es-419», tres cifras de la ONU) o escritura
    /// («zh-Hans», cuatro letras) opcionales: idioma en minúsculas, región en mayúsculas y escritura con mayúscula
    /// inicial. Null si no lo es.
    /// </summary>
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return null;
        var parts = raw.Split('-');
        if (parts.Length > 2) return null;

        var language = parts[0].ToLowerInvariant();
        if (!Iso6391.Contains(language)) return null;
        if (parts.Length == 1) return language;

        var sub = parts[1];
        if (sub.Length == 3 && sub.All(char.IsAsciiDigit)) return $"{language}-{sub}";
        if (!sub.All(char.IsAsciiLetter)) return null;
        return sub.Length switch
        {
            2 => $"{language}-{sub.ToUpperInvariant()}",
            4 => $"{language}-{char.ToUpperInvariant(sub[0])}{sub[1..].ToLowerInvariant()}",
            _ => null
        };
    }

    /// <summary>
    /// Código de traducción de un sufijo de archivo («es» en «cancion.es.lrc»). Vale un código ISO 639-1 normalizable o,
    /// por compatibilidad, el código del propio sistema tal como lo leía la app (`TwoLetterISOLanguageName`, que en
    /// culturas sin ISO 639-1 tiene tres letras: «fil», «haw»). «rom» nunca es traducción.
    /// </summary>
    public static string? FromSuffix(string suffix, string systemLanguage)
    {
        if (suffix.Equals(PronunciationSuffix, StringComparison.OrdinalIgnoreCase)) return null;
        if (Normalize(suffix) is { } code) return code;

        var system = systemLanguage.Trim().TrimStart('.').ToLowerInvariant();
        return system.Length > 0 && system != PronunciationSuffix &&
               suffix.Equals(system, StringComparison.OrdinalIgnoreCase)
            ? system
            : null;
    }

    /// <summary>
    /// Código de traducción que indica el nombre de un archivo elegido a mano («es» en «cualquier.es.lrc», «pt-BR» en
    /// «x.pt-BR.lrc»), con la misma regla que los hermanos (<see cref="FromSuffix"/>). Null si el nombre no trae un
    /// idioma válido («traduccion.lrc», «x.rom.lrc», «x.alt.lrc»); quien llama usa entonces el idioma del sistema.
    /// </summary>
    public static string? FromFileName(string path, string systemLanguage)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        var dot = name.LastIndexOf('.');
        return dot < 0 ? null : FromSuffix(name[(dot + 1)..], systemLanguage);
    }

    /// <summary>
    /// Código de traducción leído de un campo incrustado: lo que <see cref="Normalize"/> acepta o, por la misma
    /// compatibilidad, un código de dos o tres letras (se escribió como código del sistema).
    /// </summary>
    public static string? FromField(string raw)
    {
        if (Normalize(raw) is { } code) return code;
        return raw.Length is 2 or 3 && raw.All(char.IsAsciiLetter) && !raw.Equals(PronunciationSuffix, StringComparison.OrdinalIgnoreCase)
            ? raw.ToLowerInvariant()
            : null;
    }

    /// <summary>
    /// Elige la traducción que se muestra (criterio 10): la del idioma exacto del sistema («pt-BR»); si no, la del mismo
    /// idioma principal («pt»), primero sin subetiqueta y luego por orden alfabético; si no, la primera alfabética.
    /// </summary>
    /// <param name="systemCulture">Nombre de la cultura de la interfaz («es-MX», «pt-BR», «fil-PH»); puede ser vacío.</param>
    public static string? Pick(IEnumerable<string> codes, string systemCulture)
    {
        var sorted = codes.Distinct(StringComparer.OrdinalIgnoreCase)
                          .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                          .ToList();
        if (sorted.Count == 0) return null;

        var exact = sorted.FirstOrDefault(c => c.Equals(systemCulture, StringComparison.OrdinalIgnoreCase));
        if (exact is not null) return exact;

        var primary = Primary(systemCulture);
        if (primary.Length > 0)
        {
            var same = sorted.Where(c => Primary(c) == primary).ToList();
            if (same.Count > 0)
                return same.FirstOrDefault(c => !c.Contains('-')) ?? same[0];
        }
        return sorted[0];
    }

    /// <summary>True si el código es del idioma del sistema (la etiqueta del menú no lleva código, criterio 13).</summary>
    public static bool IsSystemLanguage(string code, string systemCulture) =>
        Primary(systemCulture).Length > 0 && Primary(code) == Primary(systemCulture);

    private static string Primary(string code) => code.Split('-')[0].ToLowerInvariant();
}
