using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Services;

/// <summary>
/// Tras guardar etiquetas, qué artista y qué álbum siguen abiertos (docs/specs/keep-browse-scope.md). Si lo abierto
/// sigue existiendo, se queda. Si desapareció (se renombró el álbum o el artista), se sigue a la primera pista que
/// se veía y sigue en la biblioteca. Si no queda ninguna, se cierra.
/// </summary>
public static class BrowseScope
{
    /// <param name="artist">Artista abierto, o null.</param>
    /// <param name="albumKey">Clave del álbum abierto (<see cref="LibraryGrouper.AlbumKey"/>), o null.</param>
    /// <param name="shownIds">Las pistas que se veían antes de guardar, en el orden de la lista.</param>
    /// <param name="tracks">La biblioteca después de guardar.</param>
    public static (string? Artist, string? AlbumKey) Resolve(
        string? artist, string? albumKey, IReadOnlyList<Guid> shownIds, IReadOnlyList<Track> tracks)
    {
        if (artist is null && albumKey is null) return (null, null);

        var byId = new Dictionary<Guid, Track>();
        foreach (var t in tracks) byId.TryAdd(t.Id, t);
        Track? followed = null;
        foreach (var id in shownIds)
            if (byId.TryGetValue(id, out followed)) break;

        if (albumKey is not null && !tracks.Any(t => LibraryGrouper.AlbumKey(t) == albumKey))
        {
            // El álbum ya no existe: se sigue la pista. Su álbum puede ser de otro artista, y el artista abierto
            // tiene que ser el de ese álbum.
            if (followed is null)
                return (artist is not null && HasArtist(tracks, artist) ? artist : null, null);
            return (artist is null ? null : Owner(followed), LibraryGrouper.AlbumKey(followed));
        }

        if (artist is not null && !HasArtist(tracks, artist))
            return (followed is null ? null : Owner(followed), null);

        return (artist, albumKey);
    }

    private static string Owner(Track track) => track.AlbumOwner.Trim();

    private static bool HasArtist(IReadOnlyList<Track> tracks, string artist) =>
        tracks.Any(t => string.Equals(Owner(t), artist.Trim(), StringComparison.OrdinalIgnoreCase));
}
