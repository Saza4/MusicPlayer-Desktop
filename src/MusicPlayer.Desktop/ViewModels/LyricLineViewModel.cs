using CommunityToolkit.Mvvm.ComponentModel;
using MusicPlayer.Core.Models;

namespace MusicPlayer.Desktop.ViewModels;

public sealed partial class LyricLineViewModel(LyricLine line, bool isSynced) : ObservableObject
{
    private const double LargeSize = 18;
    private const double SmallSize = 14;

    public LyricLine Line { get; } = line;
    public bool IsSynced { get; } = isSynced;

    // Las líneas vacías de un .lrc marcan pausas instrumentales.
    public string Text => string.IsNullOrWhiteSpace(Line.Text) ? "♪" : Line.Text;
    public string? Pronunciation => Line.Romanization;
    public string? Translation => Line.Translation;
    public double TranslationFontSize => SmallSize;

    [ObservableProperty] private bool _isActive;
    [ObservableProperty] private bool _showOriginal = true;
    [ObservableProperty] private bool _showPronunciation;
    [ObservableProperty] private bool _showTranslation;
    [ObservableProperty] private double _originalFontSize = LargeSize;
    [ObservableProperty] private double _pronunciationFontSize = SmallSize;

    public void ApplyLayout(LyricsLayout layout)
    {
        // Una línea sin texto en un idioma simplemente no muestra ese renglón.
        ShowPronunciation = layout.ShowPronunciation && !string.IsNullOrWhiteSpace(Line.Romanization);
        ShowTranslation = layout.ShowTranslation && !string.IsNullOrWhiteSpace(Line.Translation);

        // Si el original está oculto pero esta línea no tiene traducción, se muestra
        // igualmente: es mejor que dejar un hueco en blanco.
        ShowOriginal = layout.ShowOriginal || (!ShowPronunciation && !ShowTranslation);

        OriginalFontSize = layout.EmphasizePronunciation ? SmallSize : LargeSize;
        PronunciationFontSize = layout.EmphasizePronunciation ? LargeSize : SmallSize;
    }
}