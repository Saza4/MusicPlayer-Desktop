using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Services;

/// <summary>Qué tema debe aplicarse: seguir al sistema o uno concreto.</summary>
public sealed record ThemeSelection(bool FollowsSystem, AppTheme? Theme, string? FallbackReason);

public static class ThemeCatalog
{
    /// <summary>Integrados primero y después los de usuario. Un id repetido (sin distinguir mayúsculas) se descarta con error.</summary>
    public static ThemeLoadReport Merge(ThemeLoadReport user)
    {
        var themes = new List<AppTheme>(BuiltInThemes.All);
        var errors = new List<ThemeIssue>(user.Errors);

        foreach (var theme in user.Themes)
        {
            if (themes.Any(t => t.Id.Equals(theme.Id, StringComparison.OrdinalIgnoreCase)))
                errors.Add(new ThemeIssue(theme.Id + ".json", "ya existe otro tema con ese nombre"));
            else
                themes.Add(theme);
        }

        return new ThemeLoadReport(themes, errors, user.Warnings);
    }

    /// <summary>
    /// Resuelve el id guardado en los ajustes. Un id desconocido cae en Oscuro con una explicación,
    /// sin que el llamador deba sobrescribir el ajuste.
    /// </summary>
    public static ThemeSelection Resolve(string? id, IReadOnlyList<AppTheme> available)
    {
        if (string.IsNullOrWhiteSpace(id))
            return new ThemeSelection(false, BuiltInThemes.Dark, null);

        if (id.Equals(BuiltInThemes.SystemId, StringComparison.OrdinalIgnoreCase))
            return new ThemeSelection(true, null, null);

        var found = available.FirstOrDefault(t => t.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        return found is not null
            ? new ThemeSelection(false, found, null)
            : new ThemeSelection(false, BuiltInThemes.Dark, $"No se encontró el tema '{id}'; se usa Oscuro.");
    }
}
