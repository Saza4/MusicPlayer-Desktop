using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Tests;

public class LyricsDisplayOptionsTests
{
    [Fact]
    public void Default_ShowsOnlyOriginal()
    {
        var layout = LyricsDisplayOptions.Default.Resolve(hasTranslation: true, hasPronunciation: true);

        Assert.True(layout.ShowOriginal);
        Assert.False(layout.ShowTranslation);
        Assert.False(layout.ShowPronunciation);
        Assert.False(layout.EmphasizePronunciation);
    }

    [Fact]
    public void Translation_IsShownOnlyWhenTheSongHasIt()
    {
        var options = new LyricsDisplayOptions(ShowTranslation: true);

        Assert.True(options.Resolve(hasTranslation: true, hasPronunciation: false).ShowTranslation);
        Assert.False(options.Resolve(hasTranslation: false, hasPronunciation: false).ShowTranslation);
    }

    [Fact]
    public void HideOriginal_WithTranslationVisible_HidesIt()
    {
        var options = new LyricsDisplayOptions(ShowTranslation: true, HideOriginal: true);

        Assert.False(options.Resolve(hasTranslation: true, hasPronunciation: false).ShowOriginal);
    }

    [Fact]
    public void HideOriginal_WithNothingElseVisible_KeepsOriginal()
    {
        var options = new LyricsDisplayOptions(ShowTranslation: true, HideOriginal: true);

        // La canción no tiene traducción: ocultar el original dejaría la pantalla vacía.
        Assert.True(options.Resolve(hasTranslation: false, hasPronunciation: false).ShowOriginal);
    }

    [Fact]
    public void EmphasizePronunciation_RequiresVisiblePronunciation()
    {
        var options = new LyricsDisplayOptions(EmphasizePronunciation: true);
        Assert.False(options.Resolve(hasTranslation: false, hasPronunciation: true).EmphasizePronunciation);

        options = new LyricsDisplayOptions(ShowPronunciation: true, EmphasizePronunciation: true);
        Assert.True(options.Resolve(hasTranslation: false, hasPronunciation: true).EmphasizePronunciation);
    }
}