using MusicPlayer.Core.Services;

namespace MusicPlayer.Core.Tests;

public class IslandPlacementTests
{
    [Fact]
    public void TopCenter_At100Percent_UsesDipsAsPixels() =>
        // 1920 px de ancho, ventana de 212 DIPs: (1920 - 212) / 2 = 854.
        Assert.Equal((854, 8), IslandPlacement.TopCenter(0, 0, 1920, 1.0, 212, 8));

    [Fact]
    public void TopCenter_At150Percent_ScalesWidthAndMargin() =>
        // 212 DIPs × 1,5 = 318 px; (2880 - 318) / 2 = 1281; 8 DIPs × 1,5 = 12 px.
        Assert.Equal((1281, 12), IslandPlacement.TopCenter(0, 0, 2880, 1.5, 212, 8));

    [Fact]
    public void TopCenter_At125Percent_RoundsToWholePixels() =>
        // 212 × 1,25 = 265 px; (2400 - 265) / 2 = 1067; 8 × 1,25 = 10 px.
        Assert.Equal((1067, 10), IslandPlacement.TopCenter(0, 0, 2400, 1.25, 212, 8));

    [Fact]
    public void TopCenter_SecondaryOrigin_AddsTheAreaOffset() =>
        // Pantalla principal a la derecha de otra, o con la barra de tareas arriba (área desplazada 48 px).
        Assert.Equal((1920 + 854, 48 + 8), IslandPlacement.TopCenter(1920, 48, 1920, 1.0, 212, 8));

    [Fact]
    public void TopCenter_NegativeOrigin_IsAllowed() =>
        // En Windows la pantalla principal puede no empezar en (0, 0) respecto al escritorio virtual.
        Assert.Equal((-1920 + 854, 8), IslandPlacement.TopCenter(-1920, 0, 1920, 1.0, 212, 8));

    [Theory]
    [InlineData(36, 1.0, 36)]
    [InlineData(36, 1.25, 45)]
    [InlineData(36, 1.5, 54)]
    [InlineData(36, 1.75, 63)]
    [InlineData(5, 1.25, 6)] // 6,25 se redondea a 6
    public void ToPhysical_RoundsTheScaledValue(double dips, double scaling, int expected) =>
        Assert.Equal(expected, IslandPlacement.ToPhysical(dips, scaling));
}
