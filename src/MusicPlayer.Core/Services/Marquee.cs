namespace MusicPlayer.Core.Services;

/// <summary>
/// Texto que se desplaza cuando no cabe (docs/specs/island-title-and-close.md): espera al principio, recorre a
/// velocidad constante hasta que la siguiente copia del texto (separada por un hueco) llega al principio, y vuelve
/// a empezar. Son funciones puras: el control solo dibuja en el desplazamiento que se le dice.
/// </summary>
public static class Marquee
{
    /// <summary>El texto de la isla colapsada: «título — artista», o solo el título si no hay artista.</summary>
    public static string Label(string? title, string? artist)
    {
        var t = title?.Trim() ?? string.Empty;
        var a = artist?.Trim() ?? string.Empty;
        if (a.Length == 0) return t;
        return t.Length == 0 ? a : $"{t} — {a}";
    }

    /// <summary>Si el texto entra entero en el ancho disponible (entonces no se desplaza ni se recorta).</summary>
    public static bool Fits(double textWidth, double viewportWidth) =>
        !(textWidth > 0) || textWidth <= viewportWidth;

    /// <summary>
    /// Cuánto se ha corrido el texto hacia la izquierda, en las mismas unidades que los anchos. Vale 0 si cabe,
    /// durante la pausa inicial o con medidas que no sirven (ancho 0, velocidad 0, valores no numéricos); llega
    /// como mucho a <c>textWidth + gap</c>, que es donde la segunda copia ocupa el sitio de la primera.
    /// </summary>
    /// <param name="elapsed">Tiempo desde que el texto empezó a mostrarse.</param>
    /// <param name="speed">Unidades por segundo.</param>
    /// <param name="startPause">Espera con el texto quieto al principio de cada vuelta.</param>
    /// <param name="gap">Hueco entre el final del texto y su siguiente copia.</param>
    public static double Offset(
        TimeSpan elapsed, double textWidth, double viewportWidth, double speed, TimeSpan startPause, double gap)
    {
        if (!(viewportWidth > 0) || !(speed > 0) || !double.IsFinite(textWidth) || Fits(textWidth, viewportWidth))
            return 0;

        var travel = textWidth + (gap > 0 ? gap : 0);
        var pause = startPause > TimeSpan.Zero ? startPause.TotalSeconds : 0;
        var cycle = pause + travel / speed;
        if (!double.IsFinite(cycle) || cycle <= 0) return 0;

        var seconds = elapsed > TimeSpan.Zero ? elapsed.TotalSeconds % cycle : 0;
        var moving = seconds - pause;
        return moving <= 0 ? 0 : Math.Min(moving * speed, travel);
    }
}
