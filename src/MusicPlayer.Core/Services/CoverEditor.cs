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
            Validate(image);

        await Task.Run(() => writer.Write(track.FilePath, image), ct);
    }

    /// <summary>Pone la misma imagen en varias pistas. Un fallo no detiene el resto.</summary>
    public async Task<CoverBatchResult> SetManyAsync(
        IReadOnlyList<Track> tracks, byte[] image, CancellationToken ct = default)
    {
        Validate(image); // una sola vez y antes de tocar ningún archivo

        var errors = new List<string>();
        var written = 0;

        foreach (var track in tracks)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await Task.Run(() => writer.Write(track.FilePath, image), ct);
                written++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // El escritor es una abstracción: cualquier fallo es "no se pudo escribir ese archivo".
                errors.Add($"{Path.GetFileName(track.FilePath)}: {ex.Message}");
            }
        }

        return new CoverBatchResult(written, errors);
    }

    private static void Validate(byte[] image)
    {
        if (image.Length > CoverImage.MaxBytes)
            throw new ArgumentException("La imagen pesa más de 10 MB.");
        if (CoverImage.DetectMime(image) is null)
            throw new ArgumentException("La imagen debe ser JPG o PNG.");
    }
}