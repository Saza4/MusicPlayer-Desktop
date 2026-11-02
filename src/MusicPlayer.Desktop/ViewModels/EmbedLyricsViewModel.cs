using System;
using System.IO;
using System.Threading.Tasks;
using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;
using MusicPlayer.Desktop.Services;

namespace MusicPlayer.Desktop.ViewModels;

public class EmbedLyricsViewModel(LyricsEditor editor, ILyricsPickerService picker)
{
    private bool _busy;

    /// <summary>Antes de escribir: quien reproduce debe soltar ese archivo.</summary>
    public event Action<Track>? Saving;

    public event Action<string>? Notified;

    public async Task EmbedAsync(Track track)
    {
        if (_busy) return;
        _busy = true;
        try
        {
            if (!File.Exists(track.FilePath))
            {
                Notified?.Invoke($"\"{track.Title}\" no está disponible: el archivo se movió o se borró");
                return;
            }

            // Si junto a la canción hay un .lrc con su mismo nombre, esa es la letra que se quiere incrustar.
            var sibling = Path.ChangeExtension(track.FilePath, ".lrc");
            var source = File.Exists(sibling)
                ? sibling
                : await picker.PickLyricsAsync($"Letra para \"{track.Title}\"");
            if (source is null) return;

            Saving?.Invoke(track);
            await editor.EmbedAsync(track, source);
            Notified?.Invoke($"Letra de \"{track.Title}\" incrustada desde {Path.GetFileName(source)}");
        }
        catch (Exception ex)
        {
            Notified?.Invoke($"No se pudo incrustar la letra: {ex.Message}");
        }
        finally { _busy = false; }
    }
}