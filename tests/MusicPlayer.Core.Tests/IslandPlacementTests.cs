using MusicPlayer.Core.Services;

namespace MusicPlayer.Core.Tests;

public class IslandPlacementTests
{
    // 372 DIPs: la ventana de la isla (340 de pastilla expandida + 2·16 de sombra).

    [Fact]
    public void TopCenter_At100Percent_UsesDipsAsPixels() =>
        // 1920 px de ancho, ventana de 372 DIPs: (1920 - 372) / 2 = 774.
        Assert.Equal((774, 8), IslandPlacement.TopCenter(0, 0, 1920, 1.0, 372, 8));

    [Fact]
    public void TopCenter_At150Percent_ScalesWidthAndMargin() =>
        // 372 DIPs × 1,5 = 558 px; (2880 - 558) / 2 = 1161; 8 DIPs × 1,5 = 12 px.
        Assert.Equal((1161, 12), IslandPlacement.TopCenter(0, 0, 2880, 1.5, 372, 8));

    [Fact]
    public void TopCenter_At125Percent_RoundsToWholePixels() =>
        // 372 × 1,25 = 465 px; (2400 - 465) / 2 = 967 (se trunca el medio píxel); 8 × 1,25 = 10 px.
        Assert.Equal((967, 10), IslandPlacement.TopCenter(0, 0, 2400, 1.25, 372, 8));

    [Fact]
    public void TopCenter_SecondaryOrigin_AddsTheAreaOffset() =>
        // Pantalla principal a la derecha de otra, o con la barra de tareas arriba (área desplazada 48 px).
        Assert.Equal((1920 + 774, 48 + 8), IslandPlacement.TopCenter(1920, 48, 1920, 1.0, 372, 8));

    [Fact]
    public void TopCenter_NegativeOrigin_IsAllowed() =>
        // En Windows la pantalla principal puede no empezar en (0, 0) respecto al escritorio virtual.
        Assert.Equal((-1920 + 774, 8), IslandPlacement.TopCenter(-1920, 0, 1920, 1.0, 372, 8));

    [Theory]
    [InlineData(36, 1.0, 36)]
    [InlineData(36, 1.25, 45)]
    [InlineData(36, 1.5, 54)]
    [InlineData(36, 1.75, 63)]
    [InlineData(5, 1.25, 6)] // 6,25 se redondea a 6
    public void ToPhysical_RoundsTheScaledValue(double dips, double scaling, int expected) =>
        Assert.Equal(expected, IslandPlacement.ToPhysical(dips, scaling));
}
