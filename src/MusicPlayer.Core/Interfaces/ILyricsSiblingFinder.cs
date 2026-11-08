using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Interfaces;

public interface ILyricsSiblingFinder
{
    /// <summary>
    /// Capas hermanas de un .lrc o de un audio («cancion.es.lrc», «cancion.rom.lrc»), las mismas que ve la lectura
    /// (docs/specs/lyrics-layers.md, criterio 1). Vacío si no hay o la carpeta no se puede leer.
    /// </summary>
    IReadOnlyList<LyricsLayerSource> Find(string lyricsOrAudioPath);
}
