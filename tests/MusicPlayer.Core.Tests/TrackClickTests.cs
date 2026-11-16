using MusicPlayer.Core.Services;

namespace MusicPlayer.Core.Tests;

// docs/specs/settings-panel.md, criterios 9 y 11.
public class TrackClickTests
{
    [Fact]
    public void SettingOff_OneClickOnlySelects()
    {
        Assert.False(TrackClick.ShouldPlay(false, 1, isLeftButton: true, hasSelectionModifier: false, isPlayingThisTrack: false));
    }

    [Fact]
    public void SettingOff_DoubleClickPlays_EvenTheOneAlreadyPlaying()
    {
        Assert.True(TrackClick.ShouldPlay(false, 2, isLeftButton: true, hasSelectionModifier: false, isPlayingThisTrack: false));
        Assert.True(TrackClick.ShouldPlay(false, 2, isLeftButton: true, hasSelectionModifier: false, isPlayingThisTrack: true));
    }

    [Fact]
    public void SettingOn_OneClickPlays()
    {
        Assert.True(TrackClick.ShouldPlay(true, 1, isLeftButton: true, hasSelectionModifier: false, isPlayingThisTrack: false));
    }

    [Fact]
    public void SettingOn_WithCtrlOrShift_OnlySelects()
    {
        Assert.False(TrackClick.ShouldPlay(true, 1, isLeftButton: true, hasSelectionModifier: true, isPlayingThisTrack: false));
    }

    [Fact]
    public void SettingOn_TheSecondClickOfADoubleClick_DoesNotRestart()
    {
        // El primer clic ya la puso a sonar; llegue el segundo como «doble clic» o como otro clic, no la reinicia.
        Assert.False(TrackClick.ShouldPlay(true, 2, isLeftButton: true, hasSelectionModifier: false, isPlayingThisTrack: true));
        Assert.False(TrackClick.ShouldPlay(true, 1, isLeftButton: true, hasSelectionModifier: false, isPlayingThisTrack: true));
        Assert.False(TrackClick.ShouldPlay(true, 2, isLeftButton: true, hasSelectionModifier: false, isPlayingThisTrack: false));
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 1)]
    [InlineData(true, 2)]
    public void TheRightButtonNeverPlays(bool singleClickEnabled, int clickCount)
    {
        Assert.False(TrackClick.ShouldPlay(singleClickEnabled, clickCount, isLeftButton: false, hasSelectionModifier: false, isPlayingThisTrack: false));
    }
}
