using MusicPlayer.Core.Services;

namespace MusicPlayer.Core.Tests;

// docs/specs/audit-3.md, criterio 1.
public class PlaybackEpochTests
{
    [Fact]
    public void AStampWithNoOrderSince_IsCurrent()
    {
        var epoch = new PlaybackEpoch();
        var stamp = epoch.Current;

        Assert.True(epoch.IsCurrent(stamp));
    }

    [Fact]
    public void AStampTakenBeforeAnotherLoad_IsStale()
    {
        var epoch = new PlaybackEpoch();
        epoch.Advance();            // se cargó la pista A
        var endOfA = epoch.Current; // A termina: el aviso sale
        epoch.Advance();            // el usuario elige B antes de que se atienda

        Assert.False(epoch.IsCurrent(endOfA));
    }

    [Fact]
    public void AStampTakenBeforeAnUnload_IsStale_AndLaterOnesAreCurrent()
    {
        var epoch = new PlaybackEpoch();
        epoch.Advance();            // carga
        var before = epoch.Current;
        epoch.Advance();            // descarga para escribir en el archivo
        epoch.Advance();            // recarga al terminar
        var after = epoch.Current;

        Assert.False(epoch.IsCurrent(before));
        Assert.True(epoch.IsCurrent(after));
    }

    [Fact]
    public void AutomaticAdvanceDoesNotCount_SoBothNoticesOfAChainStayCurrent()
    {
        var epoch = new PlaybackEpoch();
        epoch.Advance();            // carga de A; B y C se encadenan solas, sin orden nueva
        var advancedToB = epoch.Current;
        var advancedToC = epoch.Current;

        Assert.True(epoch.IsCurrent(advancedToB));
        Assert.True(epoch.IsCurrent(advancedToC));
    }
}
