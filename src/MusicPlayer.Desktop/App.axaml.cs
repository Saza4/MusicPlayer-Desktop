using System.Globalization;
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
            var baseLyrics = new CompositeLyricsProvider(new ILyricsProvider[]
            {
                new LrcFileLyricsProvider(parser),   // prioridad: .lrc sincronizado
                new EmbeddedLyricsProvider(parser)   // respaldo: USLT / LYRICS
            });
            var lyricsProvider = new CompanionLyricsProvider(
                baseLyrics, parser, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
            var lyrics = new LyricsViewModel(lyricsProvider, player);

            var coverProvider = new CachedCoverArtProvider(new CompositeCoverArtProvider(new ICoverArtProvider[]
            {
                new EmbeddedCoverArtProvider(),  // prioridad: carátula incrustada
                new FolderCoverArtProvider()     // respaldo: cover.jpg, folder.png...
            }));
            var covers = new CoverArtLoader(coverProvider);

            var viewModel = new MainViewModel(player, library, new AvaloniaFolderPickerService(window), lyrics, covers);

            window.DataContext = viewModel;
            desktop.MainWindow = window;
            desktop.Exit += (_, _) => player.Dispose();
            _ = viewModel.InitializeAsync(); // carga la biblioteca guardada sin bloquear el arranque
        }

        base.OnFrameworkInitializationCompleted();
    }
}