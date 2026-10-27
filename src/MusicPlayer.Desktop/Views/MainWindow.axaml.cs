using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using MusicPlayer.Desktop.ViewModels;

namespace MusicPlayer.Desktop.Views;

public partial class MainWindow : Window
{
    private WindowState _stateBeforeImmersive = WindowState.Normal;

    public MainWindow()
    {
        InitializeComponent();

        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainViewModel vm)
                vm.PropertyChanged += OnViewModelPropertyChanged;
        };
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.IsImmersive) || sender is not MainViewModel vm)
            return;

        if (vm.IsImmersive)
        {
            _stateBeforeImmersive = WindowState == WindowState.FullScreen ? WindowState.Normal : WindowState;
            WindowState = WindowState.FullScreen;
        }
        else
        {
            WindowState = _stateBeforeImmersive;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && DataContext is MainViewModel { IsImmersive: true } vm)
        {
            vm.CloseImmersiveCommand.Execute(null);
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    // Puro código de vista: delega toda la lógica en el ViewModel.
    private void OnTrackDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.SelectedTrack is { } track)
            vm.PlayTrackCommand.Execute(track);
    }

    private void OnLyricTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel vm && (e.Source as Control)?.DataContext is LyricLineViewModel line)
            vm.Lyrics.SeekToLineCommand.Execute(line);
    }
}