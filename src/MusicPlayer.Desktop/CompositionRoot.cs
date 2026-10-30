using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using MusicPlayer.Audio;
using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Services;
using MusicPlayer.Desktop.Services;
using MusicPlayer.Desktop.ViewModels;
using MusicPlayer.Infrastructure;

namespace MusicPlayer.Desktop;

/// <summary>Único lugar donde se decide qué implementación usa cada interfaz.</summary>
public static class CompositionRoot
{
    public static ServiceProvider Build()
    {
        var services = new ServiceCollection();

        // Audio
        services.AddSingleton<IAudioPlayer, BassAudioPlayer>();

        // Biblioteca
        services.AddSingleton<ITrackMetadataReader, TagLibTrackMetadataReader>();
        services.AddSingleton<LibraryScanner>();
        services.AddSingleton<ILibraryRepository>(_ => new JsonLibraryRepository(AppPaths.Library));
        services.AddSingleton<LibraryService>();
        services.AddSingleton<ITrackTagWriter, TagLibTrackTagWriter>();
        services.AddSingleton<TagEditor>();

        // Playlists y ajustes
        services.AddSingleton<IPlaylistRepository>(_ => new JsonPlaylistRepository(AppPaths.Playlists));
        services.AddSingleton<PlaylistManager>();
        services.AddSingleton<ISettingsRepository>(_ => new JsonSettingsRepository(AppPaths.Settings));
        services.AddSingleton<SettingsService>();

        // Letras: .lrc primero, USLT de respaldo, y encima traducción/pronunciación.
        services.AddSingleton<ILyricParser, LrcLyricParser>();
        services.AddSingleton<ILyricsProvider>(sp =>
        {
            var parser = sp.GetRequiredService<ILyricParser>();
            var baseProvider = new CompositeLyricsProvider(new ILyricsProvider[]
            {
                new LrcFileLyricsProvider(parser),
                new EmbeddedLyricsProvider(parser)
            });
            return new CompanionLyricsProvider(
                baseProvider, parser, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
        });

        // Carátulas: incrustada primero, cover.jpg de respaldo, con caché.
        services.AddSingleton<ICoverArtProvider>(_ => new CachedCoverArtProvider(
            new CompositeCoverArtProvider(new ICoverArtProvider[]
            {
                new EmbeddedCoverArtProvider(),
                new FolderCoverArtProvider()
            })));
        services.AddSingleton<CoverArtLoader>();

        // Interfaz
        services.AddSingleton<IFolderPickerService, AvaloniaFolderPickerService>();
        services.AddSingleton<PlaylistViewModel>();
        services.AddSingleton<LyricsViewModel>();
        services.AddSingleton<TagEditorViewModel>();
        services.AddSingleton<MainViewModel>();

        // Un registro que falte falla aquí, al arrancar, y no la primera vez que se use.
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
    }
}