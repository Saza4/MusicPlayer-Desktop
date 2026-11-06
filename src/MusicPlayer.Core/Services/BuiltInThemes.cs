using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Services;

/// <summary>Temas integrados. Oscuro conserva exactamente los colores que tenía la interfaz antes de los temas.</summary>
public static class BuiltInThemes
{
    public const string SystemId = "system";
    public const string DarkId = "dark";
    public const string LightId = "light";

    public static AppTheme Dark { get; } = new(
        DarkId, "Oscuro", ThemeBase.Dark,
        new ThemePalette(
            Background: ThemeColor.Parse("#0F0F0F"),
            Surface: ThemeColor.Parse("#000000"),
            Overlay: ThemeColor.Parse("#FFFFFF"),
            Primary: ThemeColor.Parse("#FFFFFF"),
            OnPrimary: ThemeColor.Parse("#101010"),
            Accent: null,
            Danger: ThemeColor.Parse("#E57373"),
            Success: ThemeColor.Parse("#4CAF50"),
            Warning: ThemeColor.Parse("#FFB74D")),
        IsBuiltIn: true);

    public static AppTheme Light { get; } = new(
        LightId, "Claro", ThemeBase.Light,
        new ThemePalette(
            Background: ThemeColor.Parse("#F3F3F3"),
            Surface: ThemeColor.Parse("#FFFFFF"),
            Overlay: ThemeColor.Parse("#000000"),
            Primary: ThemeColor.Parse("#1A73E8"),
            OnPrimary: ThemeColor.Parse("#FFFFFF"),
            Accent: null,
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
