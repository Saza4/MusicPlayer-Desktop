using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;

namespace MusicPlayer.Infrastructure;

public sealed class TagLibTrackTagWriter : ITrackTagWriter
{
    public void Write(string filePath, TrackTags tags) =>
        SafeFileEdit.Run(filePath, temp =>
        {
            Apply(temp, tags);
            Verify(temp, tags);
        });

    private static void Apply(string path, TrackTags tags)
    {
        using var file = TagLib.File.Create(path);
        var tag = file.Tag;

        tag.Title = OrNull(tags.Title);
        tag.Performers = tags.Artist.Length > 0 ? new[] { tags.Artist } : Array.Empty<string>();
        tag.AlbumArtists = tags.AlbumArtist.Length > 0 ? new[] { tags.AlbumArtist } : Array.Empty<string>();
        tag.Album = OrNull(tags.Album);
        tag.Year = tags.Year;
        tag.Track = tags.TrackNumber;
        tag.Disc = tags.DiscNumber;

        file.Save();
    }

    // Reabre la copia y comprueba que lo escrito es lo pedido.
    private static void Verify(string path, TrackTags tags)
    {
        using var file = TagLib.File.Create(path);
        if ((file.Tag.Title ?? "") != tags.Title || file.Tag.Track != tags.TrackNumber)
            throw new IOException("La verificación tras escribir falló. El archivo original no se modificó.");
    }

    private static string? OrNull(string text) => text.Length == 0 ? null : text;
}