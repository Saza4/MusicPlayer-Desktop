namespace MusicPlayer.Core.Models;

/// <summary>
/// Cómo se ve la letra sincronizada en el modo inmersivo (docs/specs/lyrics-progressive.md, criterio 10).
/// Ajuste temporal: cuando el autor apruebe el estilo progresivo, se retira junto con el clásico del inmersivo.
/// </summary>
public enum LyricsStyle
{
    /// <summary>La lista de siempre (ListBox), la misma del panel lateral.</summary>
    Classic,

    /// <summary>Estilo Apple Music: la línea activa se rellena de izquierda a derecha.</summary>
    Progressive,
}
