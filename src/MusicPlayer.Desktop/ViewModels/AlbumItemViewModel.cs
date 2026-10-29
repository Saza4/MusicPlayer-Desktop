using System.Linq;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using MusicPlayer.Core.Models;
using MusicPlayer.Desktop.Services;

namespace MusicPlayer.Desktop.ViewModels;

public sealed class AlbumItemViewModel(AlbumGroup group, CoverArtLoader covers) : ObservableObject
{
    public AlbumGroup Group { get; } = group;
    public string Title => Group.Album;

    public string Subtitle => string.Join(" · ", new[]
    {
        Group.Artist,
        Group.Year > 0 ? Group.Year.ToString() : null,
        Plural.Of(Group.Tracks.Count, "canción", "canciones")
    }.Where(s => s is not null));

    public Bitmap? Cover => covers.GetThumbnail(Group.Tracks[0], () => OnPropertyChanged(nameof(Cover)));
}