using System.Text;
using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Services;

public sealed class LyricsEditor(ILyricsWriter writer)
{
    public const long MaxBytes = 1024 * 1024;

    // Estricto: un archivo que no sea UTF-8 válido se rechaza en lugar de incrustar caracteres rotos para siempre.
    private static readonly UTF8Encoding StrictUtf8 =
        new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>Lee el archivo de letra (.lrc o texto) y lo incrusta en el archivo de audio de la pista.</summary>
    public async Task EmbedAsync(Track track, string lyricsPath, CancellationToken ct = default)
    {
        var text = await ReadAsync(lyricsPath, ct);
        await Task.Run(() => writer.Write(track.FilePath, text), ct);
    }

    private static async Task<string> ReadAsync(string path, CancellationToken ct)
    {
        var info = new FileInfo(path);
        if (!info.Exists)
            throw new FileNotFoundException("No se encontró el archivo de letra.");
        if (info.Length > MaxBytes)
            throw new ArgumentException("El archivo de letra pesa más de 1 MB.");

        string text;
        try
        {
            text = await File.ReadAllTextAsync(path, StrictUtf8, ct); // reconoce y descarta el BOM
        }
        catch (DecoderFallbackException)
        {
            throw new ArgumentException("El archivo de letra no está en UTF-8. Guárdalo como UTF-8 y vuelve a intentarlo.");
        }

        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("El archivo de letra está vacío.");

        return text;
    }
}