namespace MusicPlayer.Core.Services;

/// <summary>
/// Dónde va la isla en una pantalla. Los tamaños de la interfaz están en DIPs (píxeles lógicos);
/// la posición de una ventana y el área de trabajo de la pantalla, en píxeles físicos.
/// La conversión usa siempre la escala de la pantalla de destino.
/// </summary>
public static class IslandPlacement
{
    public static int ToPhysical(double dips, double scaling) => (int)Math.Round(dips * scaling);

    /// <summary>
    /// Esquina superior izquierda (en píxeles físicos) de una ventana de <paramref name="windowWidthDips"/>
    /// centrada horizontalmente en el área de trabajo y a <paramref name="topMarginDips"/> de su borde superior.
    /// </summary>
    public static (int X, int Y) TopCenter(
        int areaX, int areaY, int areaWidth, double scaling, double windowWidthDips, double topMarginDips)
    {
        var width = ToPhysical(windowWidthDips, scaling);
        return (areaX + (areaWidth - width) / 2, areaY + ToPhysical(topMarginDips, scaling));
    }
}
