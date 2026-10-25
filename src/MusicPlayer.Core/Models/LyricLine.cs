namespace MusicPlayer.Core.Models;

/// <summary>Línea triple: original, fonética/romanización y traducción.</summary>
public sealed record LyricLine(
    TimeSpan Timestamp,
    string Text,
    string? Romanization = null,
    string? Translation = null);