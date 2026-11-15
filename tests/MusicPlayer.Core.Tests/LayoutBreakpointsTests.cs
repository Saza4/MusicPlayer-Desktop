using MusicPlayer.Core.Services;

namespace MusicPlayer.Core.Tests;

// docs/specs/responsive-layout.md, criterio 8.
public class LayoutBreakpointsTests
{
    [Theory]
    [InlineData(640, true, true, true, true)]
    [InlineData(799.9, true, true, true, true)]
    [InlineData(800, true, true, false, false)]
    [InlineData(960, true, true, false, false)]
    [InlineData(1099.9, true, true, false, false)]
    [InlineData(1100, false, false, false, false)]
    [InlineData(1920, false, false, false, false)]
    public void For_SwitchesAtEachThreshold(double width, bool topBar, bool playerBar, bool volume, bool sidebar)
    {
        Assert.Equal(new WindowLayout(topBar, playerBar, volume, sidebar), LayoutBreakpoints.For(width));
    }

    [Fact]
    public void TheTopBarThresholdIsStill1100()
    {
        Assert.Equal(1100, LayoutBreakpoints.CompactBelow);
    }

    [Theory]
    [InlineData(863, true, true)]    // 863 - 220 - 380 - 24 = 239
    [InlineData(864, true, false)]   // 240 justos: caben los dos
    [InlineData(960, true, false)]
    [InlineData(640, false, true)]   // con la barra lateral plegada: 236
    [InlineData(644, false, false)]
    [InlineData(700, false, false)]
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
