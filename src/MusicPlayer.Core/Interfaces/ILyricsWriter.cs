using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Interfaces;

public interface ILyricsWriter
{
    /// <summary>
    /// Incrusta en una sola escritura atómica la letra principal (USLT en ID3, LYRICS en FLAC, ©lyr en M4A) y las capas
    /// de <see cref="LyricsLayers.Extras"/> en sus campos propios. Las capas que no vienen no se tocan. Lanza si no se puede.
    /// </summary>
    void Write(string filePath, LyricsLayers layers);
}
