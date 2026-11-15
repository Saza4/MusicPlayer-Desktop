namespace MusicPlayer.Core.Services;

/// <summary>
/// Decide si un clic sobre una fila de canción la reproduce (docs/specs/settings-panel.md, criterio 9). Por defecto
/// un clic selecciona y el doble clic reproduce; con el ajuste «Reproducir con un solo clic», el primer clic
/// reproduce.
/// </summary>
public static class TrackClick
{
    /// <param name="singleClickEnabled">El ajuste «Reproducir con un solo clic».</param>
    /// <param name="clickCount">1 para un clic, 2 para el segundo clic de un doble clic.</param>
    /// <param name="isLeftButton">Solo el botón izquierdo reproduce: el derecho abre el menú.</param>
    /// <param name="hasSelectionModifier">Ctrl o Mayús pulsados: el clic es para seleccionar.</param>
    /// <param name="isPlayingThisTrack">La fila pulsada es la pista que ya suena.</param>
    public static bool ShouldPlay(
        bool singleClickEnabled, int clickCount, bool isLeftButton, bool hasSelectionModifier, bool isPlayingThisTrack)
    {
        if (!isLeftButton) return false;

        if (!singleClickEnabled)
            return clickCount == 2; // como siempre: el doble clic reproduce (y reinicia la que suena)

        // Un clic reproduce. El segundo clic de un doble clic no la reinicia, ni tampoco un clic en la que ya suena.
        return clickCount == 1 && !hasSelectionModifier && !isPlayingThisTrack;
    }
}
