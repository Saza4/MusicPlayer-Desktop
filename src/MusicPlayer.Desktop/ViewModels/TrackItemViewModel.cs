using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using MusicPlayer.Core.Models;
using MusicPlayer.Desktop.Services;

namespace MusicPlayer.Desktop.ViewModels;

/// <summary>Adaptador de presentación: deja el modelo Track de Core sin lógica de UI.</summary>
public sealed class TrackItemViewModel(Track track, CoverArtLoader covers) : ObservableObject
{
    public Track Track { get; } = track;
    public string Title => Track.Title;
    public string Artist => Track.Artist;
    public string DurationText => TimeFormatter.Format(Track.Duration);

    // Se pide solo cuando la fila se muestra (la lista virtualiza) y se notifica al llegar.
    public Bitmap? Cover => covers.GetThumbnail(Track, () => OnPropertyChanged(nameof(Cover)));
}