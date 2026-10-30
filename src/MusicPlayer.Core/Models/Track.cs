namespace MusicPlayer.Core.Models;

public sealed record Track
{
    public const string UnknownArtist = "Artista desconocido";
    public const string UnknownAlbum = "Álbum desconocido";

    public Guid Id { get; init; } = Guid.NewGuid();
    public required string FilePath { get; init; }
    public required string Title { get; init; }
    public string Artist { get; init; } = UnknownArtist;
    public string Album { get; init; } = UnknownAlbum;
    public uint Year { get; init; }
    public string? Genre { get; init; }
    public uint TrackNumber { get; init; }
    public uint DiscNumber { get; init; }

    /// <summary>Etiqueta "artista del álbum" (ALBUMARTIST). Vacía si el archivo no la tiene.</summary>
    public string AlbumArtist { get; init; } = "";

    /// <summary>Bajo quién se agrupa el álbum: su artista del álbum, o el de la pista si no hay.</summary>
    public string AlbumOwner => string.IsNullOrWhiteSpace(AlbumArtist) ? Artist : AlbumArtist;
    public TimeSpan Duration { get; init; }
    public DateTimeOffset AddedAt { get; init; } = DateTimeOffset.UtcNow;
}