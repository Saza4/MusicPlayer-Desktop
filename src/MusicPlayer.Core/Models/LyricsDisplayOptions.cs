namespace MusicPlayer.Core.Models;

/// <summary>Qué se muestra realmente, ya resuelto contra lo que la canción tiene.</summary>
public readonly record struct LyricsLayout(
    bool ShowOriginal,
    bool ShowPronunciation,
    bool ShowTranslation,
    bool EmphasizePronunciation);

/// <summary>Preferencias del usuario sobre cómo ver la letra.</summary>
public sealed record LyricsDisplayOptions(
    bool ShowTranslation = false,
    bool ShowPronunciation = false,
    bool HideOriginal = false,
    bool EmphasizePronunciation = false)
{
    public static LyricsDisplayOptions Default { get; } = new();

    public LyricsLayout Resolve(bool hasTranslation, bool hasPronunciation)
    {
        var showTranslation = ShowTranslation && hasTranslation;
        var showPronunciation = ShowPronunciation && hasPronunciation;
        var showOriginal = !HideOriginal || (!showTranslation && !showPronunciation);

        return new LyricsLayout(
            showOriginal,
            showPronunciation,
            showTranslation,
            EmphasizePronunciation && showPronunciation);
    }
}