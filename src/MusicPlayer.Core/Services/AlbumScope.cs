using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Services;

public static class AlbumScope
{
    /// <summary>Las pistas del mismo álbum que <paramref name="track"/>. Sin álbum conocido, solo ella misma.</summary>
    public static IReadOnlyList<Track> Of(Track track, IEnumerable<Track> library)
    {
        if (track.Album == Track.UnknownAlbum)
            return new[] { track };

        var owner = Owner(track);
        return library
            .Where(t => string.Equals(t.Album, track.Album, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(Owner(t), owner, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    // El "artista del álbum" agrupa los recopilatorios; sin él, manda el artista de la pista.
    private static string Owner(Track t) => t.AlbumArtist.Length > 0 ? t.AlbumArtist : t.Artist;
}