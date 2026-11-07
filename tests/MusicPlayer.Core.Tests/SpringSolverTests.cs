using MusicPlayer.Core.Services;

namespace MusicPlayer.Core.Tests;

public class SpringSolverTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 24); // ~24 fps, como en M0

    private static (SpringState State, int Frames) RunUntilSettled(SpringState state, double target, Spring spring,
        TimeSpan frame, int maxFrames = 1000)
    {
        for (int i = 1; i <= maxFrames; i++)
        {
            state = SpringSolver.Step(state, target, frame, spring);
            if (SpringSolver.IsSettled(state, target)) return (state, i);
        }
        return (state, -1);
    }

    [Fact]
    public void FromResponse_ComputesStiffnessAndDamping()
    {
        var spring = Spring.FromResponse(1, 1); // ω = 2π

        Assert.Equal(4 * Math.PI * Math.PI, spring.Stiffness, 9);
        Assert.Equal(4 * Math.PI, spring.Damping, 9);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1, -0.1)]
    public void FromResponse_RejectsInvalidValues(double response, double damping)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Spring.FromResponse(response, damping));
    }

    [Fact]
    public void Default_SettlesOnTargetInUnderASecondAndAHalf()
    {
        var (state, frames) = RunUntilSettled(SpringState.At(0), -120, Spring.Default, Frame);

        Assert.InRange(frames, 1, 36); // ≤ 1,5 s a 24 fps
        Assert.Equal(-120, state.Position, 0);
    }

    [Fact]
    public void CriticallyDamped_NeverOvershoots()
    {
        var spring = Spring.FromResponse(0.5, 1);
        var state = SpringState.At(0);

        for (int i = 0; i < 100; i++)
        {
            state = SpringSolver.Step(state, 100, Frame, spring);
            Assert.True(state.Position <= 100.001, $"Se pasó del objetivo en el fotograma {i}: {state.Position}");
        }
    }

    [Fact]
    public void Default_OvershootsOnlySlightly()
    {
        var state = SpringState.At(0);
        double max = 0;

        for (int i = 0; i < 100; i++)
        {
            state = SpringSolver.Step(state, 100, Frame, Spring.Default);
            max = Math.Max(max, state.Position);
        }

        Assert.InRange(max, 100, 102); // el rebote de spring() es de ~1 %
    }

    [Fact]
    public void Step_IsDeterministic()
    {
        var a = SpringSolver.Step(new SpringState(3, -40), 50, TimeSpan.FromMilliseconds(37), Spring.Default);
        var b = SpringSolver.Step(new SpringState(3, -40), 50, TimeSpan.FromMilliseconds(37), Spring.Default);

        Assert.Equal(a, b);
    }

    [Theory]
    [InlineData(50)]   // 2 × 50 ms
    [InlineData(41)]   // fotogramas de ~24 fps que no dividen el tiempo exacto
    [InlineData(17)]
    public void Step_FrameSplitting_ChangesResultByLessThanHalfAPixel(int frameMs)
    {
        var whole = SpringSolver.Step(SpringState.At(0), 50, TimeSpan.FromMilliseconds(400), Spring.Default);

        var split = SpringState.At(0);
        var left = 400;
        while (left > 0)
        {
            var step = Math.Min(frameMs, left);
            split = SpringSolver.Step(split, 50, TimeSpan.FromMilliseconds(step), Spring.Default);
            left -= step;
        }

        Assert.InRange(Math.Abs(whole.Position - split.Position), 0, 0.5);
    }

    [Fact]
    public void Step_SlowFrames_StayStable()
    {
        var (_, frames) = RunUntilSettled(SpringState.At(0), 300, Spring.Default, TimeSpan.FromMilliseconds(200));

        Assert.True(frames > 0, "Con fotogramas de 200 ms no llegó (inestable)");
    }

    [Fact]
    public void Step_ZeroOrNegativeElapsed_ChangesNothing()
    {
        var state = new SpringState(10, 5);

        Assert.Equal(state, SpringSolver.Step(state, 0, TimeSpan.Zero, Spring.Default));
        Assert.Equal(state, SpringSolver.Step(state, 0, TimeSpan.FromMilliseconds(-5), Spring.Default));
    }

    [Fact]
    public void Step_LongPause_SnapsToTarget()
    {
        var state = SpringSolver.Step(new SpringState(0, 400), 80, TimeSpan.FromSeconds(3), Spring.Default);

        Assert.Equal(SpringState.At(80), state);
    }

    [Fact]
    public void Step_TargetChangeMidFlight_KeepsVelocity()
    {
        var moving = SpringSolver.Step(SpringState.At(0), -100, Frame * 3, Spring.Default);
        var redirected = SpringSolver.Step(moving, 50, Frame, Spring.Default);

        Assert.True(moving.Velocity < 0);
        Assert.True(redirected.Position < moving.Position, "Sin salto: la inercia sigue hacia abajo un instante");
        var (state, frames) = RunUntilSettled(redirected, 50, Spring.Default, Frame);
        Assert.True(frames > 0);
        Assert.Equal(50, state.Position, 0);
    }

    [Theory]
    [InlineData(100.4, 0, true)]
    [InlineData(101, 0, false)]
    [InlineData(100, 3, false)]
    public void IsSettled_UsesPositionAndVelocity(double position, double velocity, bool expected)
    {
        Assert.Equal(expected, SpringSolver.IsSettled(new SpringState(position, velocity), 100));
    }
}
