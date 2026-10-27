namespace MusicPlayer.Core.Models;

public sealed record AppSettings(
    double Volume = 1.0,
    bool ShowTranslation = false,
    bool ShowPronunciation = false,
    bool HideOriginal = false,
    bool EmphasizePronunciation = false,
    Guid? LastTrackId = null)
{
    public static AppSettings Default { get; } = new();
}