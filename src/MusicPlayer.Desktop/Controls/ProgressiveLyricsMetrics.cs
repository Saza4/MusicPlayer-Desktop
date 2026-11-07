using System;
using MusicPlayer.Core.Services;

namespace MusicPlayer.Desktop.Controls;

/// <summary>
/// Medidas y tiempos de la letra progresiva, en un solo lugar (DIPs). La lógica (resorte, cascada, atenuación)
/// vive en Core: aquí solo se eligen los valores con los que la usa la vista.
/// </summary>
public static class ProgressiveLyricsMetrics
{
    // Tamaños: los de LyricLineViewModel (18 grande y 14 pequeño, pensados para el panel lateral) escalados para
    // el modo inmersivo, así se respetan las opciones de traducción, pronunciación y «texto grande».
    public const double FontScale = 30.0 / 18;   // 18 → 30, 14 → 23,3
    public const double RowSpacing = 4;          // entre pronunciación, original y traducción de una línea
    public const double LineSpacing = 28;        // entre líneas
    public const double SoftEdge = 24;           // borde suave del relleno

    /// <summary>Paso de referencia para medir la distancia a la línea activa «en líneas» (renglón de 30 + hueco).</summary>
    public const double ReferenceStep = 30 * 1.25 + LineSpacing;

    /// <summary>Altura de la línea activa en el panel (fracción): la de Core.</summary>
    public const double Anchor = LyricsScroll.DefaultAnchor;

    /// <summary>Reloj: 30 pedidos, ~24 reales en Windows (M0). Solo corre mientras hay algo que mover.</summary>
    public const double FramesPerSecond = 30;

    /// <summary>Tolerancias para no tocar la transformación ni la opacidad si el cambio no se ve.</summary>
    public const double PositionEpsilon = 0.05;
    public const double ScaleEpsilon = 0.0005;
    public const double OpacityEpsilon = 0.004;

    /// <summary>El resorte del desplazamiento (el spring() de SwiftUI, como el original).</summary>
    public static readonly Spring Spring = Spring.Default;

    /// <summary>Nunca se programa un despertar más corto que esto.</summary>
    public static readonly TimeSpan MinWake = TimeSpan.FromMilliseconds(1);
}
