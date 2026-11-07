namespace MusicPlayer.Core.Services;

/// <summary>
/// Desplazamiento de la letra progresiva (docs/specs/lyrics-progressive.md, criterio 3). Trabaja con las alturas
/// medidas de cada línea, no con una altura fija, y no acumula nada: el objetivo depende solo de la línea activa.
/// </summary>
public static class LyricsScroll
{
    /// <summary>La línea activa se coloca con su borde superior a esta fracción de la altura visible.</summary>
    public const double DefaultAnchor = 0.35;

    /// <summary>Retardo de cada línea respecto a la anterior en el efecto cascada.</summary>
    public static readonly TimeSpan CascadeStep = TimeSpan.FromMilliseconds(100);

    /// <summary>Retardo máximo de la cascada, para que las líneas lejanas no se queden atrás.</summary>
    public static readonly TimeSpan MaxCascade = TimeSpan.FromMilliseconds(400);

    /// <summary>Posición vertical del borde superior de cada línea, con <paramref name="spacing"/> entre líneas.</summary>
    public static IReadOnlyList<double> LineTops(IReadOnlyList<double> heights, double spacing)
    {
        var tops = new double[heights.Count];
        double y = 0;
        for (int i = 0; i < heights.Count; i++)
        {
            tops[i] = y;
            y += Math.Max(0, heights[i]) + spacing;
        }
        return tops;
    }

    /// <summary>
    /// Desplazamiento vertical del contenido para que la línea activa quede en el ancla. Antes de la primera
    /// línea (índice -1) se usa la primera; un índice mayor que el número de líneas se ajusta a la última.
    /// </summary>
    public static double TargetOffset(IReadOnlyList<double> heights, double spacing, int activeIndex,
        double viewportHeight, double anchor = DefaultAnchor)
    {
        var anchorY = Math.Max(0, viewportHeight) * anchor;
        if (heights.Count == 0) return anchorY;

        var index = Math.Clamp(activeIndex, 0, heights.Count - 1);
        return anchorY - LineTops(heights, spacing)[index];
    }

    /// <summary>
    /// Retardo con que empieza a moverse cada línea al cambiar la línea activa. Al avanzar una línea, las de
    /// debajo de la nueva activa se retrasan más cuanto más lejos están; al retroceder una, las de encima. Un salto
    /// de más de una línea (seek, cambio de pista) mueve todo a la vez, para que llegue cuanto antes.
    /// </summary>
    public static TimeSpan CascadeDelay(int lineIndex, int activeIndex, int previousActiveIndex,
        TimeSpan? step = null, TimeSpan? max = null)
    {
        var change = activeIndex - previousActiveIndex;
        if (change is not (1 or -1)) return TimeSpan.Zero;

        var distance = change == 1 ? lineIndex - activeIndex : activeIndex - lineIndex;
        if (distance <= 0) return TimeSpan.Zero;

        var delay = (step ?? CascadeStep) * distance;
        var limit = max ?? MaxCascade;
        return delay < limit ? delay : limit;
    }
}
