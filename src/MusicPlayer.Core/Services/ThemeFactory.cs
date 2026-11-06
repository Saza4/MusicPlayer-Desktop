using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Services;

/// <summary>Resultado de validar un tema: el tema (si es válido) y los problemas encontrados.</summary>
public sealed record ThemeResult(AppTheme? Theme, IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings);

/// <summary>
/// Reglas de los temas de usuario. Recibe los campos ya leídos del JSON y decide si el tema es válido.
/// Un error descarta el tema; un aviso solo ignora ese dato (el color hereda el de la base).
/// </summary>
public static class ThemeFactory
{
    public const int SupportedFormatVersion = 1;
    public const int MaxNameLength = 40;
    public const double MinContrast = 3.0;

    private static readonly ThemeColor Black = new(0, 0, 0);

    private static readonly string[] ColorKeys =
        ["background", "surface", "overlay", "primary", "onPrimary", "accent", "danger", "success", "warning"];

    public static ThemeResult Create(
        string id,
        int? formatVersion,
        string? name,
        string? baseName,
        IReadOnlyDictionary<string, string?>? colors)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (BuiltInThemes.IsReservedId(id))
            errors.Add($"el nombre de archivo '{id}' está reservado para los temas integrados");

        if (formatVersion is null)
            errors.Add("falta 'formatVersion'");
        else if (formatVersion > SupportedFormatVersion)
            errors.Add($"creado para una versión más nueva del formato ({formatVersion})");
        else if (formatVersion != SupportedFormatVersion)
            errors.Add($"'formatVersion' {formatVersion} no es válido (se espera {SupportedFormatVersion})");

        var themeBase = ThemeBase.Dark;
        if (baseName is not null)
        {
            var b = baseName.Trim();
            if (b.Equals("dark", StringComparison.OrdinalIgnoreCase)) themeBase = ThemeBase.Dark;
            else if (b.Equals("light", StringComparison.OrdinalIgnoreCase)) themeBase = ThemeBase.Light;
            else errors.Add($"'base' debe ser 'dark' o 'light' (no '{baseName}')");
        }

        if (errors.Count > 0)
            return new ThemeResult(null, errors, warnings);

        var palette = ReadPalette(BuiltInThemes.For(themeBase).Palette, colors, warnings);
        CheckContrast(themeBase, palette, errors);
        if (errors.Count > 0)
            return new ThemeResult(null, errors, warnings);

        var displayName = string.IsNullOrWhiteSpace(name) ? id : name.Trim();
        if (displayName.Length > MaxNameLength)
            displayName = displayName[..MaxNameLength];

        return new ThemeResult(new AppTheme(id, displayName, themeBase, palette), errors, warnings);
    }

    private static ThemePalette ReadPalette(
        ThemePalette inherited, IReadOnlyDictionary<string, string?>? colors, List<string> warnings)
    {
        var given = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (colors is not null)
            foreach (var (key, value) in colors)
                given[key] = value;

        foreach (var key in given.Keys.Where(k => !ColorKeys.Contains(k, StringComparer.OrdinalIgnoreCase)))
            warnings.Add($"el color '{key}' no existe y se ignora");

        ThemeColor Pick(string key, ThemeColor fallback) => Read(key, given, fallback, warnings) ?? fallback;

        return new ThemePalette(
            Background: Pick("background", inherited.Background),
            Surface: Pick("surface", inherited.Surface),
            Overlay: Pick("overlay", inherited.Overlay),
            Primary: Pick("primary", inherited.Primary),
            OnPrimary: Pick("onPrimary", inherited.OnPrimary),
            Accent: Read("accent", given, null, warnings),
            Danger: Pick("danger", inherited.Danger),
            Success: Pick("success", inherited.Success),
            Warning: Pick("warning", inherited.Warning));
    }

    // Ausente: hereda sin aviso (todos los colores son opcionales). Presente pero inválido: aviso y hereda.
    private static ThemeColor? Read(
        string key, Dictionary<string, string?> given, ThemeColor? fallback, List<string> warnings)
    {
        if (!given.TryGetValue(key, out var text))
            return fallback;

        if (ThemeColor.TryParse(text, out var color))
            return color;

        warnings.Add($"el color '{key}' no es válido ('{text}'); se usa el de la base. Formato: #RRGGBB");
        return fallback;
    }

    private static void CheckContrast(ThemeBase themeBase, ThemePalette p, List<string> errors)
    {
        var text = BuiltInThemes.TextOf(themeBase);

        void Check(ThemeColor a, ThemeColor b, string pair)
        {
            var ratio = ColorContrast.Ratio(a, b);
            if (ratio < MinContrast)
                errors.Add($"contraste insuficiente en {pair} ({ratio:0.0}:1, mínimo {MinContrast:0.0}:1)");
        }

        Check(text, p.Background, "el texto contra 'background'");
        Check(text, p.Surface, "el texto contra 'surface'");
        Check(p.OnPrimary, p.Primary, "'onPrimary' contra 'primary'");
        Check(p.Primary, Black, "'primary' contra el velo negro del modo inmersivo");
        if (p.Accent is { } accent)
            Check(accent, Black, "'accent' contra el velo negro del modo inmersivo");
    }
}
