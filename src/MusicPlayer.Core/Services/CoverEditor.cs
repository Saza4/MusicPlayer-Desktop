using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Services;

public sealed class CoverEditor(ICoverArtWriter writer, LibraryService library)
{
    /// <summary>Pone la carátula de una pista, o la quita si <paramref name="image"/> es null.</summary>
    public async Task SetAsync(Guid trackId, byte[]? image, CancellationToken ct = default)
    {
        var track = library.Current.Tracks.FirstOrDefault(t => t.Id == trackId)
            ?? throw new KeyNotFoundException("La canción ya no está en la biblioteca.");

        if (image is not null)
        {
            if (image.Length > CoverImage.MaxBytes)
                throw new ArgumentException("La imagen pesa más de 10 MB.");
            if (CoverImage.DetectMime(image) is null)
                throw new ArgumentException("La imagen debe ser JPG o PNG.");
        }

        await Task.Run(() => writer.Write(track.FilePath, image), ct);
    }
}