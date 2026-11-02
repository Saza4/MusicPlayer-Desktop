namespace MusicPlayer.Core.Interfaces;

public interface ICoverArtCache
{
    /// <summary>Olvida lo guardado de este archivo (tras cambiarle la carátula).</summary>
    void Invalidate(string filePath);
}