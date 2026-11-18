using CommunityToolkit.Mvvm.ComponentModel;
using MusicPlayer.Core.Models;

namespace MusicPlayer.Desktop.ViewModels;

public sealed partial class PlaylistItemViewModel(Playlist? playlist) : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Name))]
    [NotifyPropertyChangedFor(nameof(CountText))]
    [NotifyPropertyChangedFor(nameof(IsUserPlaylist))]
    private Playlist? _playlist = playlist;

    public bool IsLibrary => Playlist is null;

    /// <summary>Se muestra en «Playlists» de la barra lateral (la biblioteca se elige en «Tu biblioteca»).</summary>
    public bool IsUserPlaylist => Playlist is not null;
    public string Name => Playlist?.Name ?? "Biblioteca";
    public string CountText => Playlist is null ? "" : Playlist.TrackIds.Count.ToString();
}