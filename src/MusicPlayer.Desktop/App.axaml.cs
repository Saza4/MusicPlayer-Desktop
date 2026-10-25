using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MusicPlayer.Desktop.ViewModels;
using MusicPlayer.Desktop.Views;

namespace MusicPlayer.Desktop;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Si tu plantilla incluye esta llamada, consérvala aquí arriba:
            // DisableAvaloniaDataAnnotationValidation();

            var window = new MainWindow();
            var player = new MusicPlayer.Audio.BassAudioPlayer();
            var scanner = new MusicPlayer.Core.Services.LibraryScanner(
                new MusicPlayer.Infrastructure.TagLibTrackMetadataReader());

            window.DataContext = new MainViewModel(
                player,
                scanner,
                new MusicPlayer.Desktop.Services.AvaloniaFolderPickerService(window));

            desktop.MainWindow = window;
            desktop.Exit += (_, _) => player.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }
}