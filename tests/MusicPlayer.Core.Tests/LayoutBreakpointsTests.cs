using MusicPlayer.Core.Services;

namespace MusicPlayer.Core.Tests;

// docs/specs/responsive-layout.md, criterio 8.
public class LayoutBreakpointsTests
{
    [Theory]
    [InlineData(640, true, true, true)]
    [InlineData(799.9, true, true, true)]
    [InlineData(800, true, false, false)]
    [InlineData(960, true, false, false)]
    [InlineData(1099.9, true, false, false)]
    [InlineData(1100, false, false, false)]
    [InlineData(1920, false, false, false)]
    public void For_SwitchesAtEachThreshold(double width, bool topBar, bool volume, bool sidebar)
    {
        Assert.Equal(new WindowLayout(topBar, volume, sidebar), LayoutBreakpoints.For(width));
    }

    // Barra de reproducción: columna central fija entre dos lados iguales (docs/specs/redesign-mockups.md).
    [Theory]
    [InlineData(1280, false, 560)] // 1240 - 520 = 720: se queda en el máximo
    [InlineData(1000, false, 440)] // 960 - 520
    [InlineData(800, false, 240)]  // 760 - 520
    [InlineData(640, true, 272)]   // 600 - 328
    [InlineData(400, true, 230)]   // nunca menos que la fila de controles
    public void PlayerCenterWidth_LeavesTwoEqualSides(double width, bool volumeInFlyout, double expected)
    {
        Assert.Equal(expected, LayoutBreakpoints.PlayerCenterWidth(width, volumeInFlyout));
    }

    [Theory]
    [InlineData(640, true)]
    [InlineData(799.9, true)]
    [InlineData(800, false)]
    [InlineData(1099.9, false)]
    public void PlayerCenterWidth_LeavesRoomForTheRightGroup(double width, bool volumeInFlyout)
    {
        var side = (width - 40 - LayoutBreakpoints.PlayerCenterWidth(width, volumeInFlyout)) / 2;
        Assert.True(side >= (volumeInFlyout ? 164 : 260));
    }

    [Fact]
    public void TheTopBarThresholdIsStill1100()
    {
        Assert.Equal(1100, LayoutBreakpoints.CompactBelow);
    }

    [Theory]
    [InlineData(803, true, true)]    // 803 - 232 - 300 - 32 = 239
    [InlineData(804, true, false)]   // 240 justos: caben los dos
    [InlineData(960, true, false)]
    [InlineData(571, false, true)]   // con la barra lateral plegada: 239
    [InlineData(572, false, false)]
    [InlineData(640, false, false)]  // al ancho mínimo de la ventana, la letra cabe junto a la lista
    public void LyricsReplacesList_WhenTheListWouldGetLessThan240(double width, bool sidebarVisible, bool expected)
    {
        Assert.Equal(expected, LayoutBreakpoints.LyricsReplacesList(width, sidebarVisible));
    }

    [Theory]
    [InlineData(400, 176)]
    [InlineData(600, 376)]
    [InlineData(300, 120)] // nunca menos del mínimo
    public void EditPanelMaxHeight_LeavesRoomForTheBars(double height, double expected)
    {
        Assert.Equal(expected, LayoutBreakpoints.EditPanelMaxHeight(height));
    }
}
