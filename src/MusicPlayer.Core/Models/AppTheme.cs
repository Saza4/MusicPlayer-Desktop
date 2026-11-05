namespace MusicPlayer.Core.Models;

/// <summary>Variante de Fluent sobre la que se apoya un tema (decide el color del texto).</summary>
public enum ThemeBase { Dark, Light }

/// <summary>
/// Los nueve colores de un tema. <see cref="Accent"/> es opcional: si falta, se usa el acento del sistema.
/// </summary>
public sealed record ThemePalette(
    ThemeColor Background,
    ThemeColor Surface,
    ThemeColor Overlay,
    ThemeColor Primary,
    ThemeColor OnPrimary,
    ThemeColor? Accent,
    ThemeColor Danger,
    ThemeColor Success,
    ThemeColor Warning);

public sealed record AppTheme(string Id, string Name, ThemeBase Base, ThemePalette Palette, bool IsBuiltIn = false);

/// <summary>Problema al leer un tema: de qué archivo viene y qué ocurre.</summary>
public sealed record ThemeIssue(string File, string Message)
{
    public override string ToString() => $"{File}: {Message}";
}

/// <summary>Resultado de leer la carpeta de temas del usuario: nunca lanza, informa.</summary>
public sealed record ThemeLoadReport(
    IReadOnlyList<AppTheme> Themes,
    IReadOnlyList<ThemeIssue> Errors,
    IReadOnlyList<ThemeIssue> Warnings)
{
    public static ThemeLoadReport Empty { get; } = new([], [], []);
}
