using System.Text;
using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Services;

public sealed class LyricsEditor(ILyricsWriter writer, ILyricParser parser)
{
    /// <summary>Límite por capa: el principal y cada traducción o pronunciación por separado.</summary>
    public const long MaxBytes = 1024 * 1024;

    /// <summary>
    /// True si el texto cabe en <see cref="MaxBytes"/> medido en UTF-8. Lo usan los lectores: una letra incrustada
    /// por otra herramienta puede pasarse del tope (docs/specs/untrusted-input-limits.md, criterio 3).
    /// </summary>
    public static bool FitsLimit(string text) =>
        text.Length <= MaxBytes && (text.Length * 3L <= MaxBytes || Encoding.UTF8.GetByteCount(text) <= MaxBytes);

    // Estricto: un archivo que no sea UTF-8 válido se rechaza en lugar de incrustar caracteres rotos para siempre.
    private static readonly UTF8Encoding StrictUtf8 =
        new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>Lee el archivo de letra (.lrc o texto) y lo incrusta en el archivo de audio de la pista.</summary>
    public Task EmbedAsync(Track track, string lyricsPath, CancellationToken ct = default) =>
        EmbedAsync(track, lyricsPath, Array.Empty<LyricsLayerSource>(), ct);

    /// <summary>
    /// Incrusta la letra principal y sus capas en una sola escritura (docs/specs/lyrics-layers.md, criterios 1 y 5).
    /// Si el principal no vale, lanza y no escribe nada. Una capa que no vale se salta y queda en el resultado.
    /// También se salta una capa de otro tipo que el principal (sin marcas con un principal sincronizado, o al revés):
    /// al leer, LyricsMerger la ignoraría y quedaría un campo que nunca se ve (criterio 5). Por lo mismo, con letra sin
    /// marcas se salta una capa con otro número de líneas: LyricsMerger las empareja por posición.
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

        var mainBlock = parser.Parse(main);
        var mainKind = mainBlock.Kind;
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

            var (text, error) = await ValidateLayerAsync(source, mainKind, mainBlock.Lines.Count, ct);
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

    /// <summary>
    /// Incrusta <b>una sola capa</b> elegida por el usuario (de cualquier carpeta) sin reescribir la letra principal
    /// (docs/specs/lyrics-layers.md, criterio 12b). La valida igual que las capas de <see cref="EmbedAsync(Track, string, IReadOnlyList{LyricsLayerSource}, CancellationToken)"/>
    /// contra la letra principal ya cargada. Si el formato no guarda capas o la capa no vale, no escribe nada y la
    /// devuelve en <see cref="LyricsEmbedReport.Skipped"/> con su motivo.
    /// </summary>
    /// <param name="main">Letra principal de la pista, tal como se ve (su tipo y, si no tiene marcas, su número de líneas).</param>
    public async Task<LyricsEmbedReport> EmbedLayerAsync(
        Track track, LyricsLayerSource source, LyricBlock main, CancellationToken ct = default)
    {
        if (!writer.SupportsLayers(track.FilePath))
            return new LyricsEmbedReport([], [new LyricsLayerSkip(source, "este formato no la guarda")]);

        var (text, error) = await ValidateLayerAsync(source, main.Kind, main.Lines.Count, ct);
        if (error is { } e)
            return new LyricsEmbedReport([], [new LyricsLayerSkip(source, Describe(e))]);

        await Task.Run(() => writer.WriteLayers(track.FilePath, [new LyricsLayer(source.Kind, source.Language, text!)]), ct);
        return new LyricsEmbedReport([source], []);
    }

    // Lee la capa y comprueba que valga junto a la principal (criterio 5). Compartido por las dos formas de incrustar.
    private async Task<(string? Text, ReadError? Error)> ValidateLayerAsync(
        LyricsLayerSource source, LyricsKind mainKind, int mainLineCount, CancellationToken ct)
    {
        var (text, error) = await ReadAsync(source.Path, ct);
        if (text is not null)
        {
            var block = parser.Parse(text);
            if (!block.IsAvailable)
                error = ReadError.NoLyrics;
            else if (block.Kind != mainKind)
                error = mainKind switch
                {
                    LyricsKind.Synced => ReadError.NotSynced,
                    LyricsKind.Plain => ReadError.Synced,
                    _ => ReadError.MainWithoutLyrics
                };
            else if (mainKind == LyricsKind.Plain && block.Lines.Count != mainLineCount)
                error = ReadError.OtherLineCount;
        }
        return (text, error);
    }

    private enum ReadError { Missing, TooBig, NotUtf8, Empty, NoLyrics, NotSynced, Synced, MainWithoutLyrics, OtherLineCount }

    private static string Describe(ReadError error) => error switch
    {
        ReadError.Missing => "no se encontró",
        ReadError.TooBig => "pesa más de 1 MB",
        ReadError.NotUtf8 => "no está en UTF-8",
        ReadError.Empty => "está vacía",
        ReadError.NotSynced => "no tiene marcas de tiempo como el original",
        ReadError.Synced => "tiene marcas de tiempo y el original no",
        ReadError.MainWithoutLyrics => "el original no tiene letra que leer",
        ReadError.OtherLineCount => "no tiene tantas líneas como el original",
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
