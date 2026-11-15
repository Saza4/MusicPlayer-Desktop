namespace MusicPlayer.Core.Services;

/// <summary>Qué partes de la ventana principal van en su forma compacta para un ancho dado.</summary>
/// <param name="TopBarCompact">Los botones de acción de la barra superior quedan solo con icono.</param>
/// <param name="PlayerBarCompact">La barra de reproducción da a su grupo derecho el ancho que necesita.</param>
/// <param name="VolumeInFlyout">El deslizador de volumen sale de la barra y pasa a un desplegable.</param>
/// <param name="SidebarCollapsible">La barra lateral COLECCIONES se pliega y aparece su botón.</param>
public sealed record WindowLayout(bool TopBarCompact, bool PlayerBarCompact, bool VolumeInFlyout, bool SidebarCollapsible);

/// <summary>
/// Umbrales del diseño adaptable de la ventana principal (docs/specs/responsive-layout.md). Son constantes, no se
/// mide el texto. La vista solo aplica lo que sale de aquí.
/// </summary>
public static class LayoutBreakpoints
{
    /// <summary>Por debajo, la barra superior y la de reproducción van compactas.</summary>
    public const double CompactBelow = 1100;

    /// <summary>Por debajo, el volumen pasa a un desplegable y la barra lateral se pliega.</summary>
    public const double NarrowBelow = 800;

    public const double SidebarWidth = 220;
    public const double LyricsPanelWidth = 380;

    /// <summary>Ancho mínimo que conserva la lista de canciones junto al panel de letra.</summary>
    public const double MinListWidth = 240;

    // Márgenes fijos de la zona central: 16 de la lista y 8 a la derecha del panel de letra.
    private const double CentralMargins = 24;

    // Lo que ocupan la barra superior, la de reproducción y un resto mínimo de lista bajo un panel de edición.
    private const double ReservedHeight = 224;
    private const double MinEditPanelHeight = 120;

    public static WindowLayout For(double width) => new(
        TopBarCompact: width < CompactBelow,
        PlayerBarCompact: width < CompactBelow,
        VolumeInFlyout: width < NarrowBelow,
        SidebarCollapsible: width < NarrowBelow);

    /// <summary>
    /// True si, con el panel de letra abierto, a la lista le quedarían menos de <see cref="MinListWidth"/>: entonces
    /// el panel ocupa el sitio de la lista.
    /// </summary>
    public static bool LyricsReplacesList(double width, bool sidebarVisible) =>
        width - (sidebarVisible ? SidebarWidth : 0) - LyricsPanelWidth - CentralMargins < MinListWidth;

    /// <summary>Alto máximo de un panel de edición antes de desplazarse, para que sus botones se alcancen siempre.</summary>
    public static double EditPanelMaxHeight(double height) => Math.Max(MinEditPanelHeight, height - ReservedHeight);
}
