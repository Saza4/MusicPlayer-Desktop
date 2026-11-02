namespace MusicPlayer.Core.Interfaces;

public interface ICoverArtWriter
{
    /// <summary>Incrusta la imagen (JPG o PNG) en el archivo. Null quita la carátula. Lanza si no se puede.</summary>
    void Write(string filePath, byte[]? image);
}