using System.Text;
using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Services;

public sealed class LyricsEditor(ILyricsWriter writer, ILyricParser parser)
{
    /// <summary>Límite por capa: el principal y cada traducción o pronunciación por separado.</summary>
    public const long MaxBytes = 1024 * 1024;

    // Estricto: un archivo que no sea UTF-8 válido se rechaza en lugar de incrustar caracteres rotos para siempre.
    private static readonly UTF8Encoding StrictUtf8 =
        new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>Lee el archivo de letra (.lrc o texto) y lo incrusta en el archivo de audio de la pista.</summary>
    public Task EmbedAsync(Track track, string lyricsPath, CancellationToken ct = default) =>
        EmbedAsync(track, lyricsPath, Array.Empty<LyricsLayerSource>(), ct);

    /// <summary>
    /// Incrusta la letra principal y sus capas en una sola escritura (docs/specs/lyrics-layers.md, criterios 1 y 5).
    /// Si el principal no vale, lanza y no escribe nada. Una capa que no vale se salta y queda en el resultado.
    /// </summary>
    public async Task<LyricsEmbedReport> EmbedAsync(
        Track track, string lyricsPath, IReadOnlyList<LyricsLayerSource> layers, CancellationToken ct = default)
    {
        var main = await ReadAsync(lyricsPath, ct) switch
        {
            (string text, null) => text,
            (_, ReadError.Missing) => throw new FileNotFoundException("No se encontró el archivo de letra."),
            (_, ReadError.TooBig) => throw new ArgumentException("El archivo de letra pesa más de 1 MB."),
            (_, ReadError.NotUtf8) => throw new ArgumentException(
                "El archivo de letra no está en UTF-8. Guárdalo como UTF-8 y vuelve a intentarlo."),
            _ => throw new ArgumentException("El archivo de letra está vacío.")
        };

        var extras = new List<LyricsLayer>();
        var embedded = new List<LyricsLayerSource>();
        var skipped = new List<LyricsLayerSkip>();
        var supported = layers.Count == 0 || writer.SupportsLayers(track.FilePath);
        foreach (var source in layers)
        {
            if (!supported)
            {
                skipped.Add(new LyricsLayerSkip(source, "este formato no la guarda"));
                continue;
            }

            var (text, error) = await ReadAsync(source.Path, ct);
            if (text is not null && !parser.Parse(text).IsAvailable)
                error = ReadError.NoLyrics;

            if (error is { } e)
            {
                skipped.Add(new LyricsLayerSkip(source, Describe(e)));
                continue;
            }
            extras.Add(new LyricsLayer(source.Kind, source.Language, text!));
            embedded.Add(source);
        }

        await Task.Run(() => writer.Write(track.FilePath, new LyricsLayers(main, extras)), ct);
        return new LyricsEmbedReport(embedded, skipped);
    }

    private enum ReadError { Missing, TooBig, NotUtf8, Empty, NoLyrics }

    private static string Describe(ReadError error) => error switch
    {
        ReadError.Missing => "no se encontró",
        ReadError.TooBig => "pesa más de 1 MB",
        ReadError.NotUtf8 => "no está en UTF-8",
        ReadError.Empty => "está vacía",
        _ => "no tiene letra que leer"
    };

    private static async Task<(string? Text, ReadError? Error)> ReadAsync(string path, CancellationToken ct)
    {
        var info = new FileInfo(path);
        if (!info.Exists) return (null, ReadError.Missing);
        if (info.Length > MaxBytes) return (null, ReadError.TooBig);

        string text;
        try
        {
            text = await File.ReadAllTextAsync(path, StrictUtf8, ct); // reconoce y descarta el BOM
        }
        catch (DecoderFallbackException)
        {
            return (null, ReadError.NotUtf8);
        }

        return string.IsNullOrWhiteSpace(text) ? (null, ReadError.Empty) : (text, null);
    }
}
