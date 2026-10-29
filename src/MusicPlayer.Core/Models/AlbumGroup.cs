namespace MusicPlayer.Core.Models;

/// <summary>Un álbum: sus pistas ya van en orden de reproducción (disco y número).</summary>
public sealed record AlbumGroup(string Key, string Artist, string Album, uint Year, IReadOnlyList<Track> Tracks);

/// <summary>Un artista con sus álbumes ordenados por año.</summary>
public sealed record ArtistGroup(string Name, IReadOnlyList<AlbumGroup> Albums)
{
    public int TrackCount => Albums.Sum(a => a.Tracks.Count);
}