using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using MusicPlayer.Core.Models;
using MusicPlayer.Desktop.Services;

namespace MusicPlayer.Desktop.ViewModels;

/// <summary>Adaptador de presentación: deja el modelo Track de Core sin lógica de UI.</summary>
public sealed partial class TrackItemViewModel(Track track, CoverArtLoader covers) : ObservableObject
{
    public Track Track { get; } = track;
    public string Title => Track.Title;
    public string Artist => Track.Artist;
    public string DurationText => TimeFormatter.Format(Track.Duration);

    // Se pide solo cuando la fila se muestra (la lista virtualiza) y se notifica al llegar.
    public Bitmap? Cover => covers.GetThumbnail(Track, () => OnPropertyChanged(nameof(Cover)));

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TrailingText))]
    private bool _isAvailable = true;

    /// <summary>Lo que se muestra a la derecha de la fila: la duración, o el aviso si falta el archivo.</summary>
    public string TrailingText => IsAvailable ? DurationText : "No disponible";
}