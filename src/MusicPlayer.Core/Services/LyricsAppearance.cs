namespace MusicPlayer.Core.Services;

/// <summary>
/// Aspecto de cada línea de la letra progresiva (docs/specs/lyrics-progressive.md, criterios 2 y 7). Una sola
/// curva de atenuación sirve para tres cosas: la opacidad y la escala según la distancia a la línea activa, y el
/// fundido de los bordes del panel. El fundido no usa una máscara de opacidad, que abre una capa por fotograma.
/// </summary>
public static class LyricsAppearance
{
    /// <summary>A esta distancia (en líneas) de la activa se llega al mínimo de opacidad y de escala.</summary>
    public const double LinesToFloor = 4;

    /// <summary>Opacidad de las líneas lejanas (sobre un texto tenue que ya es translúcido).</summary>
    public const double MinOpacity = 0.5;

    /// <summary>Escala de las líneas lejanas: una reducción ligera.</summary>
    public const double MinScale = 0.95;

    /// <summary>Alto de la franja de fundido arriba y abajo, como fracción del alto del panel.</summary>
    public const double EdgeFadeFraction = 0.15;

    /// <summary>
    /// La curva común: 1 a distancia 0 y <paramref name="floor"/> a partir de <paramref name="range"/>, con una
    /// transición suave (smoothstep) entre medias. La distancia se toma en valor absoluto.
    /// </summary>
    public static double Falloff(double distance, double range, double floor)
    {
        if (range <= 0) return distance == 0 ? 1 : floor;
        var t = Math.Clamp(Math.Abs(distance) / range, 0, 1);
        var eased = t * t * (3 - 2 * t);
        return floor + (1 - floor) * (1 - eased);
    }

    /// <summary>Opacidad según la distancia a la línea activa, en líneas (admite fracciones: sigue al resorte).</summary>
    public static double LineOpacity(double distanceInLines) => Falloff(distanceInLines, LinesToFloor, MinOpacity);

    /// <summary>Escala según la distancia a la línea activa, en líneas.</summary>
    public static double LineScale(double distanceInLines) => Falloff(distanceInLines, LinesToFloor, MinScale);

    /// <summary>
    /// Fundido de los bordes: 1 si la línea entera está fuera de las franjas de arriba y abajo, y baja hasta 0 cuando
    /// su borde toca el del panel, con la misma curva. Así una línea no se ve cortada por el borde. Fuera, 0.
    /// </summary>
    public static double EdgeOpacity(double top, double height, double viewportHeight, double fadeHeight)
    {
        if (viewportHeight <= 0) return 0;
        var bottom = top + Math.Max(0, height);
        var inset = Math.Min(top, viewportHeight - bottom); // del borde de la línea al borde del panel más cercano
        if (inset <= 0) return 0;
        if (fadeHeight <= 0 || inset >= fadeHeight) return 1;
        return Falloff(fadeHeight - inset, fadeHeight, 0);
    }
}
