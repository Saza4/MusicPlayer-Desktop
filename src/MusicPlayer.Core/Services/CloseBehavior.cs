namespace MusicPlayer.Core.Services;

/// <summary>Qué hacer cuando se pide cerrar la ventana principal.</summary>
public enum CloseAction
{
    /// <summary>La app termina.</summary>
    Exit,

    /// <summary>La ventana se oculta a la bandeja y la música sigue.</summary>
    Hide,
}

/// <summary>
/// Regla de «Seguir sonando al cerrar la ventana» (docs/specs/background-playback.md, criterio 7). Solo se oculta
/// si el usuario lo eligió, hay una forma de volver (icono de bandeja) y nadie pidió salir de verdad.
/// </summary>
public static class CloseBehavior
{
    /// <param name="keepRunning">El ajuste «Seguir sonando al cerrar la ventana».</param>
    /// <param name="supported">Hay icono de bandeja para volver a abrir la ventana (hoy, solo en Windows).</param>
    /// <param name="exitRequested">Se pidió salir: «Salir» de la bandeja o de la isla, o el apagado del sistema.</param>
    public static CloseAction Decide(bool keepRunning, bool supported, bool exitRequested) =>
        keepRunning && supported && !exitRequested ? CloseAction.Hide : CloseAction.Exit;
}
