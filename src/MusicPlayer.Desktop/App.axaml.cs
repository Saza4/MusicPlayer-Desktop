using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MusicPlayer.Audio;
using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Services;
using MusicPlayer.Desktop.Services;
using MusicPlayer.Desktop.ViewModels;
using MusicPlayer.Desktop.Views;
using MusicPlayer.Infrastructure;

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
            var player = new BassAudioPlayer();
            var scanner = new LibraryScanner(new TagLibTrackMetadataReader());
            var library = new LibraryService(scanner, new JsonLibraryRepository(JsonLibraryRepository.DefaultPath));

            var parser = new LrcLyricParser();
            var lyricsProvider = new CompositeLyricsProvider(new ILyricsProvider[]
            {
                new LrcFileLyricsProvider(parser),   // prioridad: .lrc sincronizado
                new EmbeddedLyricsProvider(parser)   // respaldo: USLT / LYRICS
            });
            var lyrics = new LyricsViewModel(lyricsProvider, player);
            var viewModel = new MainViewModel(player, library, new AvaloniaFolderPickerService(window), lyrics);

            window.DataContext = viewModel;
            desktop.MainWindow = window;
            desktop.Exit += (_, _) => player.Dispose();
            _ = viewModel.InitializeAsync(); // carga la biblioteca guardada sin bloquear el arranque
        }

        base.OnFrameworkInitializationCompleted();
    }
}