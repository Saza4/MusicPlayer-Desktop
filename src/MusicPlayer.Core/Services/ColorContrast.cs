using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Services;

/// <summary>Relación de contraste de WCAG 2.x entre dos colores (de 1 a 21).</summary>
public static class ColorContrast
{
    public static double Ratio(ThemeColor a, ThemeColor b)
    {
        var la = Luminance(a);
        var lb = Luminance(b);
        var (light, dark) = la >= lb ? (la, lb) : (lb, la);
        return (light + 0.05) / (dark + 0.05);
    }

    private static double Luminance(ThemeColor c) =>
        0.2126 * Linear(c.R) + 0.7152 * Linear(c.G) + 0.0722 * Linear(c.B);

    private static double Linear(byte channel)
    {
        var v = channel / 255.0;
        return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
    }
}
