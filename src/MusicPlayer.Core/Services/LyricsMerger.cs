using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Services;

public static class LyricsMerger
{
    public static readonly TimeSpan DefaultTolerance = TimeSpan.FromMilliseconds(300);

    public static LyricBlock Merge(
        LyricBlock original,
        LyricBlock? translation,
        LyricBlock? romanization,
        TimeSpan? tolerance = null)
    {
        if (!original.IsAvailable)
            return original;

        var tol = tolerance ?? DefaultTolerance;
        var lines = new List<LyricLine>(original.Lines.Count);

        for (int i = 0; i < original.Lines.Count; i++)
        {
            var line = original.Lines[i];
            lines.Add(line with
            {
                Translation = Find(original, translation, i, tol) ?? line.Translation,
                Romanization = Find(original, romanization, i, tol) ?? line.Romanization
            });
        }

        return original with { Lines = lines };
    }

    private static string? Find(LyricBlock original, LyricBlock? source, int index, TimeSpan tolerance)
    {
        if (source is null || !source.IsAvailable || source.Kind != original.Kind)
            return null;

        if (original.Kind == LyricsKind.Plain)
            return source.Lines.Count == original.Lines.Count
                ? NullIfBlank(source.Lines[index].Text)
                : null;

        var timestamp = original.Lines[index].Timestamp;
        LyricLine? best = null;
        var bestDiff = TimeSpan.MaxValue;

        foreach (var candidate in source.Lines)
        {
            var diff = (candidate.Timestamp - timestamp).Duration();
            if (diff <= tolerance && diff < bestDiff)
            {
                best = candidate;
                bestDiff = diff;
            }
        }

        return NullIfBlank(best?.Text);
    }

    private static string? NullIfBlank(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text;
}