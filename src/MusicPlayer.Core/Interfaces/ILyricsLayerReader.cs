using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Interfaces;

public interface ILyricsLayerReader
{
    /// <summary>
    /// Traducciones y pronunciación incrustadas en el archivo, con el código normalizado. Vacío si el formato no las
    /// guarda, si no hay ninguna o si el archivo no se puede leer: nunca lanza por el archivo.
    /// </summary>
    IReadOnlyList<LyricsLayer> Read(string filePath);
}
