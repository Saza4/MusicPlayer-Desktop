using System;
using Avalonia;
using Avalonia.Animation.Easings;
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

    // Pastilla colapsada (sin sombra)
    public const double CollapsedWidth = 180;
    public const double CollapsedHeight = 36;
    public const double CollapsedCornerRadius = 18;
    public const double CollapsedCoverSize = 24;
    public const double CollapsedCoverCornerRadius = 6;
    // En reposo recibe clics la pastilla más este margen. La región de Windows corta sin suavizar: con 0 el
    // borde de la pastilla sale a escalones; con 2 el corte cae en zona transparente (medido en M5).
    public const double CollapsedHitMargin = 2;

    // Pastilla expandida
    public const double ExpandedWidth = 340;
    public const double ExpandedHeight = 144;
    public const double ExpandedCornerRadius = 24;

    // Contenido expandido. Filas: cabecera + hueco + progreso + hueco + controles = alto - 2·relleno
    // (48 + 8 + 16 + 4 + 36 = 112 = 144 - 2·16).
    public const double ExpandedPadding = 16;
    public const double ExpandedCoverSize = 48;
    public const double ExpandedCoverCornerRadius = 10;
    public const double HeaderSpacing = 12;          // entre carátula, textos y onda
    public const double HeaderToProgress = 8;
    public const double ProgressHeight = 16;         // zona de clic de la barra (la barra visible es más fina)
    public const double ProgressBarHeight = 4;
    public const double ProgressTimeWidth = 38;      // ancho fijo de los tiempos: la barra no cambia de ancho
    public const double ProgressToControls = 4;
    public const double ControlSize = 32;            // aleatorio, anterior, siguiente
    public const double PlaySize = 36;               // reproducir/pausa, mayor
    public const double ControlSpacing = 14;
    public const double TitleFontSize = 14;
    public const double ArtistFontSize = 12;
    public const double TimeFontSize = 11;

    // Sombra: solo con la pastilla expandida. La ventana deja este margen a los lados y debajo.
    public const double ExpandedShadowMargin = 16;
    // Negro translúcido, fijo como el fondo de la isla (la sombra no sigue el tema). Desenfoque 12 + 4 hacia
    // abajo = 16: cabe en el margen. Aparece y desaparece con un fundido de opacidad, al ritmo de la pastilla.
    public static readonly BoxShadows ExpandedShadow = BoxShadows.Parse("0 4 12 0 #73000000");

    // Tiempos de la animación
    public static readonly TimeSpan ExpandDuration = TimeSpan.FromMilliseconds(400);
    public static readonly TimeSpan CollapseDuration = TimeSpan.FromMilliseconds(300);
    public static readonly TimeSpan CollapseDelay = TimeSpan.FromMilliseconds(200);   // tras salir el cursor
    public static readonly TimeSpan ContentFadeDuration = TimeSpan.FromMilliseconds(150);
    // El contenido expandido espera a que la pastilla tenga sitio antes de aparecer.
    public static readonly TimeSpan ExpandedContentDelay = TimeSpan.FromMilliseconds(150);

    // Curva de la pastilla: arranque rápido y llegada suave, sin rebote (x1, y1, x2, y2).
    public static readonly Easing ExpandEasing = new SplineEasing(0.32, 0.72, 0, 1);
    public static readonly Easing CollapseEasing = new SplineEasing(0.32, 0.72, 0, 1);

    // Onda: cada barra va y vuelve entre WaveMinScale y su altura, con su propio periodo (ida y vuelta) para que
    // no suban todas a la vez. Solo se anima con la isla visible y la pista sonando, a WaveFrameInterval.
    public const double WaveMinScale = 0.35;
    public static readonly TimeSpan WaveFrameInterval = TimeSpan.FromMilliseconds(1000.0 / 15);
    public static readonly TimeSpan[] WaveBarPeriods =
    [
        TimeSpan.FromMilliseconds(840), TimeSpan.FromMilliseconds(1120), TimeSpan.FromMilliseconds(760),
        TimeSpan.FromMilliseconds(1000), TimeSpan.FromMilliseconds(920),
    ];

    // Mostrar y ocultar la isla entera
    public static readonly TimeSpan VisibilityDelay = TimeSpan.FromMilliseconds(150); // contra el parpadeo
    public static readonly TimeSpan FadeDuration = TimeSpan.FromMilliseconds(180);

    /// <summary>
    /// Ventana (DIPs), fija: pastilla expandida + margen de sombra a los lados y debajo. No cambia al
    /// expandir; lo que cambia es la zona que recibe clics (en reposo, la pastilla colapsada + CollapsedHitMargin).
    /// </summary>
    public static Size WindowSize =>
        new(ExpandedWidth + 2 * ExpandedShadowMargin, ExpandedHeight + ExpandedShadowMargin);
}
