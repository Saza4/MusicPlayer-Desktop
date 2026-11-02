namespace MusicPlayer.Core.Interfaces;

public interface ILyricsWriter
{
    /// <summary>Incrusta la letra en el archivo (USLT en ID3, LYRICS en FLAC, ©lyr en M4A). Lanza si no se puede.</summary>
    void Write(string filePath, string lyrics);
}