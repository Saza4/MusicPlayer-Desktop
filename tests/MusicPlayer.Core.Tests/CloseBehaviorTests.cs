using MusicPlayer.Core.Services;

namespace MusicPlayer.Core.Tests;

// docs/specs/background-playback.md, criterio 7: todas las combinaciones.
public class CloseBehaviorTests
{
    [Theory]
    [InlineData(true, true, false, CloseAction.Hide)]   // el único caso en que se oculta
    [InlineData(true, true, true, CloseAction.Exit)]    // «Salir» o apagado del sistema
    [InlineData(true, false, false, CloseAction.Exit)]  // sin bandeja no hay forma de volver
    [InlineData(true, false, true, CloseAction.Exit)]
    [InlineData(false, true, false, CloseAction.Exit)]  // ajuste apagado: como siempre
    [InlineData(false, true, true, CloseAction.Exit)]
    [InlineData(false, false, false, CloseAction.Exit)]
    [InlineData(false, false, true, CloseAction.Exit)]
    public void Decide_HidesOnlyWhenChosenSupportedAndNoExitRequested(
        bool keepRunning, bool supported, bool exitRequested, CloseAction expected)
    {
        Assert.Equal(expected, CloseBehavior.Decide(keepRunning, supported, exitRequested));
    }
}
