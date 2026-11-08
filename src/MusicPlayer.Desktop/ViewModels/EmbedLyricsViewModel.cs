using System;
using System.IO;
using System.Threading.Tasks;
using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;
using MusicPlayer.Desktop.Services;

namespace MusicPlayer.Desktop.ViewModels;

public class EmbedLyricsViewModel(LyricsEditor editor, ILyricsPickerService picker, ILyricsSiblingFinder siblings)
{
    private bool _busy;

    /// <summary>Antes de escribir: quien reproduce debe soltar ese archivo.</summary>
    public event Action<Track>? Saving;

    public event Action<string>? Notified;

    /// <param name="allowPicker">
    /// Sin el .lrc principal junto al audio, abre el selector (clic derecho). Desde el inmersivo, no: avisa y no hace
    /// nada (docs/specs/lyrics-layers.md, criterio 12b).
    /// </param>
    /// <returns>
    /// True si pidió soltar el archivo con <see cref="Saving"/> (quien reproduce lo descargó), haya salido bien o no
    /// la escritura: entonces hay que volver a cargar la pista para seguir oyéndola.
    /// </returns>
    public async Task<bool> EmbedAsync(Track track, bool allowPicker = true)
    {
        var saving = false;
        if (_busy) return false;
        _busy = true;
        try
        {
            if (!File.Exists(track.FilePath))
            {
                Notified?.Invoke($"\"{track.Title}\" no está disponible: el archivo se movió o se borró");
                return false;
            }

            // Si junto a la canción hay un .lrc con su mismo nombre, esa es la letra que se quiere incrustar.
            var sibling = Path.ChangeExtension(track.FilePath, ".lrc");
            if (!File.Exists(sibling) && !allowPicker)
            {
                Notified?.Invoke($"No se incrustó la letra de \"{track.Title}\": falta {Path.GetFileName(sibling)} junto a la canción");
                return false;
            }
            var source = File.Exists(sibling)
                ? sibling
                : await picker.PickLyricsAsync($"Letra para \"{track.Title}\"");
            if (source is null) return false;

            // Las capas se toman junto al principal: junto al audio o junto al archivo elegido (spec, criterio 6).
            var layers = siblings.Find(source);

            saving = true;
            Saving?.Invoke(track);
            var report = await editor.EmbedAsync(track, source, layers);
            Notified?.Invoke(report.Describe(track.Title, Path.GetFileName(source)));
        }
        catch (Exception ex)
        {
            Notified?.Invoke($"No se pudo incrustar la letra: {ex.Message}");
        }
        finally { _busy = false; }
        return saving;
    }
}
