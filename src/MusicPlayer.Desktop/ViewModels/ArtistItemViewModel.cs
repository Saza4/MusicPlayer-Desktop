using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using MusicPlayer.Core.Models;
using MusicPlayer.Desktop.Services;

namespace MusicPlayer.Desktop.ViewModels;

public sealed class ArtistItemViewModel(ArtistGroup group, CoverArtLoader covers) : ObservableObject
{
    public ArtistGroup Group { get; } = group;
    public string Name => Group.Name;

    public string Summary =>
        $"{Plural.Of(Group.Albums.Count, "álbum", "álbumes")} · {Plural.Of(Group.TrackCount, "canción", "canciones")}";

    /// <summary>Número de canciones a la derecha del nombre, en la lista de artistas.</summary>
    public string CountText => Group.TrackCount.ToString("N0", System.Globalization.CultureInfo.CurrentCulture);

    // Un artista siempre tiene al menos un álbum, y un álbum al menos una pista.
    public Bitmap? Cover =>
        covers.GetThumbnail(Group.Albums[0].Tracks[0], () => OnPropertyChanged(nameof(Cover)));
}