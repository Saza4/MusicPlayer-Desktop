using MusicPlayer.Core.Services;

namespace MusicPlayer.Core.Tests;

public class LyricsAppearanceTests
{
    [Theory]
    [InlineData(0, 1.0)]
    [InlineData(10, 0.2)]   // más allá del alcance: el mínimo
    [InlineData(5, 0.6)]    // a mitad: smoothstep(0,5) = 0,5
    [InlineData(-5, 0.6)]   // simétrica
    public void Falloff_GoesFromOneToFloor(double distance, double expected)
    {
        Assert.Equal(expected, LyricsAppearance.Falloff(distance, range: 10, floor: 0.2), 6);
    }

    [Fact]
    public void Falloff_IsMonotonic()
    {
        double previous = 1;
        for (double d = 0; d <= 12; d += 0.25)
        {
            var value = LyricsAppearance.Falloff(d, 10, 0.2);
            Assert.True(value <= previous + 1e-12, $"Sube en {d}");
            previous = value;
        }
    }

    [Fact]
    public void Falloff_ZeroRange_IsStep()
    {
        Assert.Equal(1, LyricsAppearance.Falloff(0, 0, 0.3));
        Assert.Equal(0.3, LyricsAppearance.Falloff(0.1, 0, 0.3));
    }

    [Fact]
    public void LineOpacityAndScale_ActiveIsFull_FarIsFloor()
    {
        Assert.Equal(1, LyricsAppearance.LineOpacity(0));
        Assert.Equal(1, LyricsAppearance.LineScale(0));
        Assert.Equal(LyricsAppearance.MinOpacity, LyricsAppearance.LineOpacity(9), 6);
        Assert.Equal(LyricsAppearance.MinScale, LyricsAppearance.LineScale(-9), 6);
    }

    [Fact]
    public void LineOpacity_NeighbourIsBetween()
    {
        var neighbour = LyricsAppearance.LineOpacity(1);

        Assert.InRange(neighbour, LyricsAppearance.MinOpacity + 0.01, 0.99);
    }

    [Theory]
    [InlineData(500, 40, 1.0)]    // en medio del panel
    [InlineData(-100, 40, 0.0)]   // por encima del panel
    [InlineData(1100, 40, 0.0)]   // por debajo
    [InlineData(-20, 40, 0.0)]    // cortada por el borde superior: invisible, no a medias
    [InlineData(0, 40, 0.0)]      // tocando el borde superior
    [InlineData(150, 40, 1.0)]    // justo fuera de la franja (15 % de 1000)
    [InlineData(75, 40, 0.5)]     // a mitad de la franja
    [InlineData(885, 40, 0.5)]    // simétrico abajo: su borde inferior a 925
    [InlineData(970, 40, 0.0)]    // cortada por el borde inferior
    public void EdgeOpacity_FadesInsideTheBands(double top, double height, double expected)
    {
        Assert.Equal(expected, LyricsAppearance.EdgeOpacity(top, height, viewportHeight: 1000, fadeHeight: 150), 6);
    }

    [Fact]
    public void EdgeOpacity_NoViewportOrNoFade()
    {
        Assert.Equal(0, LyricsAppearance.EdgeOpacity(0, 10, 0, 100));
        Assert.Equal(1, LyricsAppearance.EdgeOpacity(5, 10, 100, 0));
    }
}
