using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Services;

/// <summary>Propone etiquetas a partir de lo que ya hay: nombre del archivo, carpeta y pistas hermanas. Sin red.</summary>
public static class TagSuggester
{
    public const string VariousArtists = "Various Artists";

    /// <summary>Solo devuelve las pistas que cambiarían.</summary>
    public static IReadOnlyList<TagSuggestion> Suggest(IEnumerable<Track> tracks, TagSuggestOptions? options = null)
    {
        options ??= new TagSuggestOptions();
        var items = tracks.Select(t => (Track: t, After: Improve(t, options))).ToList();

        if (options.DetectCompilations)
            items = FillAlbumArtists(items);

        return items.Select(i => new TagSuggestion(i.Track, TrackTags.From(i.Track), i.After))
                    .Where(s => s.HasChanges)
                    .ToList();
    }

    private static TrackTags Improve(Track track, TagSuggestOptions options)
    {
        var tags = TrackTags.From(track);
        var fileName = Path.GetFileNameWithoutExtension(track.FilePath);
        var parsed = FileNameParser.Parse(fileName);

        var title = tags.Title;
        var artist = tags.Artist;
        var album = tags.Album;
        var number = tags.TrackNumber;

        // Un título igual al nombre del archivo significa que el archivo no tenía etiqueta de título.
        if (string.Equals(title, fileName, StringComparison.OrdinalIgnoreCase))
        {
            var splits = parsed.Artist is not null
                && (artist.Length == 0 || string.Equals(artist, parsed.Artist, StringComparison.OrdinalIgnoreCase));

            title = splits ? parsed.Title : parsed.Rest;
            if (splits && artist.Length == 0) artist = parsed.Artist!;
        }

        if (number == 0 && parsed.Number is { } n) number = n;

        if (album.Length == 0 && options.AlbumFromFolder
            && Path.GetFileName(Path.GetDirectoryName(track.FilePath)) is { Length: > 0 } folder)
            album = folder;

        var cleaned = TitleCleaner.Clean(title);
        return tags with
        {
            Title = cleaned.Length > 0 ? cleaned : tags.Title,
            Artist = TitleCleaner.Tidy(artist),
            AlbumArtist = TitleCleaner.Tidy(tags.AlbumArtist),
            Album = TitleCleaner.Tidy(album),
            TrackNumber = number
        };
    }

    // El artista del álbum es una propiedad del álbum: se propaga dentro de cada (carpeta, álbum).
    private static List<(Track Track, TrackTags After)> FillAlbumArtists(List<(Track Track, TrackTags After)> items)
    {
        var fill = new Dictionary<Guid, string>();
        var groups = items
            .Where(i => i.After.Album.Length > 0)
            .GroupBy(i => (Dir: Path.GetDirectoryName(i.Track.FilePath)?.ToLowerInvariant(),
                           Album: i.After.Album.ToLowerInvariant()));

        foreach (var group in groups)
        {
            var list = group.ToList();
            var existing = list.Select(i => i.After.AlbumArtist).Where(a => a.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var artists = list.Select(i => i.After.Artist).Where(a => a.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count();

            string? target =
                existing.Count == 1 ? existing[0]
                : existing.Count == 0 && list.Count >= 3 && artists >= 3 && artists * 2 >= list.Count ? VariousArtists
                : null;
            if (target is null) continue;

            foreach (var item in list.Where(i => i.After.AlbumArtist.Length == 0))
                fill[item.Track.Id] = target;
        }

        return items.Select(i => fill.TryGetValue(i.Track.Id, out var albumArtist)
            ? (i.Track, i.After with { AlbumArtist = albumArtist })
            : i).ToList();
    }
}