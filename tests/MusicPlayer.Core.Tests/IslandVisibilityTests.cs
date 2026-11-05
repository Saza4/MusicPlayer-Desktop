using MusicPlayer.Core.Services;

namespace MusicPlayer.Core.Tests;

public class IslandVisibilityTests
{
    [Fact]
    public void ShouldShow_TrackLoadedAndMainWindowInactive_Shows() =>
        Assert.True(IslandVisibility.ShouldShow(hasTrack: true, showIsland: true, isImmersive: false, isMainWindowActive: false));

    [Theory]
    [InlineData(false, true, false, false)] // sin pista
    [InlineData(true, false, false, false)] // ajuste desactivado
    [InlineData(true, true, true, false)]   // modo inmersivo
    [InlineData(true, true, false, true)]   // ventana principal activa
    [InlineData(false, false, true, true)]  // nada se cumple
    public void ShouldShow_AnyConditionMissing_Hides(bool hasTrack, bool showIsland, bool isImmersive, bool isMainWindowActive) =>
        Assert.False(IslandVisibility.ShouldShow(hasTrack, showIsland, isImmersive, isMainWindowActive));
}
