using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Services;

/// <summary>
/// Temas integrados, con la paleta del rediseño (docs/specs/redesign-mockups.md): fondo, superficie y un solo azul
/// para lo interactivo y lo que suena. El acento es ese mismo azul, no el del sistema, para que los iconos activos, el
/// relleno de las barras y el botón Reproducir/Pausa hablen el mismo color. En Claro el azul es más oscuro para llegar a
/// 4,5:1 sobre blanco.
/// </summary>
public static class BuiltInThemes
{
    public const string SystemId = "system";
    public const string DarkId = "dark";
    public const string LightId = "light";

    public static AppTheme Dark { get; } = new(
        DarkId, "Oscuro", ThemeBase.Dark,
        new ThemePalette(
            Background: ThemeColor.Parse("#141416"),
            Surface: ThemeColor.Parse("#0C0C0E"),
            Overlay: ThemeColor.Parse("#FFFFFF"),
            Primary: ThemeColor.Parse("#6B9CF4"),
            OnPrimary: ThemeColor.Parse("#0B0F1A"),
            Accent: ThemeColor.Parse("#6B9CF4"),
            Danger: ThemeColor.Parse("#E57373"),
            Success: ThemeColor.Parse("#4CAF50"),
            Warning: ThemeColor.Parse("#FFB74D")),
        IsBuiltIn: true);

    public static AppTheme Light { get; } = new(
        LightId, "Claro", ThemeBase.Light,
        new ThemePalette(
            Background: ThemeColor.Parse("#F4F4F2"),
            Surface: ThemeColor.Parse("#FFFFFF"),
            Overlay: ThemeColor.Parse("#000000"),
            Primary: ThemeColor.Parse("#2F62C8"),
            OnPrimary: ThemeColor.Parse("#FFFFFF"),
            Accent: ThemeColor.Parse("#2F62C8"),
            Danger: ThemeColor.Parse("#C62828"),
            Success: ThemeColor.Parse("#2E7D32"),
            Warning: ThemeColor.Parse("#B26A00")),
        IsBuiltIn: true);

    public static IReadOnlyList<AppTheme> All { get; } = [Dark, Light];

    /// <summary>Ids que un archivo de usuario no puede usar.</summary>
    public static bool IsReservedId(string id) =>
        id.Equals(SystemId, StringComparison.OrdinalIgnoreCase)
        || All.Any(t => t.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public static AppTheme For(ThemeBase themeBase) => themeBase == ThemeBase.Light ? Light : Dark;

    /// <summary>Color del texto de Fluent sobre cada variante (se usa para comprobar el contraste).</summary>
    public static ThemeColor TextOf(ThemeBase themeBase) =>
        themeBase == ThemeBase.Light ? new ThemeColor(0, 0, 0) : new ThemeColor(255, 255, 255);
}
