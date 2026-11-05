using MusicPlayer.Core.Services;

namespace MusicPlayer.Core.Tests;

public class PlaybackShortcutsTests
{
    private static readonly TimeSpan Duration = TimeSpan.FromMinutes(3);

    [Fact]
    public void SeekTarget_Forward_AddsFiveSeconds()
    {
        var target = PlaybackShortcuts.SeekTarget(TimeSpan.FromSeconds(60), Duration, +1);
        Assert.Equal(TimeSpan.FromSeconds(65), target);
    }

    [Fact]
    public void SeekTarget_Backward_SubtractsFiveSeconds()
    {
        var target = PlaybackShortcuts.SeekTarget(TimeSpan.FromSeconds(60), Duration, -1);
        Assert.Equal(TimeSpan.FromSeconds(55), target);
    }

    [Fact]
    public void SeekTarget_BackwardNearStart_ClampsToZero()
    {
        var target = PlaybackShortcuts.SeekTarget(TimeSpan.FromSeconds(3), Duration, -1);
        Assert.Equal(TimeSpan.Zero, target);
    }

    [Theory]
    [InlineData(175)] // 175 + 5 = duración exacta
    [InlineData(178)] // pasaría del final
    public void SeekTarget_ForwardReachingTheEnd_ReturnsNull(double seconds)
    {
        Assert.Null(PlaybackShortcuts.SeekTarget(TimeSpan.FromSeconds(seconds), Duration, +1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void SeekTarget_WithoutDuration_ReturnsNull(double durationSeconds)
    {
        var duration = TimeSpan.FromSeconds(durationSeconds);
        Assert.Null(PlaybackShortcuts.SeekTarget(TimeSpan.Zero, duration, +1));
        Assert.Null(PlaybackShortcuts.SeekTarget(TimeSpan.Zero, duration, -1));
    }

    [Fact]
    public void SeekTarget_NoDirection_ReturnsNull()
    {
        Assert.Null(PlaybackShortcuts.SeekTarget(TimeSpan.FromSeconds(60), Duration, 0));
    }

    [Theory]
    [InlineData(0.50, +1, 0.55)]
    [InlineData(0.50, -1, 0.45)]
    [InlineData(0.00, +1, 0.05)]
    public void StepVolume_MovesOneStep(double current, int direction, double expected)
    {
        Assert.Equal(expected, PlaybackShortcuts.StepVolume(current, direction));
    }

    [Fact]
    public void StepVolume_ClampsAtTheLimits()
    {
        Assert.Equal(1.0, PlaybackShortcuts.StepVolume(1.0, +1));
        Assert.Equal(0.0, PlaybackShortcuts.StepVolume(0.0, -1));
    }

    [Fact]
    public void StepVolume_TwentyStepsUp_ReachExactlyOne()
    {
        var volume = 0.0;
        for (var i = 0; i < 20; i++) volume = PlaybackShortcuts.StepVolume(volume, +1);
        Assert.Equal(1.0, volume);
    }

    [Fact]
    public void StepVolume_TwentyStepsDown_ReachExactlyZero()
    {
        var volume = 1.0;
        for (var i = 0; i < 20; i++) volume = PlaybackShortcuts.StepVolume(volume, -1);
        Assert.Equal(0.0, volume);
    }

    [Fact]
    public void StepVolume_NotMultipleOfStep_MovesToNearestMultipleInThatDirection()
    {
        Assert.Equal(0.35, PlaybackShortcuts.StepVolume(0.33, +1));
        Assert.Equal(0.30, PlaybackShortcuts.StepVolume(0.33, -1));
    }

    [Fact]
    public void StepVolume_OutOfRangeInput_IsClamped()
    {
        Assert.Equal(1.0, PlaybackShortcuts.StepVolume(1.5, +1));
        Assert.Equal(0.0, PlaybackShortcuts.StepVolume(-0.2, -1));
    }
}
