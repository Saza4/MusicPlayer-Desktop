using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Interfaces;

public interface ITrackMetadataReader
{
    /// <summary>Devuelve null si el archivo está corrupto o no es un formato soportado.</summary>
    Track? Read(string filePath);
}