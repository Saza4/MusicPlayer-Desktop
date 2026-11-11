using MusicPlayer.Core.Services;

namespace MusicPlayer.Core.Tests;

/// <summary>docs/specs/ui-responsiveness.md, criterios 4 y 5.</summary>
public class SeekScrubTests
{
    private readonly SeekScrub _scrub = new();

    private static TimeSpan S(int seconds) => TimeSpan.FromSeconds(seconds);

    [Fact]
    public void WithoutDragging_AChangeSeeksAtOnce_AndTheTimerMayWrite()
    {
        Assert.True(_scrub.TimerMayWrite);
        Assert.Equal(S(30), _scrub.Change(S(30))); // atajo de teclado, clic en la isla
    }

    [Fact]
    public void WhileDragging_NothingSeeks_AndTheTimerKeepsOff()
    {
        _scrub.Begin();

        Assert.Null(_scrub.Change(S(10)));
        Assert.Null(_scrub.Change(S(20)));
        Assert.False(_scrub.TimerMayWrite);
    }

    [Fact]
    public void Releasing_SeeksOnce_ToTheLastValue()
    {
        _scrub.Begin();
        _scrub.Change(S(10));
        _scrub.Change(S(20));
        _scrub.Change(S(45));

        Assert.Equal(S(45), _scrub.End());
        Assert.True(_scrub.TimerMayWrite);
        Assert.Null(_scrub.End()); // soltar dos veces (soltar y perder la captura) no repite el salto
    }

    [Fact]
    public void PressAndReleaseWithoutMoving_DoesNotSeek()
    {
        _scrub.Begin();

        Assert.Null(_scrub.End());
    }

    [Fact]
    public void AClickOnTheBar_SeeksOnRelease()
    {
        _scrub.Begin();
        _scrub.Change(S(90)); // la barra mueve el pulgar al punto pulsado

        Assert.Equal(S(90), _scrub.End());
    }

    [Fact]
    public void TrackChangeDuringTheDrag_AbandonsIt_WithoutSeekingInTheNewTrack()
    {
        _scrub.Begin();
        _scrub.Change(S(200));

        _scrub.Abandon();

        Assert.Null(_scrub.Change(S(210))); // el puntero sigue pulsado y moviéndose
        Assert.False(_scrub.TimerMayWrite);
        Assert.Null(_scrub.End());
        Assert.True(_scrub.TimerMayWrite);
    }

    [Fact]
    public void AfterAnAbandonedDrag_TheNextOneWorksAsUsual()
    {
        _scrub.Begin();
        _scrub.Abandon();
        _scrub.End();

        _scrub.Begin();
        _scrub.Change(S(15));

        Assert.Equal(S(15), _scrub.End());
    }

    [Fact]
    public void Abandon_WithoutADrag_ChangesNothing()
    {
        _scrub.Abandon();

        Assert.Equal(S(5), _scrub.Change(S(5)));
        Assert.True(_scrub.TimerMayWrite);
    }
}
