using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;

namespace MusicPlayer.Infrastructure;

public sealed class TagLibTrackTagWriter : ITrackTagWriter
{
    public void Write(string filePath, TrackTags tags)
    {
        using var file = TagLib.File.Create(filePath);
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

    private static string? OrNull(string text) => text.Length == 0 ? null : text;
}