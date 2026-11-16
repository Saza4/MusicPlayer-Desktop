namespace MusicPlayer.Core.Models;

public sealed record AppSettings(
    double Volume = 1.0,
    bool ShowTranslation = false,
    bool ShowPronunciation = false,
    bool HideOriginal = false,
    bool EmphasizePronunciation = false,
    Guid? LastTrackId = null,
    bool Shuffle = false,
    RepeatMode Repeat = RepeatMode.Off,
    string ThemeId = "dark",
    bool ShowIsland = true, // al final: los settings.json anteriores la reciben activada
    bool PlayOnSingleClick = false, // docs/specs/settings-panel.md: lo nuevo, siempre al final y con su valor de siempre
    bool IslandShowTitle = false) // docs/specs/island-title-and-close.md: título desplazable en la isla colapsada
{
    public static AppSettings Default { get; } = new();
}