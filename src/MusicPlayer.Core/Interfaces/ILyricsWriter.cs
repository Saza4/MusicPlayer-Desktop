using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Interfaces;

public interface ILyricsWriter
{
    /// <summary>
    /// Incrusta en una sola escritura atómica la letra principal (USLT en ID3, LYRICS en FLAC, ©lyr en M4A) y las capas
    /// de <see cref="LyricsLayers.Extras"/> en sus campos propios. Las capas que no vienen no se tocan. Lanza si no se puede.
    /// </summary>
    void Write(string filePath, LyricsLayers layers);

    /// <summary>
    /// Incrusta solo capas (traducción o pronunciación) en una escritura atómica, <b>sin tocar la letra principal</b>
    /// (docs/specs/lyrics-layers.md, criterio 12b). Las capas que no vienen no se tocan. Lanza si el formato no las
    /// guarda (<see cref="SupportsLayers"/>) o si no se puede escribir.
    /// </summary>
    void WriteLayers(string filePath, IReadOnlyList<LyricsLayer> layers);

    /// <summary>True si el formato del archivo guarda capas (MP3, FLAC y M4A). Los demás solo llevan la principal.</summary>
    bool SupportsLayers(string filePath);
}
