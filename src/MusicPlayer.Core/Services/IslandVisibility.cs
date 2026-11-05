namespace MusicPlayer.Core.Services;

// Regla pura de cuándo se ve la isla flotante de «ahora suena».
public static class IslandVisibility
{
    /// <summary>
    /// La isla se ve si hay pista cargada, el ajuste «Mostrar isla» está activo, no se está en
    /// modo inmersivo y la ventana principal no está activa (minimizada o detrás de otra ventana):
    /// con la ventana principal activa, la isla solo duplicaría sus controles.
    /// </summary>
    public static bool ShouldShow(bool hasTrack, bool showIsland, bool isImmersive, bool isMainWindowActive) =>
        hasTrack && showIsland && !isImmersive && !isMainWindowActive;
}
