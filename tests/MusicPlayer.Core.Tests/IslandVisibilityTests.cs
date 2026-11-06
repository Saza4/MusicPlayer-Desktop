using MusicPlayer.Core.Services;

namespace MusicPlayer.Core.Tests;

public class IslandVisibilityTests
{
    [Fact]
    public void ShouldShow_TrackLoadedAndMainWindowInactive_Shows() =>
        Assert.True(IslandVisibility.ShouldShow(
            hasTrack: true, showIsland: true, isImmersive: false, isMainWindowActive: false, isModalOpen: false));

    [Theory]
    [InlineData(false, true, false, false, false)] // sin pista
    [InlineData(true, false, false, false, false)] // ajuste desactivado
    [InlineData(true, true, true, false, false)]   // modo inmersivo
    [InlineData(true, true, false, true, false)]   // ventana principal activa
    [InlineData(true, true, false, false, true)]   // selector de archivos abierto
    [InlineData(false, false, true, true, true)]   // nada se cumple
    public void ShouldShow_AnyConditionMissing_Hides(
        bool hasTrack, bool showIsland, bool isImmersive, bool isMainWindowActive, bool isModalOpen) =>
        Assert.False(IslandVisibility.ShouldShow(hasTrack, showIsland, isImmersive, isMainWindowActive, isModalOpen));
}
