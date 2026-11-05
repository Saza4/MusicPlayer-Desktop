namespace MusicPlayer.Core.Services;

// Regla pura de cuándo se ve la isla flotante de «ahora suena».
public static class IslandVisibility
{
    /// <summary>
    /// La isla se ve si hay pista cargada, el ajuste «Mostrar isla» está activo, no se está en
    /// modo inmersivo, la ventana principal no está activa (minimizada o detrás de otra ventana)
    /// y no hay un selector ni un diálogo de la app abierto. Con la ventana principal activa la
    /// isla solo duplicaría sus controles, y un selector desactiva la ventana sin que el usuario
    /// haya salido de la app.
    /// </summary>
    public static bool ShouldShow(bool hasTrack, bool showIsland, bool isImmersive, bool isMainWindowActive, bool isModalOpen) =>
        hasTrack && showIsland && !isImmersive && !isMainWindowActive && !isModalOpen;
}
