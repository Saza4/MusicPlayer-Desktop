namespace MusicPlayer.Core.Models;

/// <summary>Capas que acompañan a la letra principal (docs/specs/lyrics-layers.md).</summary>
public enum LyricsLayerKind { Translation, Pronunciation }

/// <summary>Un archivo de capa junto a la letra principal: «cancion.es.lrc» o «cancion.rom.lrc».</summary>
/// <param name="Language">Código normalizado («es», «pt-BR», «zh-Hans»). Solo en traducciones; null en la pronunciación.</param>
public sealed record LyricsLayerSource(LyricsLayerKind Kind, string? Language, string Path)
{
    /// <summary>Para la línea de estado: «traducción (es)» o «pronunciación».</summary>
    public string Label => Kind == LyricsLayerKind.Translation ? $"traducción ({Language})" : "pronunciación";
}

/// <summary>Texto de una capa ya leído y validado.</summary>
public sealed record LyricsLayer(LyricsLayerKind Kind, string? Language, string Text);

/// <summary>
/// Lo que se incrusta en una sola escritura: la letra principal (campo estándar) y las capas con fuente. Las capas que no
/// están aquí no se tocan en el archivo (reincrustar reemplaza solo las que tienen fuente).
/// </summary>
public sealed record LyricsLayers(string Main, IReadOnlyList<LyricsLayer> Extras)
{
    public static LyricsLayers MainOnly(string main) => new(main, Array.Empty<LyricsLayer>());
}

/// <summary>Una capa que no se incrustó y por qué («no está en UTF-8»).</summary>
public sealed record LyricsLayerSkip(LyricsLayerSource Source, string Reason);

/// <summary>Resultado de incrustar: qué capas entraron y cuáles se saltaron.</summary>
public sealed record LyricsEmbedReport(IReadOnlyList<LyricsLayerSource> Embedded, IReadOnlyList<LyricsLayerSkip> Skipped)
{
    /// <summary>
    /// Resumen para la línea de estado. Sin capas, el mismo texto de siempre:
    /// «Letra de "X" incrustada desde x.lrc». Con capas:
    /// «Letra de "X" incrustada desde x.lrc: original, traducción (es) y pronunciación · saltada: traducción (en), no está en UTF-8».
    /// </summary>
    /// <remarks>El orden no depende de cómo llegan: traducciones por código y luego la pronunciación (criterio 7).</remarks>
    public string Describe(string title, string mainFileName)
    {
        var text = $"Letra de \"{title}\" incrustada desde {mainFileName}";
        if (Embedded.Count > 0)
            text += ": " + JoinSpanish(new[] { "original" }.Concat(InOrder(Embedded, e => e).Select(e => e.Label)).ToList());
        if (Skipped.Count > 0)
            text += (Skipped.Count == 1 ? " · saltada: " : " · saltadas: ") +
                    string.Join("; ", InOrder(Skipped, s => s.Source).Select(s => $"{s.Source.Label}, {s.Reason}"));
        return text;
    }

    private static IEnumerable<T> InOrder<T>(IEnumerable<T> items, Func<T, LyricsLayerSource> source) =>
        items.OrderBy(i => source(i).Kind == LyricsLayerKind.Pronunciation)
             .ThenBy(i => source(i).Language, StringComparer.Ordinal);

    private static string JoinSpanish(IReadOnlyList<string> items) =>
        items.Count == 1 ? items[0] : string.Join(", ", items.Take(items.Count - 1)) + " y " + items[^1];
}
