using System;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;

namespace MusicPlayer.Desktop.ViewModels;

public partial class TagEditorViewModel(TagEditor editor) : ObservableObject
{
    private Track? _target;
    private bool _busy;

    /// <summary>Antes de escribir: quien reproduce debe soltar el archivo.</summary>
    public event Action<Track>? Saving;

    /// <summary>Después de guardar: la biblioteca nueva, ya reordenada.</summary>
    public event Action<LibrarySnapshot>? Saved;

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
        IsOpen = true;
    }

    [RelayCommand]
    private void Cancel()
    {
        _target = null;
        Error = "";
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

            var name = Title.Trim();
            Cancel();
            Saved?.Invoke(snapshot);
            Notified?.Invoke($"Etiquetas de \"{name}\" guardadas");
        }
        catch (Exception ex)
        {
            Error = ex.Message; // el panel sigue abierto para corregir o cancelar
        }
        finally { _busy = false; }
    }

    private static bool TryParse(string text, uint max, out uint value)
    {
        text = text.Trim();
        if (text.Length == 0) { value = 0; return true; } // vacío = sin etiqueta
        return uint.TryParse(text, out value) && value <= max;
    }
}