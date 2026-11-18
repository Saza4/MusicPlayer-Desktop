namespace MusicPlayer.Core.Services;

/// <summary>Qué partes de la ventana principal van en su forma compacta para un ancho dado.</summary>
/// <param name="TopBarCompact">Los botones de acción de la barra superior quedan solo con icono.</param>
/// <param name="VolumeInFlyout">El deslizador de volumen sale de la barra y pasa a un desplegable.</param>
/// <param name="SidebarCollapsible">La barra lateral se pliega y aparece su botón.</param>
public sealed record WindowLayout(bool TopBarCompact, bool VolumeInFlyout, bool SidebarCollapsible);

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

    public const double SidebarWidth = 232;
    public const double LyricsPanelWidth = 300;

    /// <summary>Ancho mínimo que conserva la lista de canciones junto al panel de letra.</summary>
    public const double MinListWidth = 240;

    // Márgenes fijos de la zona central: 16 a cada lado de la lista (el panel de letra va pegado al borde).
    private const double CentralMargins = 32;

    // Lo que ocupan la barra superior, la de reproducción y un resto mínimo de lista bajo un panel de edición.
    private const double ReservedHeight = 224;
    private const double MinEditPanelHeight = 120;

    public static WindowLayout For(double width) => new(
        TopBarCompact: width < CompactBelow,
        VolumeInFlyout: width < NarrowBelow,
        SidebarCollapsible: width < NarrowBelow);

    // Barra de reproducción (docs/specs/redesign-mockups.md, cuarta pasada): los controles van en una columna central
    // de ancho fijo entre dos columnas iguales, así quedan centrados en la ventana a cualquier ancho.
    private const double PlayerBarPadding = 40;       // 20 a cada lado
    private const double PlayerSideWithVolume = 260;  // letra, isla, pantalla completa, silencio y deslizador
    private const double PlayerSideNarrow = 164;      // los mismos, con el volumen en un desplegable

    /// <summary>Lo mínimo para la fila de controles (aleatorio, anterior, reproducir, siguiente, repetir).</summary>
    public const double MinPlayerCenterWidth = 230;

    /// <summary>A partir de aquí la barra de progreso no crece más (mockup 1a).</summary>
    public const double MaxPlayerCenterWidth = 560;

    /// <summary>
    /// Ancho de la columna central de la barra de reproducción: lo que dejan los dos lados (cada uno del ancho del
    /// grupo derecho, para que el izquierdo sea igual y el centro quede centrado), entre el mínimo y el máximo.
    /// </summary>
    public static double PlayerCenterWidth(double width, bool volumeInFlyout)
    {
        var side = volumeInFlyout ? PlayerSideNarrow : PlayerSideWithVolume;
        return Math.Clamp(width - PlayerBarPadding - 2 * side, MinPlayerCenterWidth, MaxPlayerCenterWidth);
    }

    /// <summary>
    /// True si, con el panel de letra abierto, a la lista le quedarían menos de <see cref="MinListWidth"/>: entonces
    /// el panel ocupa el sitio de la lista.
    /// </summary>
    public static bool LyricsReplacesList(double width, bool sidebarVisible) =>
        width - (sidebarVisible ? SidebarWidth : 0) - LyricsPanelWidth - CentralMargins < MinListWidth;

    /// <summary>Alto máximo de un panel de edición antes de desplazarse, para que sus botones se alcancen siempre.</summary>
    public static double EditPanelMaxHeight(double height) => Math.Max(MinEditPanelHeight, height - ReservedHeight);
}
