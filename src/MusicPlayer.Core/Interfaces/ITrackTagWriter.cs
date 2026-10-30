using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Interfaces;

public interface ITrackTagWriter
{
    /// <summary>Escribe las etiquetas en el archivo. Lanza una excepción si no se puede (en uso, solo lectura, formato).</summary>
    void Write(string filePath, TrackTags tags);
}