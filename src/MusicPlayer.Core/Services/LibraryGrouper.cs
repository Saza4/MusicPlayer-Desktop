using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Services;

public static class LibraryGrouper
{
    /// <summary>Identifica un álbum: artista y título, sin distinguir mayúsculas ni espacios de los extremos.</summary>
    public static string AlbumKey(Track track) =>
        $"{track.Artist.Trim().ToLowerInvariant()}\u001F{track.Album.Trim().ToLowerInvariant()}";

    /// <summary>Álbumes ordenados por artista, año (el desconocido al final) y título.</summary>
    public static IReadOnlyList<AlbumGroup> GroupAlbums(IEnumerable<Track> tracks) =>
        tracks.GroupBy(AlbumKey)
              .Select(g =>
              {
                  var sorted = TrackSorter.Sort(g); // disco, pista y título
                  var year = sorted.Select(t => t.Year).Where(y => y > 0).DefaultIfEmpty(0u).Min();
                  return new AlbumGroup(g.Key, sorted[0].Artist.Trim(), sorted[0].Album.Trim(), year, sorted);
              })
              .OrderBy(a => a.Artist, StringComparer.CurrentCultureIgnoreCase)
              .ThenBy(a => a.Year == 0 ? uint.MaxValue : a.Year)
              .ThenBy(a => a.Album, StringComparer.CurrentCultureIgnoreCase)
              .ToList();

    /// <summary>Artistas por nombre, cada uno con sus álbumes.</summary>
    public static IReadOnlyList<ArtistGroup> GroupArtists(IEnumerable<Track> tracks) =>
        tracks.GroupBy(t => t.Artist.Trim().ToLowerInvariant())
              .Select(g => new ArtistGroup(g.First().Artist.Trim(), GroupAlbums(g)))
              .OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
              .ToList();
}