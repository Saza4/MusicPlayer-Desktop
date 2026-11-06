namespace MusicPlayer.Core.Services;

// Textos de tiempo que no dependen de la interfaz.
public static class TimeText
{
    /// <summary>Signo menos tipográfico (U+2212), como en «−1:31».</summary>
    public const string Minus = "−";

    /// <summary>
    /// Tiempo que falta, con signo menos: «−1:31», o «−1:02:03» a partir de una hora.
    /// Trabaja en segundos enteros (truncados, igual que el tiempo transcurrido), así que
    /// transcurrido + restante siempre suman la duración que se muestra. Devuelve una cadena
    /// vacía si la duración no se conoce.
    /// </summary>
    public static string Remaining(TimeSpan position, TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
            return "";

        var elapsed = Math.Max(0, (long)position.TotalSeconds);
        var total = (long)duration.TotalSeconds;
        var left = TimeSpan.FromSeconds(Math.Max(0, total - elapsed));

        return Minus + (left.TotalHours >= 1 ? left.ToString(@"h\:mm\:ss") : left.ToString(@"m\:ss"));
    }
}
