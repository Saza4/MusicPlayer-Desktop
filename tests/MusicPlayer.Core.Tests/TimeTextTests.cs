using MusicPlayer.Core.Services;

namespace MusicPlayer.Core.Tests;

public class TimeTextTests
{
    private static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);

    [Fact]
    public void Remaining_UsesTheTypographicMinus() =>
        Assert.Equal("−1:31", TimeText.Remaining(S(155), S(246)));

    [Fact]
    public void Remaining_AtTheStart_IsTheWholeDuration() =>
        Assert.Equal("−4:55", TimeText.Remaining(TimeSpan.Zero, S(295)));

    [Fact]
    public void Remaining_AtTheEnd_IsZero() =>
        Assert.Equal("−0:00", TimeText.Remaining(S(295), S(295)));

    [Fact]
    public void Remaining_PastTheEnd_NeverGoesNegative() =>
        Assert.Equal("−0:00", TimeText.Remaining(S(300), S(295)));

    [Fact]
    public void Remaining_TruncatesLikeTheElapsedTime()
    {
        // 1,9 s transcurridos se muestran como 0:01; con 10,4 s de duración (0:10) quedan 0:09.
        Assert.Equal("−0:09", TimeText.Remaining(S(1.9), S(10.4)));
    }

    [Fact]
    public void Remaining_UnderOneSecond_ShowsZero() =>
        Assert.Equal("−0:00", TimeText.Remaining(TimeSpan.Zero, S(0.6)));

    [Fact]
    public void Remaining_OneHourOrMore_ShowsHours() =>
        Assert.Equal("−1:02:03", TimeText.Remaining(S(10), S(3733)));

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Remaining_UnknownDuration_IsEmpty(double duration) =>
        Assert.Equal("", TimeText.Remaining(S(10), S(duration)));
}
