using System;
using System.Globalization;
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

    /// <summary>
    /// Incrusta la letra principal y las capas hermanas (clic derecho de la lista). Sin el .lrc principal junto al
    /// audio, abre el selector.
    /// </summary>
    /// <returns>
    /// True si pidió soltar el archivo con <see cref="Saving"/> (quien reproduce lo descargó), haya salido bien o no
    /// la escritura: entonces hay que volver a cargar la pista para seguir oyéndola.
    /// </returns>
    public async Task<bool> EmbedAsync(Track track)
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

    /// <summary>
    /// Incrusta una sola capa (traducción o pronunciación) elegida con el selector, de cualquier carpeta y sin tocar
    /// la letra principal (docs/specs/lyrics-layers.md, criterio 12b). El selector se abre <b>antes</b> de soltar el
    /// archivo: cancelarlo no corta la reproducción.
    /// </summary>
    /// <param name="main">Letra principal cargada: la capa debe ser del mismo tipo (con marcas de tiempo si ella las tiene).</param>
    /// <param name="beforeSaving">Se llama tras elegir el archivo y justo antes de soltar la pista (para anotar la posición).</param>
    /// <returns>Como <see cref="EmbedAsync"/>: True si soltó el archivo y hay que volver a cargar la pista.</returns>
    public async Task<bool> EmbedLayerAsync(Track track, LyricsLayerKind kind, LyricBlock main, Action? beforeSaving = null)
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

            var what = kind == LyricsLayerKind.Translation ? "Traducción" : "Pronunciación";
            var path = await picker.PickLyricsAsync($"{what} para \"{track.Title}\"");
            if (path is null) return false;

            // Idioma de la traducción: el del nombre del archivo («x.en.lrc») o, si no lo trae, el del sistema.
            string? language = null;
            var fromSystem = false;
            if (kind == LyricsLayerKind.Translation)
            {
                var system = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                language = LyricsLanguage.FromFileName(path, system);
                if (language is null)
                {
                    language = LyricsLanguage.FromSuffix(system, system);
                    fromSystem = true;
                    if (language is null)
                    {
                        Notified?.Invoke("No se incrustó la traducción: no se pudo saber su idioma. " +
                                         "Nombra el archivo con el idioma (por ejemplo cancion.es.lrc)");
                        return false;
                    }
                }
            }

            beforeSaving?.Invoke();
            saving = true;
            Saving?.Invoke(track);
            var report = await editor.EmbedLayerAsync(track, new LyricsLayerSource(kind, language, path), main);
            Notified?.Invoke(report.DescribeLayer(track.Title, Path.GetFileName(path), fromSystem));
        }
        catch (Exception ex)
        {
            Notified?.Invoke($"No se pudo incrustar la letra: {ex.Message}");
        }
        finally { _busy = false; }
        return saving;
    }
}
