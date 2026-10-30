using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Services;

/// <summary>Edita las etiquetas de una o varias pistas: primero el archivo y, solo si salió bien, la biblioteca.</summary>
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

    /// <summary>
    /// Escribe los cambios en orden. Un fallo en una pista no detiene el resto, y al cancelar
    /// se conserva lo ya escrito. La biblioteca se actualiza una sola vez, solo con lo que se escribió.
    /// </summary>
    public async Task<TagBatchResult> ApplyBatchAsync(
        IReadOnlyList<TagChange> changes, IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var tracks = library.Current.Tracks.ToDictionary(t => t.Id);
        var applied = new List<TagChange>();
        var errors = new List<string>();

        await Task.Run(() =>
        {
            foreach (var change in changes)
            {
                if (ct.IsCancellationRequested) break;

                if (!tracks.TryGetValue(change.TrackId, out var track))
                {
                    errors.Add("Una canción ya no está en la biblioteca.");
                    continue;
                }

                var name = Path.GetFileName(track.FilePath);
                var after = change.After.Normalized();
                if (after.Title.Length == 0)
                {
                    errors.Add($"{name}: el título no puede estar vacío");
                    continue;
                }

                try
                {
                    writer.Write(track.FilePath, after);
                    applied.Add(change with { After = after });
                }
                catch (Exception ex) // el escritor es una abstracción: cualquier fallo es "no se pudo escribir"
                {
                    errors.Add($"{name}: {ex.Message}");
                }

                progress?.Report(applied.Count + errors.Count);
            }
        });

        var snapshot = await library.ApplyTagsBatchAsync(applied.ToDictionary(c => c.TrackId, c => c.After));
        return new TagBatchResult(snapshot, applied, errors);
    }

    /// <summary>Devuelve las pistas a las etiquetas que tenían antes del lote.</summary>
    public Task<TagBatchResult> UndoAsync(
        IReadOnlyList<TagChange> applied, IProgress<int>? progress = null, CancellationToken ct = default) =>
        ApplyBatchAsync(applied.Select(c => new TagChange(c.TrackId, c.After, c.Before)).ToList(), progress, ct);
}