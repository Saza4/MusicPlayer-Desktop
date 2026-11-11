namespace MusicPlayer.Core.Models;

/// <summary>
/// Palabra de una línea de un .lrc enhanced (docs/specs/lrc-enhanced.md): el trozo de texto que empieza en la marca
/// <c>&lt;mm:ss.xx&gt;</c>. Puede ser una sílaba si la marca va pegada a otra palabra.
/// </summary>
/// <param name="Text">El texto de la palabra, sin espacios en los extremos.</param>
/// <param name="Start">Cuándo empieza (tiempo absoluto, como la marca de la línea). Sin corregir: puede ser anterior a la línea.</param>
/// <param name="End">Cuándo termina: el inicio de la palabra siguiente o, en la última, la marca final de la línea. Null si no se sabe.</param>
/// <param name="Offset">Índice donde empieza la palabra dentro de <see cref="LyricLine.Text"/>.</param>
public sealed record LyricWord(string Text, TimeSpan Start, TimeSpan? End, int Offset);

/// <summary>Línea triple: original, fonética/romanización y traducción.</summary>
/// <param name="Words">Palabras con su tiempo (.lrc enhanced). Null en una línea normal.</param>
/// <param name="End">Marca final sin texto de un .lrc enhanced (fin de la línea). Null si no la trae.</param>
public sealed record LyricLine(
    TimeSpan Timestamp,
    string Text,
    string? Romanization = null,
    string? Translation = null,
    IReadOnlyList<LyricWord>? Words = null,
    TimeSpan? End = null);
