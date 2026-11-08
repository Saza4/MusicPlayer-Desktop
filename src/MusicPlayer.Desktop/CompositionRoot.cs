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
                baseProvider, parser,
                CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, CultureInfo.CurrentUICulture.Name);
        });

        // Carátulas: la caché es un solo objeto visto como proveedor y como "invalidable".
        services.AddSingleton(_ => new CachedCoverArtProvider(
            new CompositeCoverArtProvider(new ICoverArtProvider[]
            {
        new EmbeddedCoverArtProvider(),
        new FolderCoverArtProvider()
            })));
        services.AddSingleton<ICoverArtProvider>(sp => sp.GetRequiredService<CachedCoverArtProvider>());
        services.AddSingleton<ICoverArtCache>(sp => sp.GetRequiredService<CachedCoverArtProvider>());
        services.AddSingleton<CoverArtLoader>();

        services.AddSingleton<ICoverArtWriter, TagLibCoverArtWriter>();
        services.AddSingleton<CoverEditor>();
        services.AddSingleton<ILyricsWriter, TagLibLyricsWriter>();
        services.AddSingleton<LyricsEditor>();
        services.AddSingleton<IAudioConverter, FfmpegAudioConverter>();
        services.AddSingleton<ConversionService>();

        // Los selectores avisan a ModalTracker mientras están abiertos (la isla no aparece encima).
        services.AddSingleton<ModalTracker>();
        services.AddSingleton<IImagePickerService, AvaloniaImagePickerService>();
        services.AddSingleton<ILyricsPickerService, AvaloniaLyricsPickerService>();
        services.AddSingleton<ConvertViewModel>();
        services.AddSingleton<AlbumCoverViewModel>();
        services.AddSingleton<EmbedLyricsViewModel>();

        // Temas: los de usuario son archivos .json en la carpeta themes; ThemeService los aplica a la interfaz.
        services.AddSingleton<IThemeRepository>(_ => new JsonThemeRepository(AppPaths.Themes));
        services.AddSingleton<ThemeService>();
        services.AddSingleton<ThemeViewModel>();

        // Interfaz
        services.AddSingleton<IFolderPickerService, AvaloniaFolderPickerService>();
        services.AddSingleton<PlaylistViewModel>();
        services.AddSingleton<LyricsViewModel>();
        services.AddSingleton<TagEditorViewModel>();
        services.AddSingleton<TagAutoFixViewModel>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<IslandController>(); // isla flotante de «ahora suena» (solo Windows)

        // Un registro que falte falla aquí, al arrancar, y no la primera vez que se use.
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
    }
}