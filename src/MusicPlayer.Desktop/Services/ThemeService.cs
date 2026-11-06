using System;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;

namespace MusicPlayer.Desktop.Services;

/// <summary>
/// Aplica un tema a la aplicación: elige la variante de Fluent (clara u oscura) y escribe los nueve colores
/// en los recursos de la app. Los pinceles de <c>Themes/*.axaml</c> los leen con DynamicResource, así que el
/// cambio se ve al instante sin reiniciar. Debe llamarse desde el hilo de la interfaz.
/// </summary>
public sealed class ThemeService
{
    private bool _followingSystem;
    private bool _subscribed;

    /// <summary>Aplica un tema concreto: su variante base y sus colores.</summary>
    public void Apply(AppTheme theme)
    {
        _followingSystem = false;
        var app = Application.Current!;
        app.RequestedThemeVariant = theme.Base == ThemeBase.Light ? ThemeVariant.Light : ThemeVariant.Dark;
        WriteColors(app, theme.Palette);
    }

    /// <summary>Sigue el tema del sistema operativo (Claro u Oscuro), también si cambia con la app abierta.</summary>
    public void ApplySystem()
    {
        var app = Application.Current!;
        _followingSystem = true;
        app.RequestedThemeVariant = ThemeVariant.Default;

        if (!_subscribed)
        {
            app.ActualThemeVariantChanged += OnActualThemeVariantChanged;
            _subscribed = true;
        }

        ApplyBuiltInForCurrentVariant(app);
    }

    private void OnActualThemeVariantChanged(object? sender, EventArgs e)
    {
        if (_followingSystem && Application.Current is { } app)
            ApplyBuiltInForCurrentVariant(app);
    }

    private static void ApplyBuiltInForCurrentVariant(Application app) =>
        WriteColors(app, (app.ActualThemeVariant == ThemeVariant.Light ? BuiltInThemes.Light : BuiltInThemes.Dark).Palette);

    private static void WriteColors(Application app, ThemePalette p)
    {
        var resources = app.Resources;
        resources["ThemeBackground"] = ToColor(p.Background);
        resources["ThemeSurface"] = ToColor(p.Surface);
        resources["ThemeOverlay"] = ToColor(p.Overlay);
        resources["ThemePrimary"] = ToColor(p.Primary);
        resources["ThemeOnPrimary"] = ToColor(p.OnPrimary);
        resources["ThemeAccent"] = p.Accent is { } accent ? ToColor(accent) : SystemAccent(app);
        resources["ThemeDanger"] = ToColor(p.Danger);
        resources["ThemeSuccess"] = ToColor(p.Success);
        resources["ThemeWarning"] = ToColor(p.Warning);
    }

    private static Color ToColor(ThemeColor c) => Color.FromRgb(c.R, c.G, c.B);

    // Sin acento en el tema se usa el del sistema, como antes de que existieran los temas.
    private static Color SystemAccent(Application app) =>
        app.TryGetResource("SystemAccentColor", app.ActualThemeVariant, out var value) && value is Color color
            ? color
            : Color.Parse("#0078D4");
}
