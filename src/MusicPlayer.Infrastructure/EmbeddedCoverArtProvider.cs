using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;

namespace MusicPlayer.Infrastructure;

/// <summary>Carátula incrustada en el archivo (APIC en ID3, PICTURE en FLAC, covr en M4A).</summary>
public sealed class EmbeddedCoverArtProvider : ICoverArtProvider
{
    public Task<byte[]?> GetCoverAsync(Track track, CancellationToken ct = default) =>
        Task.Run<byte[]?>(() =>
        {
            try
            {
                // ReadStyle.None omite las propiedades de audio: solo necesitamos los tags.
                using var file = TagLib.File.Create(track.FilePath, TagLib.ReadStyle.None);
                var pictures = file.Tag.Pictures;
                var picture = pictures.FirstOrDefault(p => p.Type == TagLib.PictureType.FrontCover)
                              ?? pictures.FirstOrDefault();
                return picture?.Data.Data;
            }
            catch (Exception ex) when (ex is TagLib.CorruptFileException
                                          or TagLib.UnsupportedFormatException
                                          or IOException
                                          or UnauthorizedAccessException)
            {
                return null;
            }
        }, ct);
}