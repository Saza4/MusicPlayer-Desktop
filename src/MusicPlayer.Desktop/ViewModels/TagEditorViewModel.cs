using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;
using MusicPlayer.Desktop.Services;

namespace MusicPlayer.Desktop.ViewModels;

public partial class TagEditorViewModel(
    TagEditor editor, CoverEditor coverEditor, CoverArtLoader covers, IImagePickerService imagePicker)
    : ObservableObject
{
    private Track? _target;
    private bool _busy;
    private byte[]? _newCover;   // imagen elegida, aún sin escribir
    private bool _removeCover;   // quitar la carátula al guardar
    private int _loadVersion;    // descarta vistas previas de una apertura anterior

    /// <summary>Antes de escribir: quien reproduce debe soltar el archivo.</summary>
    public event Action<Track>? Saving;

    /// <summary>Después de guardar: la biblioteca nueva, ya reordenada.</summary>
    public event Action<LibrarySnapshot>? Saved;

    /// <summary>La escritura terminó, bien o mal: quien soltó el archivo en <see cref="Saving"/> puede volver a cargarlo.</summary>
    public event Action? Finished;

    public event Action<string>? Notified;

    [ObservableProperty] private bool _isOpen;
    [ObservableProperty] private string _heading = "";
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _artist = "";
    [ObservableProperty] private string _albumArtist = "";
    [ObservableProperty] private string _album = "";
    [ObservableProperty] private string _yearText = "";
    [ObservableProperty] private string _trackText = "";
    [ObservableProperty] private string _discText = "";
    [ObservableProperty] private Bitmap? _coverPreview;
    [ObservableProperty] private string _coverNote = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _error = "";

    public bool HasError => Error.Length > 0;

    public void Begin(TrackItemViewModel item)
    {
        if (!File.Exists(item.Track.FilePath))
        {
            Notified?.Invoke($"\"{item.Title}\" no está disponible: el archivo se movió o se borró");
            return;
        }

        var tags = TrackTags.From(item.Track);
        _target = item.Track;

        Heading = $"Editar etiquetas — {Path.GetFileName(item.Track.FilePath)}";
        Title = tags.Title;
        Artist = tags.Artist;
        AlbumArtist = tags.AlbumArtist;
        Album = tags.Album;
        YearText = tags.Year == 0 ? "" : tags.Year.ToString();
        TrackText = tags.TrackNumber == 0 ? "" : tags.TrackNumber.ToString();
        DiscText = tags.DiscNumber == 0 ? "" : tags.DiscNumber.ToString();
        Error = "";

        ResetCover();
        _ = LoadCurrentCoverAsync(item.Track, _loadVersion);
        IsOpen = true;
    }

    private void ResetCover()
    {
        _newCover = null;
        _removeCover = false;
        _loadVersion++;
        CoverPreview = null;
        CoverNote = "";
    }

    private async Task LoadCurrentCoverAsync(Track track, int version)
    {
        try
        {
            var cover = await covers.LoadLargeAsync(track);
            if (version == _loadVersion && _newCover is null && !_removeCover)
                CoverPreview = cover;
        }
        catch (Exception)
        {
            // La vista previa es decorativa: sin ella el editor funciona igual.
        }
    }

    [RelayCommand]
    private async Task ChooseCoverAsync()
    {
        try
        {
            var path = await imagePicker.PickImageAsync();
            if (path is null) return;

            if (new FileInfo(path).Length > CoverImage.MaxBytes)
            {
                Error = "La imagen pesa más de 10 MB.";
                return;
            }

            var bytes = await File.ReadAllBytesAsync(path);
            if (CoverImage.DetectMime(bytes) is null)
            {
                Error = "La imagen debe ser JPG o PNG.";
                return;
            }

            // Se decodifica a tamaño de vista previa: no hace falta cargar una imagen de 3000 px.
            var preview = await Task.Run(() =>
            {
                using var stream = new MemoryStream(bytes);
                return Bitmap.DecodeToWidth(stream, 240);
            });

            _newCover = bytes;
            _removeCover = false;
            CoverPreview = preview;
            CoverNote = "Nueva carátula: se escribirá en el archivo al guardar.";
            Error = "";
        }
        catch (Exception ex)
        {
            Error = $"No se pudo leer la imagen: {ex.Message}";
        }
    }

    [RelayCommand]
    private void RemoveCover()
    {
        _newCover = null;
        _removeCover = true;
        _loadVersion++;
        CoverPreview = null;
        CoverNote = "Se quitará la carátula del archivo al guardar.";
    }

    [RelayCommand]
    private void Cancel()
    {
        _target = null;
        Error = "";
        ResetCover();
        IsOpen = false;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (_target is not { } target || _busy) return;

        if (!TryParse(YearText, 9999, out var year)) { Error = "El año debe ser un número de hasta 4 cifras."; return; }
        if (!TryParse(TrackText, 9999, out var number)) { Error = "La pista debe ser un número."; return; }
        if (!TryParse(DiscText, 999, out var disc)) { Error = "El disco debe ser un número."; return; }

        _busy = true;
        try
        {
            Saving?.Invoke(target);
            var snapshot = await editor.SaveAsync(
                target.Id, new TrackTags(Title, Artist, AlbumArtist, Album, year, number, disc));

            // Las etiquetas ya están escritas. Si la carátula falla, no se deshace lo anterior: se avisa.
            string? coverError = null;
            if (_newCover is not null || _removeCover)
            {
                try
                {
                    await coverEditor.SetAsync(target.Id, _removeCover ? null : _newCover);
                    covers.Invalidate(target.FilePath);
                }
                catch (Exception ex)
                {
                    coverError = ex.Message;
                }
            }

            var name = Title.Trim();
            Cancel();
            Saved?.Invoke(snapshot);
            Notified?.Invoke(coverError is null
                ? $"Etiquetas de \"{name}\" guardadas"
                : $"Etiquetas guardadas, pero la carátula no se pudo escribir: {coverError}");
        }
        catch (Exception ex)
        {
            Error = ex.Message; // el panel sigue abierto para corregir o cancelar
        }
        finally
        {
            _busy = false;
            Finished?.Invoke();
        }
    }

    private static bool TryParse(string text, uint max, out uint value)
    {
        text = text.Trim();
        if (text.Length == 0) { value = 0; return true; } // vacío = sin etiqueta
        return uint.TryParse(text, out value) && value <= max;
    }
}