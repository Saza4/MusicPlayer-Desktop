using System.Globalization;
using System.Text;
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

    // Marca por palabra de un .lrc enhanced: <mm:ss.xx>. Solo dígitos ASCII; los segundos (< 60) se comprueban aparte.
    [GeneratedRegex(@"<([0-9]{1,3}):([0-9]{2})(?:[.:]([0-9]{1,3}))?>")]
    private static partial Regex WordTag();

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
                var marks = SingleTimeTag().Matches(prefix.Value);
                // Los tiempos de las palabras están escritos respecto a la primera marca del archivo: cada copia los desplaza.
                var baseTime = ToTimeSpan(marks[0]);
                var (text, words, end) = ParseBody(line[prefix.Length..], baseTime, out var hasWords);
                foreach (Match tag in marks)
                {
                    var at = ToTimeSpan(tag);
                    var delta = at - baseTime;
                    synced.Add(hasWords || end is not null
                        ? new LyricLine(at, text, Words: Shift(words, delta), End: end + delta)
                        : new LyricLine(at, text));
                }
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

    /// <summary>
    /// Separa el texto limpio de las marcas &lt;mm:ss.xx&gt; (docs/specs/lrc-enhanced.md). El espaciado entre tramos se
    /// conserva, así que «Hel&lt;t&gt;lo» da «Hello». Una marca no válida (segundos ≥ 60) queda como texto.
    /// </summary>
    private static (string Text, List<LyricWord>? Words, TimeSpan? End) ParseBody(string body, TimeSpan lineTime, out bool hasWords)
    {
        hasWords = false;
        var tags = WordTag().Matches(body).Where(m => int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) < 60).ToList();
        if (tags.Count == 0)
            return (body.Trim(), null, null);

        var text = new StringBuilder();
        var words = new List<LyricWord>();
        TimeSpan? end = null;

        // Texto anterior a la primera marca: primera palabra, a la hora de la línea.
        AddSegment(body[..tags[0].Index], lineTime);
        for (int i = 0; i < tags.Count; i++)
        {
            var from = tags[i].Index + tags[i].Length;
            var to = i + 1 < tags.Count ? tags[i + 1].Index : body.Length;
            var segment = body[from..to];
            if (i == tags.Count - 1 && string.IsNullOrWhiteSpace(segment))
                end = ToTimeSpan(tags[i]); // marca final sin texto: fin de la línea
            else
                AddSegment(segment, ToTimeSpan(tags[i]));
        }

        var full = text.ToString();
        var lead = full.Length - full.TrimStart().Length;
        var cleaned = words.Select(w => w with { Offset = w.Offset - lead }).ToList();
        hasWords = cleaned.Count > 0;
        if (!hasWords)
            return (full.Trim(), null, end);

        // End de cada palabra: el inicio de la siguiente; la última, la marca final (si la hay).
        for (int i = 0; i < cleaned.Count; i++)
            cleaned[i] = cleaned[i] with { End = i + 1 < cleaned.Count ? cleaned[i + 1].Start : end };
        return (full.Trim(), cleaned, end);

        void AddSegment(string segment, TimeSpan start)
        {
            var word = segment.Trim();
            if (word.Length > 0)
                words.Add(new LyricWord(word, start, null, text.Length + (segment.Length - segment.TrimStart().Length)));
            text.Append(segment);
        }
    }

    private static IReadOnlyList<LyricWord>? Shift(List<LyricWord>? words, TimeSpan delta) =>
        words?.Select(w => w with { Start = w.Start + delta, End = w.End + delta }).ToList();

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
