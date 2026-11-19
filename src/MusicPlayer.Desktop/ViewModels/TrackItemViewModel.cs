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
    public string Album => Track.Album;
    public string DurationText => TimeFormatter.Format(Track.Duration);

    // Se pide solo cuando la fila se muestra (la lista virtualiza) y se notifica al llegar.
    public Bitmap? Cover => covers.GetThumbnail(Track, () => OnPropertyChanged(nameof(Cover)));

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TrailingText))]
    private bool _isAvailable = true;

    /// <summary>Lo que se muestra a la derecha de la fila: la duración, o el aviso si falta el archivo.</summary>
    public string TrailingText => IsAvailable ? DurationText : "No disponible";

    /// <summary>Posición en la lista que se ve (columna «#»). La fija MainViewModel cada vez que la arma.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AlbumTrackNumber))]
    private int _number;

    /// <summary>En la página de artista: el número de pista de la etiqueta, o la posición si no lo trae.</summary>
    public string AlbumTrackNumber => (Track.TrackNumber > 0 ? (int)Track.TrackNumber : Number).ToString();

    /// <summary>«DISCO 2» sobre la primera pista de cada disco después del primero (página de artista).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDiscHeader))]
    private string? _discHeader;

    public bool HasDiscHeader => DiscHeader is not null;

    /// <summary>Es la canción que suena: la fila muestra las barras en lugar del número y el título en azul.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotPlaying))]
    private bool _isPlaying;

    public bool IsNotPlaying => !IsPlaying;
}
