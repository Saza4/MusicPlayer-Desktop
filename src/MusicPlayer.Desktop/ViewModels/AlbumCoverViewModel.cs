using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;
using MusicPlayer.Desktop.Services;

namespace MusicPlayer.Desktop.ViewModels;

public class AlbumCoverViewModel(
    CoverEditor editor, CoverArtLoader covers, IImagePickerService picker, LibraryService library)
{
    private bool _busy;

    /// <summary>Antes de escribir: quien reproduce debe soltar ese archivo.</summary>
    public event Action<Track>? Saving;

    /// <summary>Terminó: la biblioteca actual, para que las filas vuelvan a pedir su carátula.</summary>
    public event Action<LibrarySnapshot>? Applied;

    public event Action<string>? Notified;

    public async Task ApplyAsync(Track track)
    {
        if (_busy) return;
        _busy = true;
        try
        {
            var scope = AlbumScope.Of(track, library.Current.Tracks);
            var available = scope.Where(t => File.Exists(t.FilePath)).ToList();
            if (available.Count == 0)
            {
                Notified?.Invoke("Esas canciones no están disponibles: los archivos se movieron o se borraron");
                return;
            }

            var label = scope.Count == 1
                ? $"\"{track.Title}\""
                : $"\"{track.Album}\" ({Plural.Of(available.Count, "canción", "canciones")})";

            var path = await picker.PickImageAsync($"Carátula para {label}");
            if (path is null) return;

            if (new FileInfo(path).Length > CoverImage.MaxBytes)
            {
                Notified?.Invoke("La imagen pesa más de 10 MB");
                return;
            }
            var image = await File.ReadAllBytesAsync(path); // el tipo lo valida CoverEditor

            Notified?.Invoke($"Aplicando carátula a {Plural.Of(available.Count, "canción", "canciones")}…");
            foreach (var item in available) Saving?.Invoke(item);

            var result = await editor.SetManyAsync(available, image);

            foreach (var item in available) covers.Invalidate(item.FilePath);
            Applied?.Invoke(library.Current); // antes de avisar: ApplySnapshot también escribe el estado
            Notified?.Invoke(Summarize(result, scope.Count - available.Count));
        }
        catch (Exception ex)
        {
            Notified?.Invoke($"No se pudo aplicar la carátula: {ex.Message}");
        }
        finally { _busy = false; }
    }

    private static string Summarize(CoverBatchResult r, int unavailable)
    {
        var parts = new System.Collections.Generic.List<string>
        {
            Plural.Of(r.Written, "canción actualizada", "canciones actualizadas")
        };
        if (unavailable > 0) parts.Add($"{unavailable} no disponibles");
        if (r.Errors.Count > 0) parts.Add($"{Plural.Of(r.Errors.Count, "error", "errores")}: {r.Errors[0]}");
        return string.Join("; ", parts);
    }
}