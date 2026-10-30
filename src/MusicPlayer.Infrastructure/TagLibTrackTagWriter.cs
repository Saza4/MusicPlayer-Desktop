using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;

namespace MusicPlayer.Infrastructure;

public sealed class TagLibTrackTagWriter : ITrackTagWriter
{
    /// <summary>
    /// Edita una copia temporal, la verifica y la intercambia por el original. Si algo falla,
    /// el original queda intacto. Cuesta copiar el archivo entero, a cambio de no arriesgarlo.
    /// </summary>
    public void Write(string filePath, TrackTags tags)
    {
        if (new FileInfo(filePath).IsReadOnly)
            throw new UnauthorizedAccessException("El archivo es de solo lectura.");

        // La copia conserva la extensión: TagLib decide el formato por ella.
        var directory = Path.GetDirectoryName(Path.GetFullPath(filePath))!;
        var temp = Path.Combine(directory, $"~tags-{Guid.NewGuid():N}{Path.GetExtension(filePath)}");

        try
        {
            File.Copy(filePath, temp);
            Apply(temp, tags);
            Verify(temp, tags);
            File.Replace(temp, filePath, destinationBackupFileName: null);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

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