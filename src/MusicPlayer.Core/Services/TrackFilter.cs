using System.Globalization;
using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Services;

public static class TrackFilter
{
    private const CompareOptions Options = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    public static IReadOnlyList<Track> Filter(IEnumerable<Track> tracks, string? query)
    {
        var terms = (query ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (terms.Length == 0)
            return tracks.ToList();

        var compare = CultureInfo.InvariantCulture.CompareInfo;
        return tracks.Where(t => terms.All(term => Matches(compare, t, term))).ToList();
    }

    private static bool Matches(CompareInfo compare, Track track, string term) =>
        Contains(compare, track.Title, term)
        || Contains(compare, track.Artist, term)
        || Contains(compare, track.Album, term);

    private static bool Contains(CompareInfo compare, string? text, string term) =>
        !string.IsNullOrEmpty(text) && compare.IndexOf(text, term, Options) >= 0;
}