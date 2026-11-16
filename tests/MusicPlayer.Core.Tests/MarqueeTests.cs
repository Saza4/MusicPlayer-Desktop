using MusicPlayer.Core.Services;

namespace MusicPlayer.Core.Tests;

// docs/specs/island-title-and-close.md, criterios 3 a 5.
public class MarqueeTests
{
    private static readonly TimeSpan Pause = TimeSpan.FromSeconds(1);
    private const double Speed = 20; // unidades por segundo
    private const double Gap = 40;

    // Texto de 160 en un hueco de 100: recorre 160 + 40 = 200 en 10 s; la vuelta completa dura 11 s.
    private static double At(double seconds, double text = 160, double viewport = 100) =>
        Marquee.Offset(TimeSpan.FromSeconds(seconds), text, viewport, Speed, Pause, Gap);

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(500)]
    public void TextThatFits_NeverMoves(double seconds)
    {
        Assert.Equal(0, At(seconds, text: 80));
        Assert.Equal(0, At(seconds, text: 100)); // justo lo que mide el hueco
    }

    [Fact]
    public void StartPause_KeepsTheTextStill()
    {
        Assert.Equal(0, At(0));
        Assert.Equal(0, At(0.5));
        Assert.Equal(0, At(1));
    }

    [Fact]
    public void AfterThePause_MovesAtConstantSpeed()
    {
        Assert.Equal(20, At(2), 6);
        Assert.Equal(100, At(6), 6); // mitad del recorrido
        Assert.Equal(190, At(10.5), 6); // ya dentro del hueco
    }

    [Fact]
    public void AfterTheGap_StartsOver()
    {
        Assert.Equal(0, At(11), 6);      // la segunda copia está donde empezó la primera: nueva pausa
        Assert.Equal(0, At(11.5), 6);
        Assert.Equal(20, At(13), 6);
        Assert.Equal(At(3), At(3 + 11 * 40), 6); // muchas vueltas después, lo mismo
    }

    [Fact]
    public void NeverExceedsTextPlusGap()
    {
        for (var s = 0.0; s < 30; s += 0.07)
            Assert.InRange(At(s), 0, 200);
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(160, 0)]
    [InlineData(-5, 100)]
    [InlineData(160, -1)]
    [InlineData(double.NaN, 100)]
    [InlineData(160, double.NaN)]
    [InlineData(double.PositiveInfinity, 100)]
    public void UselessWidths_GiveZero(double text, double viewport)
    {
        Assert.Equal(0, At(5, text, viewport));
    }

    [Fact]
    public void UselessSpeedOrNegativeTime_GiveZero()
    {
        Assert.Equal(0, Marquee.Offset(TimeSpan.FromSeconds(5), 160, 100, 0, Pause, Gap));
        Assert.Equal(0, Marquee.Offset(TimeSpan.FromSeconds(5), 160, 100, double.NaN, Pause, Gap));
        Assert.Equal(0, Marquee.Offset(TimeSpan.FromSeconds(-5), 160, 100, Speed, Pause, Gap));
    }

    [Fact]
    public void NoPauseAndNoGap_StillLoops()
    {
        // Recorre 160 en 8 s y vuelve a empezar.
        Assert.Equal(80, Marquee.Offset(TimeSpan.FromSeconds(4), 160, 100, Speed, TimeSpan.Zero, 0), 6);
        Assert.Equal(20, Marquee.Offset(TimeSpan.FromSeconds(9), 160, 100, Speed, TimeSpan.Zero, -3), 6);
    }

    [Fact]
    public void Fits_ComparesWidths()
    {
        Assert.True(Marquee.Fits(80, 100));
        Assert.True(Marquee.Fits(100, 100));
        Assert.False(Marquee.Fits(101, 100));
        Assert.True(Marquee.Fits(0, 0)); // sin texto no hay nada que recortar
    }

    [Theory]
    [InlineData("Canción", "Artista", "Canción — Artista")]
    [InlineData("Canción", "", "Canción")]
    [InlineData("Canción", null, "Canción")]
    [InlineData("Canción", "   ", "Canción")]
    [InlineData("  Canción ", " Artista ", "Canción — Artista")]
    [InlineData("", "Artista", "Artista")]
    [InlineData(null, null, "")]
    public void Label_JoinsTitleAndArtist(string? title, string? artist, string expected)
    {
        Assert.Equal(expected, Marquee.Label(title, artist));
    }
}
