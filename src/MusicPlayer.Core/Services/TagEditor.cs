using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Services;

/// <summary>Edita las etiquetas de una pista: primero el archivo y, solo si salió bien, la biblioteca.</summary>
public sealed class TagEditor(ITrackTagWriter writer, LibraryService library)
{
    public async Task<LibrarySnapshot> SaveAsync(Guid trackId, TrackTags tags, CancellationToken ct = default)
    {
        var track = library.Current.Tracks.FirstOrDefault(t => t.Id == trackId)
            ?? throw new KeyNotFoundException("La canción ya no está en la biblioteca.");

        var clean = tags.Normalized();
        if (clean.Title.Length == 0)
            throw new ArgumentException("El título no puede estar vacío.");

        await Task.Run(() => writer.Write(track.FilePath, clean), ct);
        return await library.ApplyTagsAsync(trackId, clean, ct);
    }
}