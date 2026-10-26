using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;

namespace MusicPlayer.Infrastructure;

/// <summary>Letra incrustada en el archivo (USLT en ID3v2, LYRICS en Vorbis/FLAC).</summary>
public sealed class EmbeddedLyricsProvider(ILyricParser parser) : ILyricsProvider
{
    public Task<LyricBlock> GetLyricsAsync(Track track, CancellationToken ct = default) =>
        Task.Run(() =>
        {
            try
            {
                using var file = TagLib.File.Create(track.FilePath);
                var lyrics = file.Tag.Lyrics;
                return string.IsNullOrWhiteSpace(lyrics) ? LyricBlock.Empty : parser.Parse(lyrics);
            }
            catch (Exception ex) when (ex is TagLib.CorruptFileException
                                          or TagLib.UnsupportedFormatException
                                          or IOException
                                          or UnauthorizedAccessException)
            {
                return LyricBlock.Empty;
            }
        }, ct);
}