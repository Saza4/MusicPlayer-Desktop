using System.Globalization;
using System.Threading.Tasks;
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

            var coverProvider = new CachedCoverArtProvider(new CompositeCoverArtProvider(new ICoverArtProvider[]
            {
                new EmbeddedCoverArtProvider(),  // prioridad: carátula incrustada
                new FolderCoverArtProvider()     // respaldo: cover.jpg, folder.png...
            }));
            var covers = new CoverArtLoader(coverProvider);

            var settings = new SettingsService(new JsonSettingsRepository(AppPaths.Settings));
            var playlistManager = new PlaylistManager(new JsonPlaylistRepository(AppPaths.Playlists));
            var playlists = new PlaylistViewModel(playlistManager);
            var lyrics = new LyricsViewModel(lyricsProvider, player, settings);
            var viewModel = new MainViewModel(
                player, library, new AvaloniaFolderPickerService(window), lyrics, covers, playlists, settings);

            window.DataContext = viewModel;
            desktop.MainWindow = window;
            desktop.Exit += (_, _) =>
            {
                Task.Run(() => settings.FlushAsync()).GetAwaiter().GetResult();
                player.Dispose();
            };
            _ = viewModel.InitializeAsync(); // carga la biblioteca guardada sin bloquear el arranque
        }

        base.OnFrameworkInitializationCompleted();
    }
}