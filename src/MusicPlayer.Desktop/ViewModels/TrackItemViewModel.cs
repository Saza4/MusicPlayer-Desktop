using MusicPlayer.Core.Models;

namespace MusicPlayer.Desktop.ViewModels;

/// <summary>Adaptador de presentación: deja el modelo Track de Core sin lógica de UI.</summary>
public sealed class TrackItemViewModel(Track track)
{
    public Track Track { get; } = track;
    public string Title => Track.Title;
    public string Artist => Track.Artist;
    public string DurationText => TimeFormatter.Format(Track.Duration);
}