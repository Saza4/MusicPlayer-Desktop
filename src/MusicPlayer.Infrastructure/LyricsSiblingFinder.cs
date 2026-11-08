using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;

namespace MusicPlayer.Infrastructure;

/// <summary><see cref="LyricsSiblingFiles.Find"/> con el idioma del sistema, para quien no está en Infrastructure.</summary>
/// <param name="systemLanguage">Código corto del sistema (TwoLetterISOLanguageName): vale como sufijo por compatibilidad.</param>
public sealed class LyricsSiblingFinder(string systemLanguage) : ILyricsSiblingFinder
{
    public IReadOnlyList<LyricsLayerSource> Find(string lyricsOrAudioPath) =>
        LyricsSiblingFiles.Find(lyricsOrAudioPath, systemLanguage);
}
