using MusicPlayer.Core.Services;

namespace MusicPlayer.Core.Tests;

public class ModalTrackerTests
{
    [Fact]
    public void NewTracker_IsClosed() => Assert.False(new ModalTracker().IsOpen);

    [Fact]
    public void Begin_OpensUntilDisposed()
    {
        var modals = new ModalTracker();

        using (modals.Begin())
            Assert.True(modals.IsOpen);

        Assert.False(modals.IsOpen);
    }

    [Fact]
    public void Nested_StaysOpenUntilTheLastOneCloses()
    {
        var modals = new ModalTracker();
        var outer = modals.Begin();
        var inner = modals.Begin();

        inner.Dispose();
        Assert.True(modals.IsOpen);

        outer.Dispose();
        Assert.False(modals.IsOpen);
    }

    [Fact]
    public void Changed_FiresOnlyOnFirstOpenAndLastClose()
    {
        var modals = new ModalTracker();
        var events = 0;
        modals.Changed += (_, _) => events++;

        var outer = modals.Begin();
        var inner = modals.Begin();
        inner.Dispose();
        outer.Dispose();

        Assert.Equal(2, events);
    }

    [Fact]
    public void DisposingTwice_DoesNotUnbalanceTheCount()
    {
        var modals = new ModalTracker();
        var outer = modals.Begin();
        var inner = modals.Begin();

        inner.Dispose();
        inner.Dispose();

        Assert.True(modals.IsOpen);
        outer.Dispose();
        Assert.False(modals.IsOpen);
    }

    [Fact]
    public async Task ExceptionInsideTheScope_StillCloses()
    {
        var modals = new ModalTracker();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            using (modals.Begin())
            {
                await Task.Yield();
                throw new InvalidOperationException("el selector falló");
            }
        });

        Assert.False(modals.IsOpen);
    }
}
