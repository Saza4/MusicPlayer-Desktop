using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Interfaces;

public interface ICoverArtProvider
{
    /// <summary>Bytes de la imagen (jpg/png), o null si la pista no tiene carátula.</summary>
    Task<byte[]?> GetCoverAsync(Track track, CancellationToken ct = default);
}