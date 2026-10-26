using CommunityToolkit.Mvvm.ComponentModel;
using MusicPlayer.Core.Models;

namespace MusicPlayer.Desktop.ViewModels;

public sealed partial class LyricLineViewModel(LyricLine line, bool isSynced) : ObservableObject
{
    public LyricLine Line { get; } = line;
    public bool IsSynced { get; } = isSynced;

    // Las líneas vacías de un .lrc marcan pausas instrumentales.
    public string Text => string.IsNullOrWhiteSpace(Line.Text) ? "♪" : Line.Text;

    [ObservableProperty] private bool _isActive;
}