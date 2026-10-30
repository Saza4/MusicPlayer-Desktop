namespace MusicPlayer.Core.Models;

/// <summary>Las etiquetas editables de una pista. Un texto vacío significa "sin etiqueta".</summary>
public sealed record TrackTags(
    string Title, string Artist, string AlbumArtist, string Album,
    uint Year, uint TrackNumber, uint DiscNumber)
{
    /// <summary>Lo que se muestra en el editor: los marcadores "desconocido" aparecen como campos vacíos.</summary>
    public static TrackTags From(Track t) => new(
        t.Title,
        t.Artist == Track.UnknownArtist ? "" : t.Artist,
        t.AlbumArtist,
        t.Album == Track.UnknownAlbum ? "" : t.Album,
        t.Year, t.TrackNumber, t.DiscNumber);

    public TrackTags Normalized() => this with
    {
        Title = (Title ?? "").Trim(),
        Artist = (Artist ?? "").Trim(),
        AlbumArtist = (AlbumArtist ?? "").Trim(),
        Album = (Album ?? "").Trim()
    };

    /// <summary>Aplica las etiquetas a la pista con las mismas reglas de respaldo que el lector de metadatos.</summary>
    public Track ApplyTo(Track track) => track with
    {
        Title = Title,
        Artist = Artist.Length > 0 ? Artist
               : AlbumArtist.Length > 0 ? AlbumArtist
               : Track.UnknownArtist,
        AlbumArtist = AlbumArtist,
        Album = Album.Length > 0 ? Album : Track.UnknownAlbum,
        Year = Year,
        TrackNumber = TrackNumber,
        DiscNumber = DiscNumber
    };
}