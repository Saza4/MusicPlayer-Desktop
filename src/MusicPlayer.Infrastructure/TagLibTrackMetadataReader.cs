using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;

namespace MusicPlayer.Infrastructure;

public sealed class TagLibTrackMetadataReader : ITrackMetadataReader
{
    public Track? Read(string filePath)
    {
        try
        {
            // Nombre completo para no chocar con System.IO.File.
            using var file = TagLib.File.Create(filePath);
            var tag = file.Tag;

            return new Track
            {
                FilePath = filePath,
                Title = string.IsNullOrWhiteSpace(tag.Title)
                    ? Path.GetFileNameWithoutExtension(filePath)
                    : tag.Title.Trim(),
                Artist = FirstNonEmpty(tag.FirstPerformer, tag.FirstAlbumArtist) ?? "Artista desconocido",
                Album = string.IsNullOrWhiteSpace(tag.Album) ? "Álbum desconocido" : tag.Album.Trim(),
                Year = tag.Year,
                Genre = tag.FirstGenre,
                TrackNumber = tag.Track,
                DiscNumber = tag.Disc,
                Duration = file.Properties.Duration
            };
        }
        catch (Exception ex) when (ex is TagLib.CorruptFileException
                                      or TagLib.UnsupportedFormatException
                                      or IOException
                                      or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();
}