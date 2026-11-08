using MusicPlayer.Core.Services;

namespace MusicPlayer.Core.Tests;

public class LyricsScrollTests
{
    private static readonly double[] Heights = [40, 80, 40, 40]; // la segunda ocupa dos renglones

    [Fact]
    public void LineTops_UseMeasuredHeightsAndSpacing()
    {
        Assert.Equal([0, 60, 160, 220], LyricsScroll.LineTops(Heights, spacing: 20));
    }

    [Fact]
    public void LineTops_NegativeHeight_CountsAsZero()
    {
        Assert.Equal([0, 10, 50], LyricsScroll.LineTops([-5, 30, 10], spacing: 10));
    }

    [Theory]
    [InlineData(0, 350)]       // ancla a 0,35 × 1000
    [InlineData(1, 290)]       // 350 − 60
    [InlineData(2, 190)]       // 350 − 160 (la línea de dos renglones cuenta entera)
    [InlineData(3, 130)]
    public void TargetOffset_PutsActiveLineAtAnchor(int active, double expected)
    {
        Assert.Equal(expected, LyricsScroll.TargetOffset(Heights, 20, active, viewportHeight: 1000));
    }

    [Theory]
    [InlineData(-1, 350)]  // antes de la primera línea: como la primera
    [InlineData(9, 130)]   // fuera de rango: la última
    public void TargetOffset_ClampsIndex(int active, double expected)
    {
        Assert.Equal(expected, LyricsScroll.TargetOffset(Heights, 20, active, viewportHeight: 1000));
    }

    [Fact]
    public void TargetOffset_SeekBackwards_DependsOnlyOnActiveLine()
    {
        var forward = LyricsScroll.TargetOffset(Heights, 20, 3, 1000);
        var back = LyricsScroll.TargetOffset(Heights, 20, 1, 1000);

        Assert.Equal(LyricsScroll.TargetOffset(Heights, 20, 1, 1000), back);
        Assert.True(back > forward); // vuelve a bajar
    }

    [Fact]
    public void TargetOffset_CustomAnchor_AndNoLines()
    {
        Assert.Equal(500, LyricsScroll.TargetOffset(Heights, 20, 0, 1000, anchor: 0.5));
        Assert.Equal(350, LyricsScroll.TargetOffset([], 20, 0, 1000));
    }

    [Theory]
    [InlineData(5, 0)]    // la nueva activa arranca ya
    [InlineData(4, 0)]    // la anterior, también
    [InlineData(6, 100)]
    [InlineData(7, 200)]
    [InlineData(20, 400)] // tope
    public void CascadeDelay_Forward_LinesBelowLag(int line, int expectedMs)
    {
        Assert.Equal(TimeSpan.FromMilliseconds(expectedMs), LyricsScroll.CascadeDelay(line, activeIndex: 5, previousActiveIndex: 4));
    }

    [Theory]
    [InlineData(5, 0)]
    [InlineData(6, 0)]
    [InlineData(4, 100)]
    [InlineData(2, 300)]
    public void CascadeDelay_Backward_LinesAboveLag(int line, int expectedMs)
    {
        Assert.Equal(TimeSpan.FromMilliseconds(expectedMs), LyricsScroll.CascadeDelay(line, activeIndex: 5, previousActiveIndex: 6));
    }

    [Theory]
    [InlineData(9, 5)]   // salto adelante
    [InlineData(1, 5)]   // salto atrás
    [InlineData(5, 5)]   // sin cambio
    [InlineData(0, -1)]  // primera línea desde «antes de empezar»: avance normal
    public void CascadeDelay_JumpsMoveEverythingTogether(int active, int previous)
    {
        var expectCascade = active - previous is 1 or -1;
        var delay = LyricsScroll.CascadeDelay(active + 3, active, previous);

        Assert.Equal(expectCascade ? TimeSpan.FromMilliseconds(300) : TimeSpan.Zero, delay);
    }

    [Theory]
    [InlineData(200, 200)]      // dentro: igual
    [InlineData(1000, 350)]     // más allá de la primera línea en el ancla
    [InlineData(-500, 130)]     // más allá de la última línea en el ancla
    [InlineData(350, 350)]
    [InlineData(130, 130)]
    public void ClampManualOffset_KeepsSomeLineAtTheAnchor(double offset, double expected)
    {
        Assert.Equal(expected, LyricsScroll.ClampManualOffset(Heights, 20, offset, viewportHeight: 1000));
    }

    [Fact]
    public void ClampManualOffset_NoLines_IsAnchor()
    {
        Assert.Equal(350, LyricsScroll.ClampManualOffset([], 20, -80, 1000));
    }

    [Fact]
    public void CascadeDelay_CustomStepAndMax()
    {
        var delay = LyricsScroll.CascadeDelay(10, 5, 4, step: TimeSpan.FromMilliseconds(50), max: TimeSpan.FromMilliseconds(120));

        Assert.Equal(TimeSpan.FromMilliseconds(120), delay);
    }
}
