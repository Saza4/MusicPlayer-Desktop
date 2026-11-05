using System;
using Avalonia;
using Avalonia.Media;

namespace MusicPlayer.Desktop.Views;

/// <summary>
/// Todas las medidas y tiempos de la isla, en un solo lugar para ajustarlas con capturas.
/// Las medidas están en DIPs (píxeles lógicos); IslandPlacement las pasa a píxeles físicos.
/// </summary>
public static class IslandMetrics
{
    // Posición
    public const double TopMargin = 8;          // distancia al borde superior del área de trabajo

    // Pastilla colapsada
    public const double CollapsedWidth = 180;
    public const double CollapsedHeight = 36;
    public const double CollapsedCornerRadius = 18;
    public const double CollapsedCoverSize = 24;
    public const double CollapsedCoverCornerRadius = 6;

    // Sombra: la ventana deja este margen a los lados y debajo de la pastilla para que se vea.
    public const double ShadowMargin = 16;
    // Negro translúcido, fijo como el fondo de la isla (la sombra no sigue el tema).
    public static readonly BoxShadows Shadow = BoxShadows.Parse("0 4 14 0 #73000000");

    // Tiempos
    public static readonly TimeSpan VisibilityDelay = TimeSpan.FromMilliseconds(150); // contra el parpadeo
    public static readonly TimeSpan FadeDuration = TimeSpan.FromMilliseconds(180);

    /// <summary>Tamaño de la ventana (DIPs) para una pastilla de este tamaño: pastilla + margen de sombra.</summary>
    public static Size WindowSizeFor(double pillWidth, double pillHeight) =>
        new(pillWidth + 2 * ShadowMargin, pillHeight + ShadowMargin);
}
