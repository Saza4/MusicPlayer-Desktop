namespace MusicPlayer.Core.Services;

// Reglas puras de los atajos de teclado: cuánto saltar y cuánto cambiar el volumen.
public static class PlaybackShortcuts
{
    public static readonly TimeSpan SeekStep = TimeSpan.FromSeconds(5);

    /// <summary>Paso del volumen, en porcentaje.</summary>
    public const int VolumeStepPercent = 5;

    /// <summary>
    /// Destino de un salto de ±<see cref="SeekStep"/>, o <c>null</c> si no procede
    /// (sin dirección, sin duración, o un avance que alcanzaría el final de la pista).
    /// </summary>
    public static TimeSpan? SeekTarget(TimeSpan current, TimeSpan duration, int direction)
    {
        if (direction == 0 || duration <= TimeSpan.Zero)
            return null;

        if (direction > 0)
        {
            var forward = current + SeekStep;
            return forward >= duration ? null : forward;
        }

        var back = current - SeekStep;
        return back < TimeSpan.Zero ? TimeSpan.Zero : back;
    }

    /// <summary>
    /// Volumen (0.0–1.0) tras subir (<paramref name="direction"/> &gt; 0) o bajar (&lt; 0) un paso.
    /// Trabaja en porcentajes enteros para no acumular error de coma flotante y, si el valor
    /// no es múltiplo del paso, avanza hasta el múltiplo más cercano en esa dirección.
    /// </summary>
    public static double StepVolume(double current, int direction)
    {
        var percent = Math.Clamp((int)Math.Round(current * 100), 0, 100);

        if (direction > 0)
            percent = (percent / VolumeStepPercent + 1) * VolumeStepPercent;
        else if (direction < 0)
            percent = percent % VolumeStepPercent == 0
                ? percent - VolumeStepPercent
                : percent / VolumeStepPercent * VolumeStepPercent;

        return Math.Clamp(percent, 0, 100) / 100.0;
    }
}
