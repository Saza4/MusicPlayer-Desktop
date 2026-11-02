using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Interfaces;

public interface IAudioConverter
{
    bool IsAvailable { get; }

    /// <summary>Convierte <paramref name="input"/> en <paramref name="output"/> sin tocar el original. Lanza si falla.</summary>
    Task ConvertAsync(string input, string output, ConversionOptions options, CancellationToken ct = default);
}