namespace MusicPlayer.Core.Models;

/// <summary>Línea triple: original, fonética/romanización y traducción.</summary>
/// <param name="Words">
/// Tiempos por palabra, para una sincronía palabra a palabra futura (spec de letras progresivas, RNF-5).
/// Hoy ningún proveedor los rellena: siempre es null. Va al final para no romper las llamadas posicionales.
/// </param>
public sealed record LyricLine(
    TimeSpan Timestamp,
    string Text,
    string? Romanization = null,
    string? Translation = null,
    IReadOnlyList<LyricWord>? Words = null);

/// <summary>Una palabra de la línea con su intervalo de canto. Sin uso todavía (ver <see cref="LyricLine.Words"/>).</summary>
public sealed record LyricWord(string Text, TimeSpan Start, TimeSpan End);
