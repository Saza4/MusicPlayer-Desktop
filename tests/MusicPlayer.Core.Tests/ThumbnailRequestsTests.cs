using MusicPlayer.Core.Services;

namespace MusicPlayer.Core.Tests;

/// <summary>docs/specs/ui-responsiveness.md, criterios 6 y 7.</summary>
public class ThumbnailRequestsTests
{
    private readonly ThumbnailRequests _requests = new(comparer: StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void Begin_WhileLoading_DoesNotStartASecondLoad()
    {
        Assert.NotNull(_requests.Begin("a.mp3"));

        Assert.Null(_requests.Begin("a.mp3"));
        Assert.Null(_requests.Begin("A.MP3"));
    }

    [Fact]
    public void Complete_WithCover_AllowsALaterLoad_ForWhenTheBitmapLeavesTheCache()
    {
        var ticket = _requests.Begin("a.mp3")!.Value;

        Assert.True(_requests.Complete("a.mp3", ticket, found: true));

        Assert.NotNull(_requests.Begin("a.mp3"));
    }

    [Fact]
    public void Complete_WithoutCover_IsRemembered_AndTheDiskIsNotReadAgain()
    {
        var ticket = _requests.Begin("a.mp3")!.Value;

        Assert.True(_requests.Complete("a.mp3", ticket, found: false));

        Assert.Null(_requests.Begin("a.mp3"));
        Assert.NotNull(_requests.Begin("b.mp3")); // las demás no se ven afectadas
    }

    [Fact]
    public void Invalidate_ForgetsAMissingCover()
    {
        _requests.Complete("a.mp3", _requests.Begin("a.mp3")!.Value, found: false);

        _requests.Invalidate("a.mp3");

        Assert.NotNull(_requests.Begin("a.mp3"));
    }

    [Fact]
    public void Invalidate_DuringALoad_DiscardsThatResult_AndLetsANewLoadStart()
    {
        var stale = _requests.Begin("a.mp3")!.Value;

        _requests.Invalidate("a.mp3");
        var fresh = _requests.Begin("a.mp3");

        Assert.NotNull(fresh); // la fila nueva lanza su propia carga
        Assert.False(_requests.Complete("a.mp3", stale, found: true)); // la vieja no se guarda ni avisa
        Assert.True(_requests.Complete("a.mp3", fresh.Value, found: true));
    }

    [Fact]
    public void Invalidate_DuringALoad_AStaleMissingResultIsNotRemembered()
    {
        var stale = _requests.Begin("a.mp3")!.Value;
        _requests.Invalidate("a.mp3");

        Assert.False(_requests.Complete("a.mp3", stale, found: false));

        Assert.NotNull(_requests.Begin("a.mp3"));
    }

    [Fact]
    public void Invalidate_Twice_OnlyTheLoadStartedAfterTheLastOneCounts()
    {
        var first = _requests.Begin("a.mp3")!.Value;
        _requests.Invalidate("a.mp3");
        var second = _requests.Begin("a.mp3")!.Value;
        _requests.Invalidate("a.mp3");
        var third = _requests.Begin("a.mp3")!.Value;

        Assert.False(_requests.Complete("a.mp3", first, found: true));
        Assert.False(_requests.Complete("a.mp3", second, found: true));
        Assert.True(_requests.Complete("a.mp3", third, found: true));
    }

    [Fact]
    public void MissingCovers_AreBounded_TheOldestIsForgotten()
    {
        var requests = new ThumbnailRequests(missingCapacity: 2);
        foreach (var key in new[] { "a", "b", "c" })
            requests.Complete(key, requests.Begin(key)!.Value, found: false);

        Assert.NotNull(requests.Begin("a")); // se olvidó: se volverá a leer
        Assert.Null(requests.Begin("c"));
    }
}
