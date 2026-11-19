using MusicPlayer.Core.Services;

namespace MusicPlayer.Core.Tests;

// docs/specs/write-sessions.md
public class WriteSessionsTests
{
    private static readonly Guid A = Guid.NewGuid(), B = Guid.NewGuid();

    private readonly WriteSessions _writes = new();
    private readonly List<string> _log = new();

    public WriteSessionsTests()
    {
        _writes.Releasing += ids => _log.Add("soltar " + string.Join(",", ids.Select(Name).Order()));
        _writes.AllFinished += () => _log.Add("fin");
    }

    private static string Name(Guid id) => id == A ? "A" : id == B ? "B" : "?";

    [Fact]
    public void New_IsClosed() => Assert.False(_writes.IsOpen);

    [Fact]
    public void Begin_AsksToReleaseThoseTracks_AndFinishesOnDispose()
    {
        using (_writes.Begin(new[] { A, B, A }))
        {
            Assert.True(_writes.IsOpen);
            Assert.Equal(new[] { "soltar A,B" }, _log);
        }

        Assert.False(_writes.IsOpen);
        Assert.Equal(new[] { "soltar A,B", "fin" }, _log);
    }

    [Fact]
    public void Overlapping_FinishesOnlyWhenTheLastOneCloses()
    {
        var slow = _writes.Begin(new[] { A }); // un lote largo sobre la pista que sonaba
        var quick = _writes.Begin(new[] { B }); // otra herramienta, que termina antes

        quick.Dispose();

        Assert.True(_writes.IsOpen);
        Assert.Equal(new[] { "soltar A", "soltar B" }, _log); // todavía sin «fin»: A se sigue escribiendo

        slow.Dispose();

        Assert.False(_writes.IsOpen);
        Assert.Equal(new[] { "soltar A", "soltar B", "fin" }, _log);
    }

    [Fact]
    public void Overlapping_ClosedInOrder_AlsoFinishesOnce()
    {
        var first = _writes.Begin(new[] { A });
        var second = _writes.Begin(new[] { B });

        first.Dispose();
        second.Dispose();

        Assert.Equal(1, _log.Count(entry => entry == "fin"));
    }

    [Fact]
    public void DisposingTwice_DoesNotUnbalanceTheCount()
    {
        var first = _writes.Begin(new[] { A });
        var second = _writes.Begin(new[] { B });

        first.Dispose();
        first.Dispose();

        Assert.True(_writes.IsOpen);
        second.Dispose();
        Assert.Equal(1, _log.Count(entry => entry == "fin"));
    }

    [Fact]
    public void AfterFinishing_ANewSessionStartsAgain()
    {
        _writes.Begin(new[] { A }).Dispose();
        _writes.Begin(new[] { B }).Dispose();

        Assert.Equal(new[] { "soltar A", "fin", "soltar B", "fin" }, _log);
    }

    [Fact]
    public void NoTracks_StillCountsAsASession()
    {
        using (_writes.Begin(Array.Empty<Guid>()))
            Assert.True(_writes.IsOpen);

        Assert.Equal(new[] { "soltar ", "fin" }, _log);
    }

    [Fact]
    public void ReleasingThrows_ClosesTheSession_AndStillFinishes()
    {
        var writes = new WriteSessions();
        var finished = 0;
        writes.Releasing += _ => throw new InvalidOperationException("no se pudo soltar");
        writes.AllFinished += () => finished++;

        Assert.Throws<InvalidOperationException>(() => writes.Begin(new[] { A }));

        Assert.False(writes.IsOpen);
        Assert.Equal(1, finished); // quien escucha pudo soltar el archivo antes de fallar: debe poder volver
    }

    [Fact]
    public void ReleasingThrows_WithAnotherSessionOpen_DoesNotFinishYet()
    {
        var writes = new WriteSessions();
        var finished = 0;
        var fail = false;
        writes.Releasing += _ => { if (fail) throw new InvalidOperationException(); };
        writes.AllFinished += () => finished++;
        var open = writes.Begin(new[] { A });
        fail = true;

        Assert.Throws<InvalidOperationException>(() => writes.Begin(new[] { B }));

        Assert.True(writes.IsOpen);
        Assert.Equal(0, finished);
        open.Dispose();
        Assert.Equal(1, finished);
    }

    [Fact]
    public void WithoutListeners_Works()
    {
        var writes = new WriteSessions();

        writes.Begin(new[] { A }).Dispose();

        Assert.False(writes.IsOpen);
    }
}
