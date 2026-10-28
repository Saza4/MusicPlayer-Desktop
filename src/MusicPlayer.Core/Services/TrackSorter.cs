using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Services;

public static class TrackSorter
{
    /// <summary>Artista, álbum, disco, pista y título. Sin etiqueta de disco (0) cuenta como disco 1.</summary>
    public static IReadOnlyList<Track> Sort(IEnumerable<Track> tracks) =>
        tracks.OrderBy(t => t.Artist, StringComparer.CurrentCultureIgnoreCase)
              .ThenBy(t => t.Album, StringComparer.CurrentCultureIgnoreCase)
              .ThenBy(t => Math.Max(t.DiscNumber, 1u))
              .ThenBy(t => t.TrackNumber)
              .ThenBy(t => t.Title, StringComparer.CurrentCultureIgnoreCase)
              .ToList();
}