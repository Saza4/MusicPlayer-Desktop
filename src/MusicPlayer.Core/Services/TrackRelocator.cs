using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Services;

/// <summary>Empareja pistas ausentes con pistas recién encontradas que parecen ser las mismas, movidas de sitio.</summary>
public static class TrackRelocator
{
    public static readonly TimeSpan DurationTolerance = TimeSpan.FromSeconds(1);

    /// <summary>Solo empareja cuando la coincidencia es única de ambos lados; lo ambiguo se deja como está.</summary>
    public static IReadOnlyList<(Track Old, Track New)> Match(
        IReadOnlyCollection<Track> missing, IReadOnlyCollection<Track> candidates)
    {
        var result = new List<(Track Old, Track New)>();
        var candidatesByKey = candidates.ToLookup(Key);

        foreach (var group in missing.GroupBy(Key))
        {
            var olds = group.ToList();
            var news = candidatesByKey[group.Key].ToList();

            if (olds.Count == 1 && news.Count == 1
                && (olds[0].Duration - news[0].Duration).Duration() <= DurationTolerance)
                result.Add((olds[0], news[0]));
        }

        return result;
    }

    private static string Key(Track t) => $"{Norm(t.Title)}\u001F{Norm(t.Artist)}\u001F{Norm(t.Album)}";

    private static string Norm(string text) => text.Trim().ToLowerInvariant();
}