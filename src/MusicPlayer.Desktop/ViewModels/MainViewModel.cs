using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;
using MusicPlayer.Desktop.Services;

namespace MusicPlayer.Desktop.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly IAudioPlayer _player;
    private readonly LibraryService _library;
    private readonly IFolderPickerService _folderPicker;
    private readonly DispatcherTimer _timer;

    private readonly CoverArtLoader _covers;
    private readonly SettingsService _settings;
    private bool _restoring;
    // Órdenes de carga y descarga dadas al reproductor. Se avanza justo DESPUÉS de cada una: el reproductor cambia
    // de pista y avisa en un solo paso, así que un aviso anterior a la orden siempre anota la cuenta vieja.
    private readonly PlaybackEpoch _epoch = new();
    private CancellationTokenSource? _coverCts;

    [ObservableProperty] private Bitmap? _nowPlayingCover;
    [ObservableProperty] private bool _isImmersive;
    [ObservableProperty] private double _volume = 1.0;

    [ObservableProperty] private bool _isShuffleOn;

    // Isla flotante de «ahora suena»: ajuste del usuario. Solo existe en Windows.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IslandToggleText))]
    private bool _showIsland = true;
    public bool IsIslandSupported { get; } = OperatingSystem.IsWindows();
    // Texto del botón de la barra: dice lo que hará el clic (como MuteText).
    public string IslandToggleText => ShowIsland ? "Ocultar isla flotante" : "Mostrar isla flotante";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RepeatText))]
    private RepeatMode _repeat;

    public string RepeatText => Repeat switch
    {
        RepeatMode.All => "Repetir: todas",
        RepeatMode.One => "Repetir: una",
        _ => "Repetir: no"
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConfirmingDelete), nameof(DeletePrompt))]
    private TrackItemViewModel? _trackPendingDelete;

    public bool IsConfirmingDelete => TrackPendingDelete is not null;

    public string DeletePrompt => TrackPendingDelete is { } track
        ? $"¿Eliminar \"{track.Title}\" del disco? El archivo se borrará de forma permanente, sin pasar por la papelera."
        : "";

    private HashSet<Guid> _missingIds = new(); // fuente de verdad de qué pistas faltan

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMissing), nameof(MissingPrompt))]
    private int _missingCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MissingPrompt))]
    private bool _isConfirmingPrune;

    public bool HasMissing => MissingCount > 0;

    public string MissingPrompt
    {
        get
        {
            var songs = MissingCount == 1 ? "1 canción" : $"{MissingCount} canciones";
            return IsConfirmingPrune
                ? $"¿Quitar {songs} de la biblioteca y de sus playlists? Los archivos no se tocan. Si desconectaste un disco, conéctalo y cancela."
                : $"{songs} no disponible{(MissingCount == 1 ? "" : "s")}: el archivo se movió, se borró o el disco está desconectado. " + "Si lo moviste, agrega su nueva carpeta y se reubicará con sus playlists.";
        }
    }

    partial void OnMissingCountChanged(int value)
    {
        if (value == 0) IsConfirmingPrune = false; // nada que confirmar
        UpdateListSummary(CurrentSource().Count, !string.IsNullOrWhiteSpace(SearchText)); // «· 3 no disponibles»
    }

    // ---- Navegación por álbum y artista ----
    [ObservableProperty] private int _browseIndex; // 0 Canciones, 1 Álbumes, 2 Artistas
    [ObservableProperty] private ObservableCollection<AlbumItemViewModel> _albums = new();
    [ObservableProperty] private ObservableCollection<ArtistItemViewModel> _artists = new();
    private string? _artistScope;     // artista abierto
    private AlbumGroup? _albumScope;  // álbum abierto
    private BrowseMode Mode => (BrowseMode)Math.Max(0, BrowseIndex);
    private bool InLibrary => Playlists.CurrentPlaylist is null;

    // Rediseño (docs/specs/redesign-mockups.md, mockup 1c): «Artistas» es una página con la lista de artistas a la
    // izquierda, las carátulas de sus álbumes arriba y las pistas del álbum elegido debajo. Siempre hay un artista y un
    // álbum elegidos (el primero si no se eligió ninguno). «Álbumes» sigue siendo una lista que se abre a sus pistas.
    public bool ShowArtistPage => InLibrary && Mode == BrowseMode.Artists;
    public bool ShowAlbums => InLibrary && Mode == BrowseMode.Albums && _albumScope is null;
    public bool ShowSongs => !ShowArtistPage && !ShowAlbums;
    public bool ShowListHeader => !ShowArtistPage;
    public bool ShowBackBar => InLibrary && Mode == BrowseMode.Albums && _albumScope is not null;
    public string BackText => "← Álbumes";

    /// <summary>
    /// Vistas de «Tu biblioteca», en el orden de sus filas en la barra lateral. «Álbumes» está comentado (oculto, no
    /// borrado): para recuperarlo, descomentarlo aquí y su ListBoxItem en MainWindow.axaml.
    /// </summary>
    private static readonly BrowseMode[] NavModes =
    [
        BrowseMode.Songs,
        // BrowseMode.Albums,
        BrowseMode.Artists,
    ];

    /// <summary>
    /// Fila elegida en «Tu biblioteca» (posición en <see cref="NavModes"/>), o -1 con una playlist abierta.
    /// Elegir una fila vuelve a la biblioteca y cambia de vista.
    /// </summary>
    public int LibraryNavIndex
    {
        get => InLibrary ? Array.IndexOf(NavModes, Mode) : -1;
        set
        {
            if (value < 0 || value >= NavModes.Length) return; // -1: se abrió una playlist, no es una orden
            var mode = NavModes[value];
            if (mode == BrowseMode.Artists) _libraryNavRequested = false; // entrar en Artistas trae su lista
            if (!InLibrary) Playlists.Selected = Playlists.Items[0];
            BrowseIndex = (int)mode;
            OnPropertyChanged();
        }
    }

    /// <summary>Número junto a «Canciones» en la barra lateral, con separador de miles («1.284»).</summary>
    public string LibraryCountText => _allTracks.Count.ToString("N0", CultureInfo.CurrentCulture);

    // Página de artista
    [ObservableProperty] private ArtistItemViewModel? _selectedArtist;
    [ObservableProperty] private AlbumItemViewModel? _selectedAlbum;
    [ObservableProperty] private string _artistPageSummary = "";
    [ObservableProperty] private string _albumSectionTitle = "";
    [ObservableProperty] private string _albumSectionYear = "";
    [ObservableProperty] private string _albumSectionDisc = "";
    private bool _syncingBrowse; // mientras ApplyFilter fija la selección, no es un clic del usuario

    // Elegir en la lista abre al terminar el clic: la página se rehace y cambiar las colecciones de una lista mientras
    // esta atiende su propia selección no es buena idea.
    partial void OnSelectedArtistChanged(ArtistItemViewModel? value)
    {
        if (!_syncingBrowse && value is not null)
            Dispatcher.UIThread.Post(() => { if (SelectedArtist == value) OpenArtist(value); });
    }

    partial void OnSelectedAlbumChanged(AlbumItemViewModel? value)
    {
        if (!_syncingBrowse && value is not null && ShowArtistPage)
            Dispatcher.UIThread.Post(() => { if (SelectedAlbum == value) OpenAlbum(value); });
    }

    // Lo que la página de artista ya agrupó: la lista de artistas y la de álbumes solo se rehacen si cambia la
    // biblioteca, la búsqueda o el artista; así no pierden el desplazamiento al elegir otro álbum.
    private (IReadOnlyList<TrackItemViewModel> Library, string Search)? _artistsBuiltFor;
    private string? _albumsBuiltFor;

    // Página de artista: la lista de artistas ocupa el sitio de la barra lateral (mockup 1c). El botón ← vuelve a
    // mostrar «Tu biblioteca» sin salir de la página; pulsar «Artistas» trae la lista otra vez. Al salir de la página
    // por cualquier camino, ShowArtistPage es falso y la barra es la de siempre.
    private bool _libraryNavRequested;
    private bool _wasArtistPage;

    public bool ShowArtistSidebar => ShowArtistPage && !_libraryNavRequested;

    [RelayCommand]
    private void ShowLibraryNav()
    {
        _libraryNavRequested = true;
        NotifyBrowse();
    }

    [RelayCommand]
    private void ShowArtistList()
    {
        _libraryNavRequested = false;
        NotifyBrowse();
    }

    partial void OnBrowseIndexChanged(int value)
    {
        _artistScope = null;
        _albumScope = null;
        if ((BrowseMode)Math.Max(0, value) == BrowseMode.Artists) _libraryNavRequested = false;
        ApplyFilter();
    }

    private void NotifyBrowse()
    {
        if (_wasArtistPage && !ShowArtistPage) _covers.ReleaseCards(); // las tarjetas de 512 px no hacen falta fuera
        _wasArtistPage = ShowArtistPage;

        OnPropertyChanged(nameof(ShowArtistPage));
        OnPropertyChanged(nameof(ShowArtistSidebar));
        OnPropertyChanged(nameof(ShowAlbums));
        OnPropertyChanged(nameof(ShowSongs));
        OnPropertyChanged(nameof(ShowListHeader));
        OnPropertyChanged(nameof(ShowBackBar));
        OnPropertyChanged(nameof(LibraryNavIndex));
        OnPropertyChanged(nameof(LibraryCountText));
    }

    [RelayCommand]
    private void OpenArtist(ArtistItemViewModel? artist)
    {
        if (artist is null) return;
        _artistScope = artist.Name;
        _albumScope = null; // se elige su primer álbum
        ApplyFilter();
    }

    [RelayCommand]
    private void OpenAlbum(AlbumItemViewModel? album)
    {
        if (album is null) return;
        _albumScope = album.Group;
        ApplyFilter();
    }

    [RelayCommand]
    private void GoBack()
    {
        _albumScope = null;
        ApplyFilter();
    }

    /// <summary>Reproduce lo que se ve (álbum o discografía del artista) desde el principio.</summary>
    [RelayCommand]
    private void PlayScope()
    {
        if (Tracks.Count > 0)
            PlayTrack(Tracks[0]);
    }

    public LyricsViewModel Lyrics { get; }
    public PlaylistViewModel Playlists { get; }
    public TagEditorViewModel TagEditor { get; }
    public TagAutoFixViewModel AutoFix { get; }
    public ConvertViewModel Conversion { get; }
    public AlbumCoverViewModel AlbumCover { get; }
    public EmbedLyricsViewModel EmbedLyrics { get; }
    public ThemeViewModel Themes { get; }

    private IReadOnlyList<TrackItemViewModel> _allTracks = Array.Empty<TrackItemViewModel>();
    private readonly PlayQueue<TrackItemViewModel> _queue = new();
    private readonly DispatcherTimer _searchDebounce;
    [ObservableProperty] private string _searchText = "";

    // Evita el bucle timer -> Slider -> Seek -> timer. Solo lo toca SetPositionSilently.
    private bool _isUpdatingFromTimer;

    [ObservableProperty] private ObservableCollection<TrackItemViewModel> _tracks = new();
    [ObservableProperty] private TrackItemViewModel? _selectedTrack;
    [ObservableProperty] private TrackItemViewModel? _currentTrack;

    [ObservableProperty] private string _status = "Agrega una carpeta para empezar";

    [ObservableProperty] private string _nowPlaying = "Nada en reproducción";

    // Cabecera de la lista (docs/specs/redesign-mockups.md): título grande y resumen («1.284 canciones · 3 no
    // disponibles»). La línea de estado, al pie, dice «Biblioteca al día.» o el último aviso.
    [ObservableProperty] private string _listHeading = "Canciones";
    [ObservableProperty] private string _listSummary = "";

    // La fila de la canción que suena se marca (barras en «#», título en azul). Se compara por Id y se recorre toda la
    // biblioteca: tras volver a cargarla, la fila marcada es una instancia nueva y no la que guarda CurrentTrack.
    partial void OnCurrentTrackChanged(TrackItemViewModel? oldValue, TrackItemViewModel? newValue)
    {
        var playingId = newValue?.Track.Id;
        if (oldValue is not null) oldValue.IsPlaying = false;
        foreach (var item in _allTracks) item.IsPlaying = item.Track.Id == playingId;
        if (newValue is not null) newValue.IsPlaying = true;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayPauseText))]
    private bool _isPlaying;

    [ObservableProperty] private double _positionSeconds;
    [ObservableProperty] private double _durationSeconds = 1; // Slider.Maximum debe ser > 0
    [ObservableProperty] private string _positionText = "0:00";
    [ObservableProperty] private string _durationText = "0:00";
    [ObservableProperty] private string _remainingText = ""; // «−1:31»; vacío sin pista

    private void UpdateRemainingText() =>
        RemainingText = TimeText.Remaining(TimeSpan.FromSeconds(PositionSeconds), _player.Duration);

    public string PlayPauseText => IsPlaying ? "Pausa" : "Reproducir";

    public MainViewModel(IAudioPlayer player, LibraryService library,
                         IFolderPickerService folderPicker, LyricsViewModel lyrics,
                         CoverArtLoader covers, PlaylistViewModel playlists,
                         SettingsService settings, TagEditorViewModel tagEditor,
                         TagAutoFixViewModel autoFix, ConvertViewModel conversion,
                         AlbumCoverViewModel albumCover, EmbedLyricsViewModel embedLyrics,
                         ThemeViewModel themes, SettingsViewModel settingsPanel, WriteSessions writes,
                         FolderRefreshPolicy folderRefresh)
    {
        _writes = writes;
        _folderRefresh = folderRefresh;
        Settings = settingsPanel;
        _covers = covers;
        _settings = settings;
        Lyrics = lyrics;
        Playlists = playlists;
        TagEditor = tagEditor;
        AutoFix = autoFix;
        Conversion = conversion;
        AlbumCover = albumCover;
        EmbedLyrics = embedLyrics;
        Themes = themes;
        _player = player;
        _library = library;
        _folderPicker = folderPicker;

        // BASS dispara estos eventos desde su propio hilo: hay que volver al hilo de la UI.
        _player.StateChanged += (_, state) =>
            Dispatcher.UIThread.Post(() => IsPlaying = state == PlaybackState.Playing);
        // Cada aviso anota la cuenta de órdenes del momento en que sale: si al atenderlo el usuario ya cargó otra
        // pista o se descargó la actual, es un aviso viejo y no debe mover la cola (docs/specs/audit-3.md, H1).
        _player.TrackEnded += (_, _) =>
        {
            var stamp = _epoch.Current;
            Dispatcher.UIThread.Post(() => { if (_epoch.IsCurrent(stamp)) OnTrackEnded(); });
        };
        _player.AdvancedToNext += (_, path) =>
        {
            var stamp = _epoch.Current;
            Dispatcher.UIThread.Post(() => { if (_epoch.IsCurrent(stamp)) OnAdvancedToNext(path); });
        };

        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(250), DispatcherPriority.Background, OnTick);
        _timer.Start();

        // Espera 200 ms tras la última tecla para no refiltrar en cada pulsación.
        _searchDebounce = new DispatcherTimer(TimeSpan.FromMilliseconds(200), DispatcherPriority.Normal,
            (_, _) => { _searchDebounce.Stop(); ApplyFilter(); });
        // Ese constructor deja el temporizador en marcha: sin pararlo, a los 200 ms filtraba solo y pisaba la
        // línea de estado del arranque (docs/specs/ui-responsiveness.md, criterio 1).
        _searchDebounce.Stop();

        Playlists.Changed += (_, _) => ApplyFilter();
        Playlists.Notified += Notify;
        TagEditor.Saved += OnTagsSaved;
        TagEditor.Notified += message => Status = message;
        AutoFix.Applied += OnTagsSaved; // el mismo manejador del editor manual
        AutoFix.Notified += message => Status = message;
        Conversion.Added += ApplySnapshot;
        Conversion.Notified += message => Status = message;
        AlbumCover.Applied += ApplySnapshot;
        AlbumCover.Notified += message => Status = message;
        EmbedLyrics.Notified += message => Status = message;
        // Las herramientas que escriben en audio abren una sesión: al abrirla se suelta el archivo y, al cerrarse
        // la última, se vuelve a cargar (docs/specs/write-sessions.md).
        _writes.Releasing += OnWriting;
        _writes.AllFinished += ResumeInterrupted;
        Lyrics.SeekRequested += SeekTo;
        Themes.Notified += Notify;

        // Configuración (docs/specs/settings-panel.md) se cierra si se abre un panel de edición, que ocupa la misma zona.
        TagEditor.PropertyChanged += ClosePanelSettingsWhenOpened;
        AutoFix.PropertyChanged += ClosePanelSettingsWhenOpened;
        Conversion.PropertyChanged += ClosePanelSettingsWhenOpened;
    }

    public SettingsViewModel Settings { get; }

    private void ClosePanelSettingsWhenOpened(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != "IsOpen") return;
        if (TagEditor.IsOpen || AutoFix.IsOpen || Conversion.IsOpen)
            Settings.IsOpen = false;
    }

    partial void OnIsImmersiveChanged(bool value)
    {
        if (value) Settings.IsOpen = false; // el inmersivo lo tapa todo: al volver, Configuración está cerrada
    }

    // Avisos que llegan mientras la app arranca (un tema que no se pudo cargar, playlists.json ilegible): la carga
    // escribe después en la línea de estado y los pisaba. Se guardan y se muestran al terminar
    // (docs/specs/audit-3.md, H11).
    private List<string>? _startupNotices = new();

    private void Notify(string message)
    {
        if (_startupNotices is { } pending) pending.Add(message);
        else Status = message;
    }

    private void ShowStartupNotices()
    {
        var pending = _startupNotices;
        _startupNotices = null;
        if (pending is { Count: > 0 })
            Status = _startupNotice = string.Join(" · ", pending);
    }

    // ---- Biblioteca ----

    public async Task InitializeAsync()
    {
        try
        {
            Status = "Cargando…";
            // Los ajustes no deben impedir que cargue la biblioteca (docs/specs/library-data-safety.md, criterio 2).
            var saved = AppSettings.Default;
            string? settingsProblem = null;
            try { saved = await _settings.LoadAsync(); }
            catch (Exception ex) { settingsProblem = $"No se pudieron leer los ajustes ({ex.Message}); se usan los de siempre y no se guardarán cambios hasta reiniciar"; }
            _restoring = true;
            try
            {
                Volume = Math.Clamp(saved.Volume, 0.0, 1.0);
                IsShuffleOn = saved.Shuffle;
                Repeat = saved.Repeat;
                ShowIsland = saved.ShowIsland;
                Lyrics.Restore(saved);
                Settings.Restore(saved);
            }
            finally
            {
                _restoring = false;
            }

            await Playlists.InitializeAsync();
            ApplySnapshot(await _library.LoadAsync());

            // La última canción queda seleccionada: Reproducir arranca desde ahí.
            if (saved.LastTrackId is { } lastId)
                SelectedTrack = _allTracks.FirstOrDefault(i => i.Track.Id == lastId);

            _ = CheckAvailabilityAsync(); // en segundo plano: no retrasa el arranque
            if (settingsProblem is not null) Notify(settingsProblem);
            ShowStartupNotices();

            // Música copiada a las carpetas agregadas con la app cerrada (docs/specs/auto-refresh-folders.md).
            _libraryLoaded = true;
            _ = RefreshFoldersAsync();
        }
        catch (Exception ex)
        {
            _startupNotices = null; // este aviso es el que importa
            Status = $"No se pudo cargar la biblioteca: {ex.Message}. No se guardará nada hasta reiniciar, para no perder tus datos";
        }
    }

    [RelayCommand]
    private async Task AddFolderAsync()
    {
        try
        {
            var folder = await _folderPicker.PickFolderAsync();
            if (folder is null) return;

            Status = "Escaneando…";
            var progress = new Progress<int>(count => Status = $"Escaneando… {count} canciones");
            var result = await _library.ScanFolderAsync(folder, progress);
            ApplySnapshot(result.Snapshot);
            _ = CheckAvailabilityAsync();
            // ApplySnapshot escribe el estado general: estos mensajes van después.
            var notes = new List<string>();
            if (result.Relocated > 0)
                notes.Add($"{Plural.Of(result.Relocated, "canción reubicada", "canciones reubicadas")}, con sus playlists");
            if (result.TemporariesRemoved > 0)
                notes.Add(Plural.Of(result.TemporariesRemoved, "temporal antiguo eliminado", "temporales antiguos eliminados"));
            if (notes.Count > 0)
                Status = string.Join("; ", notes);
        }
        catch (Exception ex)
        {
            Status = $"Error al escanear: {ex.Message}";
        }
    }

    // ---- Revisión automática de las carpetas agregadas (docs/specs/auto-refresh-folders.md) ----

    private readonly FolderRefreshPolicy _folderRefresh;
    private bool _libraryLoaded; // hasta cargar bien la biblioteca no se revisa (ni cuenta como revisión)
    private string? _startupNotice; // el aviso del arranque, para no taparlo con «N canciones nuevas»

    /// <summary>La ventana principal se activó: si toca, se revisan las carpetas (criterio 11).</summary>
    public void OnWindowActivated() => _ = RefreshFoldersAsync();

    private async Task RefreshFoldersAsync()
    {
        if (!_libraryLoaded || _writes.IsOpen || !_folderRefresh.TryStart(DateTime.UtcNow)) return;
        try
        {
            var result = await _library.RefreshFoldersAsync();
            if (result.Changed)
            {
                var shown = Status;
                ApplySnapshot(result.Snapshot); // escribe el estado general: el mensaje va después
                var notes = new List<string>();
                if (result.Added > 0)
                    notes.Add(Plural.Of(result.Added, "canción nueva", "canciones nuevas"));
                if (result.Relocated > 0)
                    notes.Add(Plural.Of(result.Relocated, "canción reubicada", "canciones reubicadas"));
                var note = string.Join("; ", notes);
                Status = _startupNotice is not null && shown == _startupNotice ? $"{_startupNotice} · {note}" : note;
            }
            await CheckAvailabilityAsync(); // lo borrado mientras tanto aparece como no disponible (criterio 12)
        }
        catch (Exception ex)
        {
            Status = $"No se pudieron revisar las carpetas: {ex.Message}";
        }
        finally
        {
            _folderRefresh.Finish(DateTime.UtcNow);
        }
    }

    private void ApplySnapshot(LibrarySnapshot snapshot)
    {
        _missingIds.IntersectWith(snapshot.Tracks.Select(t => t.Id));
        _allTracks = snapshot.Tracks
            .Select(t => new TrackItemViewModel(t, _covers)
            {
                IsAvailable = !_missingIds.Contains(t.Id),
                IsPlaying = CurrentTrack?.Track.Id == t.Id, // CurrentTrack sigue siendo la instancia anterior
            })
            .ToList();
        // La cola guardaba las instancias anteriores, con las etiquetas y la ruta de antes: se cambian por las
        // nuevas, por Id (docs/specs/audit-followup.md, criterio 2). Las que ya no están en la biblioteca se quedan.
        var fresh = new Dictionary<Guid, TrackItemViewModel>();
        foreach (var item in _allTracks) fresh.TryAdd(item.Track.Id, item);
        _queue.Replace(queued => fresh.GetValueOrDefault(queued.Track.Id, queued));
        MissingCount = _missingIds.Count;
        ApplyFilter();
    }

    partial void OnSearchTextChanged(string value)
    {
        _searchDebounce.Stop();
        _searchDebounce.Start();
    }

    private IReadOnlyList<TrackItemViewModel> CurrentSource() =>
        Playlists.CurrentPlaylist is { } playlist
            ? PlaylistManager.Resolve(playlist, _allTracks, i => i.Track.Id)
            : _allTracks;

    private void ApplyFilter()
    {
        var source = CurrentSource();
        var ids = TrackFilter.Filter(source.Select(i => i.Track), SearchText)
            .Select(t => t.Id)
            .ToHashSet();
        var visible = source.Where(i => ids.Contains(i.Track.Id)).ToList(); // conserva el orden de la fuente
        var playlist = Playlists.CurrentPlaylist;
        if (playlist is null)
        {
            // Solo se agrupa lo que se ve (criterio 3): cada cambio de vista o de álbum abierto pasa por aquí.
            if (ShowArtistPage)
            {
                visible = BuildArtistPage(visible);
            }
            else
            {
                _artistsBuiltFor = null; // las colecciones se reutilizan para otra cosa: al volver, se rehacen
                _albumsBuiltFor = null;
                Artists = new();
                Albums = ShowAlbums
                    ? new(LibraryGrouper.GroupAlbums(visible.Select(i => i.Track))
                        .Select(g => new AlbumItemViewModel(g, _covers)))
                    : new();
                if (_albumScope is { } album)
                    visible = InAlbumOrder(album, visible);
            }
        }

        // La lista se sustituye entera y la selección se iría con ella: se recupera por Id (criterio 2).
        var selectedId = SelectedTrack?.Track.Id;
        for (var i = 0; i < visible.Count; i++) visible[i].Number = i + 1; // columna «#»
        Tracks = new ObservableCollection<TrackItemViewModel>(visible);
        SelectedTrack = selectedId is { } id ? visible.FirstOrDefault(i => i.Track.Id == id) : null;
        var searching = !string.IsNullOrWhiteSpace(SearchText);
        ListHeading = playlist?.Name
            ?? (ShowBackBar ? _albumScope!.Album
                : Mode switch { BrowseMode.Albums => "Álbumes", BrowseMode.Artists => "Artistas", _ => "Canciones" });
        UpdateListSummary(source.Count, searching);
        Status = playlist is not null
            ? source.Count == 0 ? $"\"{playlist.Name}\" está vacía. Clic derecho en una canción para agregarla"
              : "Biblioteca al día."
            : _allTracks.Count == 0 ? "Agrega una carpeta para empezar"
            : MissingCount > 0
                ? $"Biblioteca al día. {Plural.Of(MissingCount, "canción no disponible", "canciones no disponibles")}."
                : "Biblioteca al día.";
        NotifyBrowse();
    }

    // Junto al título grande: «1.284 canciones · 3 no disponibles», «12 de 1.284 canciones», «8 álbumes»…
    private void UpdateListSummary(int sourceCount, bool searching)
    {
        var missing = MissingCount > 0 ? $" · {MissingCount.ToString("N0", CultureInfo.CurrentCulture)} no disponible{(MissingCount == 1 ? "" : "s")}" : "";
        ListSummary = Playlists.CurrentPlaylist is not null
            ? searching ? $"{Plural.Of(Tracks.Count, "canción", "canciones")} de {sourceCount.ToString("N0", CultureInfo.CurrentCulture)}"
              : Plural.Of(sourceCount, "canción", "canciones")
            : _allTracks.Count == 0 ? ""
            : ShowAlbums ? Plural.Of(Albums.Count, "álbum", "álbumes")
            : ShowBackBar ? Plural.Of(Tracks.Count, "canción", "canciones")
            : searching ? $"{Plural.Of(Tracks.Count, "canción", "canciones")} de {sourceCount.ToString("N0", CultureInfo.CurrentCulture)}"
            : Plural.Of(_allTracks.Count, "canción", "canciones") + missing;
    }

    // Las pistas de un álbum en su orden (disco, pista, título), no en el de la biblioteca.
    private static List<TrackItemViewModel> InAlbumOrder(AlbumGroup album, List<TrackItemViewModel> visible)
    {
        var byId = new Dictionary<Guid, TrackItemViewModel>();
        foreach (var item in visible) byId.TryAdd(item.Track.Id, item);
        return album.Tracks.Select(t => byId.GetValueOrDefault(t.Id)).OfType<TrackItemViewModel>().ToList();
    }

    // Página de artista (mockup 1c): artistas, álbumes del elegido y pistas del álbum elegido, con los separadores
    // de disco. Devuelve las pistas que se muestran.
    private List<TrackItemViewModel> BuildArtistPage(List<TrackItemViewModel> visible)
    {
        var source = (_allTracks, SearchText);
        if (_artistsBuiltFor is not { } built || !ReferenceEquals(built.Library, _allTracks) || built.Search != SearchText)
        {
            Artists = new(LibraryGrouper.GroupArtists(visible.Select(i => i.Track))
                .Select(g => new ArtistItemViewModel(g, _covers)));
            _artistsBuiltFor = source;
            _albumsBuiltFor = null;
        }

        // Sin artista elegido, la página abre en el de la canción que suena y en su álbum (mockup 1c).
        string? playingAlbumKey = null;
        if (_artistScope is null && CurrentTrack is { } playing)
        {
            _artistScope = playing.Track.AlbumOwner.Trim();
            playingAlbumKey = LibraryGrouper.AlbumKey(playing.Track);
        }

        var artist = Artists.FirstOrDefault(a => string.Equals(a.Name, _artistScope, StringComparison.OrdinalIgnoreCase))
                     ?? Artists.FirstOrDefault();
        _artistScope = artist?.Name;

        if (_albumsBuiltFor is null || !string.Equals(_albumsBuiltFor, artist?.Name, StringComparison.OrdinalIgnoreCase))
        {
            Albums = artist is null ? new() : new(artist.Group.Albums.Select(g => new AlbumItemViewModel(g, _covers)));
            _albumsBuiltFor = artist?.Name;
        }
        var albumKey = _albumScope?.Key ?? playingAlbumKey;
        var album = Albums.FirstOrDefault(a => a.Group.Key == albumKey) ?? Albums.FirstOrDefault();
        _albumScope = album?.Group;

        _syncingBrowse = true;
        try
        {
            SelectedArtist = artist;
            SelectedAlbum = album;
        }
        finally { _syncingBrowse = false; }

        ArtistPageSummary = artist?.Summary ?? "";
        AlbumSectionTitle = album?.Title ?? "";
        AlbumSectionYear = album is { Group.Year: > 0 } ? $"· {album.Group.Year}" : "";
        if (album is null) return new();

        var tracks = InAlbumOrder(album.Group, visible);
        var discs = tracks.Select(i => i.Track.DiscNumber).Where(d => d > 0).Distinct().ToList();
        AlbumSectionDisc = discs.Count > 0 ? $"DISCO {discs.Min()}" : "";
        for (var i = 0; i < tracks.Count; i++)
        {
            var disc = tracks[i].Track.DiscNumber;
            tracks[i].DiscHeader = i > 0 && disc > 0 && disc != tracks[i - 1].Track.DiscNumber ? $"DISCO {disc}" : null;
        }

        return tracks;
    }

    [RelayCommand]
    private async Task CheckAvailabilityAsync()
    {
        try
        {
            var missing = await _library.FindMissingAsync();
            _missingIds = missing.Select(t => t.Id).ToHashSet();
            foreach (var item in _allTracks)
                item.IsAvailable = !_missingIds.Contains(item.Track.Id);
            MissingCount = _missingIds.Count;
        }
        catch (Exception ex)
        {
            Status = $"No se pudo revisar la biblioteca: {ex.Message}";
        }
    }

    [RelayCommand]
    private void AskPrune() => IsConfirmingPrune = true;

    [RelayCommand]
    private void CancelPrune() => IsConfirmingPrune = false;

    [RelayCommand]
    private async Task ConfirmPruneAsync()
    {
        IsConfirmingPrune = false;
        try
        {
            // Si suena una pista que va a desaparecer de la biblioteca, se detiene (igual que al quitarla a mano).
            if (CurrentTrack is { } playing && _missingIds.Contains(playing.Track.Id))
                ClearCurrentTrack();

            var result = await _library.PruneMissingAsync(); // vuelve a comprobar el disco en este momento
            var removed = result.Removed.Select(t => t.Id).ToHashSet();
            _queue.RemoveWhere(q => removed.Contains(q.Track.Id));
            UpdateGapless();
            await Playlists.ForgetTracksAsync(removed);
            ApplySnapshot(result.Snapshot);

            // ApplySnapshot escribe el estado general: el mensaje de esta acción va después.
            Status = removed.Count == 0 ? "No había canciones no disponibles"
                : removed.Count == 1 ? "1 canción quitada de la biblioteca"
                : $"{removed.Count} canciones quitadas de la biblioteca";
        }
        catch (Exception ex)
        {
            Status = $"No se pudo limpiar la biblioteca: {ex.Message}";
        }
    }

    private void MarkAvailability(TrackItemViewModel item, bool available)
    {
        var changed = available ? _missingIds.Remove(item.Track.Id) : _missingIds.Add(item.Track.Id);
        if (!changed) return;
        item.IsAvailable = available;
        // La cola puede tener una instancia vieja (tras reescanear): se actualiza también la visible.
        if (_allTracks.FirstOrDefault(i => i.Track.Id == item.Track.Id) is { } shown)
            shown.IsAvailable = available;
        MissingCount = _missingIds.Count;
    }

    /// <summary>
    /// Para los movimientos de la cola que saltan pistas ausentes: comprueba el archivo y lo anota. Si no hay
    /// ninguna disponible, la cola no se mueve (docs/specs/playback-resilience.md, criterio 9).
    /// </summary>
    private bool CheckAvailable(TrackItemViewModel candidate)
    {
        var exists = File.Exists(candidate.Track.FilePath);
        MarkAvailability(candidate, exists);
        return exists;
    }

    private static string NotAvailable(TrackItemViewModel item) =>
        $"\"{item.Title}\" no está disponible: el archivo se movió o se borró";

    // ---- Eliminación y desvinculación ----

    [RelayCommand]
    private Task UnlinkTrackAsync(TrackItemViewModel? item) => RemoveTrackAsync(item, deleteFile: false);

    [RelayCommand]
    private void AskDeleteTrack(TrackItemViewModel? item) => TrackPendingDelete = item;

    [RelayCommand]
    private void CancelDeleteTrack() => TrackPendingDelete = null;

    [RelayCommand]
    private async Task ConfirmDeleteTrackAsync()
    {
        var item = TrackPendingDelete;
        TrackPendingDelete = null;
        await RemoveTrackAsync(item, deleteFile: true);
    }

    private async Task RemoveTrackAsync(TrackItemViewModel? item, bool deleteFile)
    {
        if (item is null) return;
        IDisposable? session = null;
        try
        {
            // Una sola regla: si suena la canción quitada, se detiene (no se reanuda al terminar). Antes de comparar,
            // ponerse al día: la pista pudo avanzar sola.
            CatchUpWithPlayer();
            if (CurrentTrack?.Track.Id == item.Track.Id)
                ClearCurrentTrack();

            // Como cualquier escritura en audio: la sesión suelta la pista preparada (en Windows no se puede borrar un
            // archivo abierto), impide volver a prepararla mientras se borra y, al cerrarse en el finally, prepara la
            // siguiente también si el guardado falla (docs/specs/remove-and-advance.md).
            session = _writes.Begin(new[] { item.Track.Id });

            var result = await _library.RemoveTracksAsync(new[] { item.Track.Id }, deleteFile);
            var removedIds = result.Removed.Select(t => t.Id).ToList();
            _queue.RemoveWhere(q => removedIds.Contains(q.Track.Id)); // la siguiente se prepara al cerrar la sesión
            await Playlists.ForgetTracksAsync(removedIds);
            ApplySnapshot(result.Snapshot);

            // ApplySnapshot escribe el estado general: el mensaje de esta acción va después.
            Status = result.Errors.Count > 0 ? $"No se pudo eliminar: {result.Errors[0]}"
                : deleteFile ? $"\"{item.Title}\" eliminada del disco"
                : $"\"{item.Title}\" quitada de la biblioteca";
        }
        catch (Exception ex)
        {
            // El guardado falló con el archivo ya borrado: la pista sigue en la biblioteca, pero no disponible
            // (docs/specs/library-data-safety-2.md, criterio 2).
            if (deleteFile && !File.Exists(item.Track.FilePath))
            {
                Status = $"Se borró el archivo, pero no se pudo guardar la biblioteca: {ex.Message}";
                _ = CheckAvailabilityAsync();
            }
            else
            {
                Status = $"No se pudo quitar la canción: {ex.Message}";
            }
        }
        finally
        {
            session?.Dispose();
        }
    }

    private void ClearCurrentTrack()
    {
        _coverCts?.Cancel(); // evita que una carátula en curso reaparezca
        _scrub.Abandon();
        Lyrics.Clear();
        _player.Unload();
        _epoch.Advance();
        SetPositionSilently(TimeSpan.Zero, duration: 1);

        PositionText = "0:00";
        DurationText = "0:00";
        RemainingText = "";
        CurrentTrack = null;
        NowPlaying = "Nada en reproducción";
        NowPlayingCover = null;
        IsImmersive = false;
    }

    // ---- Reproducción ----

    [RelayCommand]
    private void PlayTrack(TrackItemViewModel? item)
    {
        if (item is null) return;
        // La cola es lo que el usuario veía al elegir. Si la pista ya no está en la lista visible
        // (por ejemplo, tras cambiar de playlist), suena sola.
        var list = Tracks.ToList();
        var index = list.FindIndex(i => i.Track.Id == item.Track.Id);
        if (index < 0)
        {
            list = new List<TrackItemViewModel> { item };
            index = 0;
        }
        _queue.Start(list, index);
        StartPlayback(list[index]);
    }

    /// <summary>Carga y reproduce la pista. Devuelve false si no pudo: entonces no se tocó lo que sonaba.</summary>
    private bool StartPlayback(TrackItemViewModel item)
    {
        if (!File.Exists(item.Track.FilePath))
        {
            MarkAvailability(item, false);
            Status = NotAvailable(item);
            return false;
        }
        try
        {
            _player.Load(item.Track.FilePath);
            _epoch.Advance();
            MarkAvailability(item, true);
            ShowTrack(item);
            _player.Play();
            UpdateGapless();
            return true;
        }
        catch (Exception ex)
        {
            // Load ya soltó lo anterior: la interfaz no debe seguir diciendo que suena algo.
            ClearCurrentTrack();
            Status = ex.Message;
            return false;
        }
    }

    /// <summary>Actualiza lo que se ve (título, duración, letra, carátula) para la pista que suena.</summary>
    private void ShowTrack(TrackItemViewModel item)
    {
        _scrub.Abandon(); // un arrastre a medias no debe saltar en la pista nueva
        // Reiniciar posición ANTES de cambiar el máximo: si no, el Slider ajusta su
        // valor al nuevo rango y dispara un Seek no deseado.
        SetPositionSilently(TimeSpan.Zero, duration: Math.Max(_player.Duration.TotalSeconds, 1));

        DurationText = TimeFormatter.Format(_player.Duration);
        UpdateRemainingText(); // si la posición ya era 0, OnPositionSecondsChanged no se dispara
        CurrentTrack = item;
        SyncSelectionWith(item);
        _ = Lyrics.LoadAsync(item.Track);
        _ = LoadNowPlayingCoverAsync(item.Track);
        _settings.Update(s => s with { LastTrackId = item.Track.Id });
        NowPlaying = $"{item.Title} — {item.Artist}";
    }

    /// <summary>
    /// Lleva el resaltado de la lista a la pista que suena (Siguiente, Anterior, avance automático, doble clic).
    /// Se busca por Id: tras reescanear, la cola puede tener una instancia distinta a la que muestra la lista.
    /// Si la pista no está en la lista visible (otra playlist, filtro activo), no se toca la selección.
    /// </summary>
    private void SyncSelectionWith(TrackItemViewModel item)
    {
        var visible = Tracks.FirstOrDefault(i => i.Track.Id == item.Track.Id);
        if (visible is not null && !ReferenceEquals(SelectedTrack, visible))
            SelectedTrack = visible;
    }

    // El reproductor pasó solo a la pista preparada: aquí solo se pone al día la cola y lo que se ve.
    private void OnAdvancedToNext(string path)
    {
        var next = _queue.MoveNextAfterEnd();
        if (next is null) return;

        if (!string.Equals(next.Track.FilePath, path, StringComparison.OrdinalIgnoreCase))
        {
            // La cola cambió justo antes del salto y lo que suena ya no coincide: se reinicia con lo correcto.
            if (StartPlayback(next)) return;

            // Su archivo ya no está (StartPlayback lo anotó en la línea de estado). Se busca la siguiente disponible,
            // como al terminar una pista; si no hay, se detiene: lo que suena no es lo que dice la cola ni lo que se
            // muestra (docs/specs/remove-and-advance.md).
            if (_queue.MoveNextAfterEnd(CheckAvailable) is { } other && StartPlayback(other)) return;
            if (CurrentTrack is not null) ClearCurrentTrack();
            return;
        }

        if (next.Track.Id == CurrentTrack?.Track.Id)
        {
            // "Repetir una": misma letra y carátula, solo vuelve el contador al principio. Por Id, como en OnTrackEnded.
            SetPositionSilently(TimeSpan.Zero);
        }
        else
        {
            ShowTrack(next);
        }

        UpdateGapless();
    }

    /// <summary>
    /// Antes de decidir si un archivo está abierto: el reproductor pudo pasar solo a la pista siguiente y su aviso
    /// seguir en cola. Entonces <see cref="CurrentTrack"/> es todavía la anterior, y compararla dejaría abierto el
    /// archivo que se va a escribir o borrar (docs/specs/write-after-advance.md). El avance se atiende aquí y el
    /// aviso en cola se descarta. Solo si el reproductor está en la pista que la cola da por siguiente: una ruta
    /// distinta por otro motivo (una pista reubicada) no es un avance.
    /// </summary>
    private void CatchUpWithPlayer()
    {
        if (CurrentTrack is not { } shown || _player.CurrentFilePath is not { } path
            || FilePaths.Comparer.Equals(path, shown.Track.FilePath)
            || !FilePaths.Comparer.Equals(path, _queue.PeekNextAfterEnd()?.Track.FilePath))
            return;

        _epoch.Advance(); // el aviso que venía de camino ya no vale: se atiende ahora
        OnAdvancedToNext(path);
    }

    /// <summary>
    /// Le dice al reproductor cuál es la pista que sigue para que empiece sin silencio. Mientras hay una escritura
    /// en curso no se prepara nada: abriría un archivo que alguien puede estar escribiendo. Al cerrarse la última
    /// sesión, <see cref="ResumeInterrupted"/> vuelve a llamar aquí.
    /// </summary>
    private void UpdateGapless()
    {
        if (_writes.IsOpen) return;
        _player.PrepareNext(CurrentTrack is null ? null : _queue.PeekNextAfterEnd()?.Track.FilePath);
    }

    private async Task LoadNowPlayingCoverAsync(Track track)
    {
        _coverCts?.Cancel();
        var cts = _coverCts = new CancellationTokenSource();
        try
        {
            var cover = await _covers.LoadLargeAsync(track, cts.Token);
            if (!cts.IsCancellationRequested)
                NowPlayingCover = cover; // null si la pista no tiene carátula
            else
                cover?.Dispose(); // se cambió de pista: nadie la ha recibido (LoadLargeAsync no la guarda en caché)
        }
        catch (OperationCanceledException)
        {
            // Se cambió de canción antes de terminar.
        }
        catch (Exception)
        {
            // La carátula es decorativa, pero no debe quedarse la de la pista anterior (criterio 8).
            if (!cts.IsCancellationRequested)
                NowPlayingCover = null;
        }
    }

    /// <summary>
    /// «Incrustar traducción…» e «Incrustar pronunciación…» del menú de capas del inmersivo (docs/specs/lyrics-layers.md,
    /// criterio 12b): abre el selector y, al elegir, escribe solo esa capa. Escribir pasa por <see cref="OnWriting"/>,
    /// que descarga la pista y cierra el inmersivo: después se vuelve a cargar en el mismo punto, se reabre el
    /// inmersivo y sigue sonando si sonaba. Cancelar el selector no toca la reproducción.
    /// </summary>
    public async Task EmbedLayerInImmersiveAsync(LyricsLayerKind kind)
    {
        if (CurrentTrack is not { } item) return;

        // Mientras se elige el archivo la música sigue. Al escribir, OnWriting anota la posición y
        // ResumeInterrupted (al cerrarse la sesión) vuelve a cargar la pista.
        await EmbedLyrics.EmbedLayerAsync(item.Track, kind, Lyrics.CurrentBlock);
    }

    // Lo que sonaba cuando una escritura obligó a soltar el archivo (docs/specs/playback-resilience.md, criterio 5).
    private sealed record Interrupted(TrackItemViewModel Item, TimeSpan Position, bool WasPlaying, bool WasImmersive);
    private Interrupted? _interrupted;
    private readonly WriteSessions _writes;

    /// <summary>Descarga la pista en curso para poder escribir en su archivo, anotando cómo volver a ella.</summary>
    private void InterruptForWriting(TrackItemViewModel item)
    {
        _interrupted = new Interrupted(
            item, _player.CurrentTime, _player.State == PlaybackState.Playing, IsImmersive);
        ClearCurrentTrack();
    }

    /// <summary>
    /// La escritura terminó, bien o mal: la pista vuelve al mismo punto, sonando o en pausa como estaba. Si el
    /// usuario eligió otra mientras tanto, gana lo último que pidió.
    /// </summary>
    private void ResumeInterrupted()
    {
        // Al abrir la sesión, OnWriting soltó también la pista preparada. Si no hay nada que reanudar (se escribió en otra pista, o el
        // usuario ya eligió otra), se vuelve a preparar la siguiente para no perder el empalme sin silencio
        // (docs/specs/audit-followup.md, criterio 3).
        var was = _interrupted;
        _interrupted = null;
        if (was is null || CurrentTrack is not null)
        {
            UpdateGapless();
            return;
        }

        // Tras guardar, la biblioteca tiene una instancia nueva con las etiquetas ya cambiadas.
        var item = _allTracks.FirstOrDefault(i => i.Track.Id == was.Item.Track.Id) ?? was.Item;
        ResumePlayback(item, was.Position, was.WasPlaying, was.WasImmersive);
    }

    // Como StartPlayback, pero en una posición y sin arrancar si estaba en pausa. Vale para la misma pista recién
    // escrita: la cola no se tocó al descargarla.
    private void ResumePlayback(TrackItemViewModel item, TimeSpan position, bool play, bool immersive)
    {
        if (!File.Exists(item.Track.FilePath))
        {
            MarkAvailability(item, false);
            Status = NotAvailable(item);
            return;
        }
        try
        {
            _player.Load(item.Track.FilePath);
            _epoch.Advance();
            ShowTrack(item);
            // Con SeekTo y no con Seek a secas: en pausa, la barra, el tiempo y la letra quedarían en 0:00 y ← →
            // saltarían desde el principio (docs/specs/audit-followup.md, criterio 1).
            if (position > TimeSpan.Zero && position < _player.Duration)
                SeekTo(position);
            if (play) _player.Play();
            UpdateGapless();
            IsImmersive = immersive;
        }
        catch (Exception ex)
        {
            ClearCurrentTrack();
            Status = ex.Message;
        }
    }

    [RelayCommand]
    private void OpenImmersive()
    {
        if (CurrentTrack is not null)
            IsImmersive = true;
    }

    [RelayCommand]
    private void CloseImmersive() => IsImmersive = false;

    [RelayCommand]
    private void TogglePlayPause()
    {
        switch (_player.State)
        {
            case PlaybackState.Playing:
                _player.Pause();
                break;
            case PlaybackState.Paused:
                _player.Play();
                break;
            default:
                if (CurrentTrack is { } current)
                {
                    TryPlay(current);
                    break;
                }
                var target = SelectedTrack ?? Tracks.FirstOrDefault();
                if (target is not null) PlayTrack(target);
                break;
        }
    }

    /// <summary>
    /// Arranca lo que el reproductor tiene cargado. Si la pista terminó sola, <c>Play</c> vuelve a abrir el archivo
    /// y lanza cuando ya no está: aquí se avisa en lugar de dejar caer la app (docs/specs/playback-resilience.md,
    /// criterio 1). No recarga por su cuenta, para no perder la posición de una pista cargada en pausa.
    /// </summary>
    private void TryPlay(TrackItemViewModel item)
    {
        try
        {
            _player.Play();
        }
        catch (Exception ex)
        {
            var exists = File.Exists(item.Track.FilePath);
            if (!exists) MarkAvailability(item, false);
            Status = exists ? ex.Message : NotAvailable(item);
        }
    }

    [RelayCommand]
    private void Next()
    {
        if (_queue.MoveNext(CheckAvailable) is { } next)
            StartPlayback(next);
    }

    [RelayCommand]
    private void Previous()
    {
        // Convención habitual: pasados 3 s, "anterior" reinicia la canción actual.
        if (_player.CurrentTime > TimeSpan.FromSeconds(3)
            || _queue.MovePrevious(CheckAvailable) is not { } previous)
        {
            SeekTo(TimeSpan.Zero);
            return;
        }
        StartPlayback(previous);
    }

    [RelayCommand]
    private void PlayNext(TrackItemViewModel? item)
    {
        if (item is null) return;
        // Sin nada sonando, "siguiente" equivale a reproducirla.
        if (CurrentTrack is null)
        {
            PlayTrack(item);
            return;
        }
        _queue.PlayNext(item);
        UpdateGapless();
        Status = $"\"{item.Title}\" sonará a continuación";
    }

    [RelayCommand]
    private void AddToQueue(TrackItemViewModel? item)
    {
        if (item is null) return;
        if (CurrentTrack is null)
        {
            PlayTrack(item);
            return;
        }
        _queue.Enqueue(item);
        UpdateGapless();
        Status = $"\"{item.Title}\" agregada al final de la cola";
    }

    private void OnTrackEnded()
    {
        if (_queue.MoveNextAfterEnd(CheckAvailable) is not { } next) return;
        if (next.Track.Id == CurrentTrack?.Track.Id)
        {
            // "Repetir una": reinicia sin recargar la letra ni la carátula. Se compara por Id: tras editar la
            // pista que suena, la cola guarda la instancia anterior.
            _player.Seek(TimeSpan.Zero);
            TryPlay(next);
        }
        else
        {
            StartPlayback(next);
        }
    }

    // El archivo debe quedar libre: en Windows no se puede escribir en uno que el reproductor tiene abierto.
    private void OnWriting(IReadOnlySet<Guid> ids)
    {
        CatchUpWithPlayer(); // antes de soltar la preparada: ponerse al día vuelve a preparar la siguiente
        _player.PrepareNext(null);
        if (CurrentTrack is { } current && ids.Contains(current.Track.Id))
            InterruptForWriting(current);
    }

    private void OnTagsSaved(LibrarySnapshot snapshot)
    {
        // El álbum o artista abierto se conserva; si cambió de nombre, se sigue a lo que se veía
        // (docs/specs/keep-browse-scope.md). El grupo se rehace con la biblioteca nueva: el viejo tiene los datos de antes.
        var shown = Tracks.Select(i => i.Track.Id).ToList();
        var (artist, albumKey) = BrowseScope.Resolve(_artistScope, _albumScope?.Key, shown, snapshot.Tracks);
        _artistScope = artist;
        _albumScope = albumKey is null ? null
            : LibraryGrouper.GroupAlbums(snapshot.Tracks.Where(t => LibraryGrouper.AlbumKey(t) == albumKey))
                .FirstOrDefault();
        ApplySnapshot(snapshot);
        UpdateGapless();
    }

    // Revisa lo que se ve: un álbum o artista abierto, una playlist o la biblioteca filtrada.
    [RelayCommand]
    private void OpenAutoFix() =>
        AutoFix.Begin(Tracks.Where(t => t.IsAvailable).Select(t => t.Track).ToList());

    [RelayCommand]
    private void OpenConvert() =>
        Conversion.Begin(Tracks.Where(t => t.IsAvailable).Select(t => t.Track).ToList(), "");

    [RelayCommand]
    private void CycleRepeat() => Repeat = Repeat switch
    {
        RepeatMode.Off => RepeatMode.All,
        RepeatMode.All => RepeatMode.One,
        _ => RepeatMode.Off
    };

    partial void OnIsShuffleOnChanged(bool value)
    {
        _queue.Shuffle = value;
        UpdateGapless();
        if (!_restoring)
            _settings.Update(s => s with { Shuffle = value });
    }

    [RelayCommand]
    private void ToggleIsland() => ShowIsland = !ShowIsland;

    /// <summary>
    /// La ventana principal está oculta en la bandeja (docs/specs/background-playback.md). Lo fija App al ocultarla
    /// o mostrarla; la isla lo usa para enseñar su botón «Salir».
    /// </summary>
    [ObservableProperty] private bool _isMainWindowHidden;

    /// <summary>Alguien pidió salir de verdad (botón «Salir» de la isla): App termina la app por el camino normal.</summary>
    public event Action? ExitRequested;

    [RelayCommand]
    private void Exit() => ExitRequested?.Invoke();

    /// <summary>Botón «Ocultar isla» de la propia isla: la música sigue y la línea de estado dice dónde reactivarla.</summary>
    [RelayCommand]
    private void HideIsland()
    {
        ShowIsland = false;
        Status = "Isla oculta. Se activa en Configuración o en la barra de reproducción";
    }

    partial void OnShowIslandChanged(bool value)
    {
        if (!_restoring)
            _settings.Update(s => s with { ShowIsland = value });
    }

    partial void OnRepeatChanged(RepeatMode value)
    {
        _queue.Repeat = value;
        UpdateGapless();
        if (!_restoring)
            _settings.Update(s => s with { Repeat = value });
    }

    // ---- Barra de progreso y volumen ----

    // Arrastre de la barra de progreso (docs/specs/ui-responsiveness.md, criterios 4 y 5).
    private readonly SeekScrub _scrub = new();

    /// <summary>El puntero se pulsó sobre una barra de progreso: hasta soltar no se salta ni se mueve el pulgar.</summary>
    public void BeginScrub() => _scrub.Begin();

    /// <summary>El puntero se soltó: el único salto del arrastre.</summary>
    public void EndScrub()
    {
        if (_scrub.End() is { } target)
            _player.Seek(target);
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_player.State != PlaybackState.Playing || !_scrub.TimerMayWrite) return;

        SetPositionSilently(_player.CurrentTime);
    }

    /// <summary>
    /// Mueve la barra, el tiempo y la letra sin saltar en el reproductor. Con <paramref name="duration"/>, cambia
    /// también el máximo de la barra dentro de la misma guarda: si no, el Slider ajusta su valor al rango nuevo y
    /// dispara un salto no deseado.
    /// </summary>
    private void SetPositionSilently(TimeSpan position, double? duration = null)
    {
        _isUpdatingFromTimer = true;
        try
        {
            PositionSeconds = position.TotalSeconds;
            if (duration is { } seconds) DurationSeconds = seconds;
        }
        finally { _isUpdatingFromTimer = false; }
    }

    /// <summary>
    /// Salta a <paramref name="position"/> y pone al día lo que se ve. Es el camino de todo salto que no nace de
    /// la barra: en pausa el temporizador no escribe, y un <c>Seek</c> a secas dejaría la barra, el tiempo y el
    /// punto de partida de ← → donde estaban (docs/specs/single-seek-path.md).
    /// </summary>
    private void SeekTo(TimeSpan position)
    {
        // Acotada como en el reproductor: una línea de la letra más allá del final (un .lrc de otra versión) no
        // debe dejar la barra fuera de su rango.
        var duration = _player.Duration;
        position = position < TimeSpan.Zero ? TimeSpan.Zero : position > duration ? duration : position;
        _player.Seek(position);
        SetPositionSilently(position);
    }

    // Método parcial generado por CommunityToolkit: se ejecuta al cambiar PositionSeconds.
    partial void OnPositionSecondsChanged(double value)
    {
        Lyrics.UpdatePosition(TimeSpan.FromSeconds(value));

        PositionText = TimeFormatter.Format(TimeSpan.FromSeconds(value));
        UpdateRemainingText();

        // Si el cambio vino del usuario, saltar a ese punto; durante un arrastre, solo al soltar (EndScrub).
        if (!_isUpdatingFromTimer && _scrub.Change(TimeSpan.FromSeconds(value)) is { } target)
            _player.Seek(target);
    }

    // Silencio (mute): un clic deja el volumen en 0 y otro clic devuelve el nivel anterior
    private double _volumeBeforeMute = 1.0; // último volumen distinto de cero

    public bool IsMuted => Volume <= 0;
    public string MuteText => IsMuted ? "Desactivar silenciar" : "Silenciar";

    [RelayCommand]
    private void ToggleMute() => Volume = IsMuted ? _volumeBeforeMute : 0;

    // Atajos de teclado: direction es +1 (adelante / subir) o -1 (atrás / bajar).
    public void SeekBy(int direction)
    {
        var position = TimeSpan.FromSeconds(PositionSeconds);
        if (PlaybackShortcuts.SeekTarget(position, _player.Duration, direction) is { } target)
            PositionSeconds = target.TotalSeconds; // fuera del temporizador: OnPositionSecondsChanged hace el Seek
    }

    /// <summary>
    /// Clic en la barra de progreso de la isla (0 = inicio, 1 = final). Asigna PositionSeconds una sola vez y
    /// fuera del temporizador, como SeekBy: OnPositionSecondsChanged hace un único Seek. La barra de la isla
    /// solo lee PositionSeconds (OneWay), así que refrescarla nunca vuelve a llamar a Seek.
    /// </summary>
    public void SeekToFraction(double fraction)
    {
        if (CurrentTrack is null) return;
        if (PlaybackShortcuts.TargetAtFraction(fraction, _player.Duration) is { } target)
            PositionSeconds = target.TotalSeconds;
    }

    public void ChangeVolume(int direction) =>
        Volume = PlaybackShortcuts.StepVolume(Volume, direction);

    partial void OnVolumeChanged(double value)
    {
        if (value > 0) _volumeBeforeMute = value;
        OnPropertyChanged(nameof(IsMuted));
        OnPropertyChanged(nameof(MuteText));
        _player.Volume = value;
        if (!_restoring)
            _settings.Update(s => s with { Volume = value });
    }
}