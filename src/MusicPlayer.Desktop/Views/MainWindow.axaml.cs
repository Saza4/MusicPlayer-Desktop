using Avalonia.Controls;
using Avalonia.Input;
using MusicPlayer.Desktop.ViewModels;

namespace MusicPlayer.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    // Puro código de vista: delega toda la lógica en el ViewModel.
    private void OnTrackDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.SelectedTrack is { } track)
            vm.PlayTrackCommand.Execute(track);
    }
}