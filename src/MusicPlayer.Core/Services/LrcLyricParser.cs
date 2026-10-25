using System.Globalization;
using System.Text.RegularExpressions;
using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Services;

public sealed partial class LrcLyricParser : ILyricParser
{
    // Uno o más tags de tiempo al inicio de la línea: [mm:ss.xx][mm:ss.xx]
    [GeneratedRegex(@"^(?:\[\d{1,3}:\d{2}(?:[.:]\d{1,3})?\])+")]
    private static partial Regex LeadingTimeTags();

    [GeneratedRegex(@"\[(\d{1,3}):(\d{2})(?:[.:](\d{1,3}))?\]")]
    private static partial Regex SingleTimeTag();

    // Metadatos: [ar:..], [ti:..], [al:..], [by:..], [offset:..], etc.
    [GeneratedRegex(@"^\[[a-zA-Z]+:.*\]$")]
    private static partial Regex MetadataTag();

    public LyricBlock Parse(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return LyricBlock.Empty;

        var synced = new List<LyricLine>();
        var plain = new List<LyricLine>();

        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || MetadataTag().IsMatch(line))
                continue;

            var prefix = LeadingTimeTags().Match(line);
            if (prefix.Success)
            {
                var text = line[prefix.Length..].Trim();
                foreach (Match tag in SingleTimeTag().Matches(prefix.Value))
                    synced.Add(new LyricLine(ToTimeSpan(tag), text));
            }
            else
            {
                plain.Add(new LyricLine(TimeSpan.Zero, line));
            }
        }

        if (synced.Count > 0)
            return new LyricBlock(LyricsKind.Synced,
                synced.OrderBy(l => l.Timestamp).ToList());

        return plain.Count > 0
            ? new LyricBlock(LyricsKind.Plain, plain)
            : LyricBlock.Empty;
    }

    private static TimeSpan ToTimeSpan(Match tag)
    {
        int minutes = int.Parse(tag.Groups[1].Value, CultureInfo.InvariantCulture);
        int seconds = int.Parse(tag.Groups[2].Value, CultureInfo.InvariantCulture);

        int millis = 0;
        if (tag.Groups[3].Success)
        {
            var fraction = tag.Groups[3].Value;
            // "5" = 500 ms, "50" = 500 ms (centésimas), "500" = 500 ms
            millis = int.Parse(fraction.PadRight(3, '0'), CultureInfo.InvariantCulture);
        }

        return new TimeSpan(0, 0, minutes, seconds, millis);
    }
}