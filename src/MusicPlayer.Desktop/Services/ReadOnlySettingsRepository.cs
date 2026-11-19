#if DEBUG
using System;
using System.Threading;
using System.Threading.Tasks;
using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;

namespace MusicPlayer.Desktop.Services;

/// <summary>
/// Solo en Debug, con MUSICPLAYER_SETTINGS_READONLY=1 (CompositionRoot): lee los ajustes reales y nunca los guarda,
/// para probar la app (otro tema, reproducir, silenciar) sin cambiar el settings.json del usuario.
/// MUSICPLAYER_THEME_OVERRIDE=&lt;id&gt; («dark», «light», el de un tema de usuario) sustituye el tema leído.
/// </summary>
public sealed class ReadOnlySettingsRepository(ISettingsRepository inner, string? themeOverride) : ISettingsRepository
{
    public async Task<AppSettings> LoadAsync(CancellationToken ct = default)
    {
        var settings = await inner.LoadAsync(ct);
        return string.IsNullOrWhiteSpace(themeOverride) ? settings : settings with { ThemeId = themeOverride.Trim() };
    }

    public Task SaveAsync(AppSettings settings, CancellationToken ct = default) => Task.CompletedTask;

    public static bool IsRequested() =>
        Environment.GetEnvironmentVariable("MUSICPLAYER_SETTINGS_READONLY") == "1";
}
#endif
