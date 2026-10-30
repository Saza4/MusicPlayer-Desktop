using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    private CancellationTokenSource? _coverCts;

    [ObservableProperty] private Bitmap? _nowPlayingCover;
    [ObservableProperty] private bool _isImmersive;
    [ObservableProperty] private double _volume = 1.0;

    [ObservableProperty] private bool _isShuffleOn;

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
    }

    // ---- Navegación por álbum y artista ----
    [ObservableProperty] private int _browseIndex; // 0 Canciones, 1 Álbumes, 2 Artistas
    [ObservableProperty] private ObservableCollection<AlbumItemViewModel> _albums = new();
    [ObservableProperty] private ObservableCollection<ArtistItemViewModel> _artists = new();
    private string? _artistScope;     // artista abierto
    private AlbumGroup? _albumScope;  // álbum abierto
    private BrowseMode Mode => (BrowseMode)Math.Max(0, BrowseIndex);
    private bool InLibrary => Playlists.CurrentPlaylist is null;
    public bool ShowModeTabs => InLibrary;
    public bool ShowArtists => InLibrary && Mode == BrowseMode.Artists && _artistScope is null;
    public bool ShowAlbums => InLibrary && _albumScope is null
        && (Mode == BrowseMode.Albums || (Mode == BrowseMode.Artists && _artistScope is not null));
    public bool ShowSongs => !ShowArtists && !ShowAlbums;
    public bool ShowBackBar => InLibrary && (_artistScope is not null || _albumScope is not null);
    public string BackText => _albumScope is null ? "← Artistas"
        : _artistScope is { } artist ? $"← {artist}"
        : "← Álbumes";
    public string BrowseTitle => _albumScope is { } album ? $"{album.Album} — {album.Artist}" : _artistScope ?? "";

    partial void OnBrowseIndexChanged(int value)
    {
        _artistScope = null;
        _albumScope = null;
        ApplyFilter();
    }

    private void NotifyBrowse()
    {
        OnPropertyChanged(nameof(ShowModeTabs));
        OnPropertyChanged(nameof(ShowArtists));
        OnPropertyChanged(nameof(ShowAlbums));
        OnPropertyChanged(nameof(ShowSongs));
        OnPropertyChanged(nameof(ShowBackBar));
        OnPropertyChanged(nameof(BackText));
        OnPropertyChanged(nameof(BrowseTitle));
    }

    [RelayCommand]
    private void OpenArtist(ArtistItemViewModel? artist)
    {
        if (artist is null) return;
        _artistScope = artist.Name;
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
        if (_albumScope is not null) _albumScope = null;
        else _artistScope = null;
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

    private IReadOnlyList<TrackItemViewModel> _allTracks = Array.Empty<TrackItemViewModel>();
    private readonly PlayQueue<TrackItemViewModel> _queue = new();
    private readonly DispatcherTimer _searchDebounce;
    [ObservableProperty] private string _searchText = "";

    // Evita el bucle timer -> Slider -> Seek -> timer.
    private bool _isUpdatingFromTimer;

    [ObservableProperty] private ObservableCollection<TrackItemViewModel> _tracks = new();
    [ObservableProperty] private TrackItemViewModel? _selectedTrack;
    [ObservableProperty] private TrackItemViewModel? _currentTrack;

    [ObservableProperty] private string _status = "Agrega una carpeta para empezar";
    [ObservableProperty] private string _nowPlaying = "Nada en reproducción";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayPauseText))]
    private bool _isPlaying;

    [ObservableProperty] private double _positionSeconds;
    [ObservableProperty] private double _durationSeconds = 1; // Slider.Maximum debe ser > 0
    [ObservableProperty] private string _positionText = "0:00";
    [ObservableProperty] private string _durationText = "0:00";

    public string PlayPauseText => IsPlaying ? "Pausa" : "Reproducir";

    public MainViewModel(IAudioPlayer player, LibraryService library,
                         IFolderPickerService folderPicker, LyricsViewModel lyrics,
                         CoverArtLoader covers, PlaylistViewModel playlists,
                         SettingsService settings, TagEditorViewModel tagEditor)
    {
        _covers = covers;
        _settings = settings;
        Lyrics = lyrics;
        Playlists = playlists;
        TagEditor = tagEditor;
        _player = player;
        _library = library;
        _folderPicker = folderPicker;

        // BASS dispara estos eventos desde su propio hilo: hay que volver al hilo de la UI.
        _player.StateChanged += (_, state) =>
            Dispatcher.UIThread.Post(() => IsPlaying = state == PlaybackState.Playing);
        _player.TrackEnded += (_, _) =>
            Dispatcher.UIThread.Post(() => OnTrackEnded());
        _player.AdvancedToNext += (_, path) =>
            Dispatcher.UIThread.Post(() => OnAdvancedToNext(path));

        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(250), DispatcherPriority.Background, OnTick);
        _timer.Start();

        // Espera 200 ms tras la última tecla para no refiltrar en cada pulsación.
        _searchDebounce = new DispatcherTimer(TimeSpan.FromMilliseconds(200), DispatcherPriority.Normal,
            (_, _) => { _searchDebounce.Stop(); ApplyFilter(); });

        Playlists.Changed += (_, _) => ApplyFilter();
        Playlists.Notified += message => Status = message;
        TagEditor.Saving += OnTagsSaving;
        TagEditor.Saved += OnTagsSaved;
        TagEditor.Notified += message => Status = message;
    }

    // ---- Biblioteca ----

    public async Task InitializeAsync()
    {
        try
        {
            Status = "Cargando…";
            var saved = await _settings.LoadAsync();
            _restoring = true;
            try
            {
                Volume = Math.Clamp(saved.Volume, 0.0, 1.0);
                IsShuffleOn = saved.Shuffle;
                Repeat = saved.Repeat;
                Lyrics.Restore(saved);
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
        }
        catch (Exception ex)
        {
            Status = $"No se pudo cargar la biblioteca: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task AddFolderAsync()
    {
        var folder = await _folderPicker.PickFolderAsync();
        if (folder is null) return;

        try
        {
            Status = "Escaneando…";
            var progress = new Progress<int>(count => Status = $"Escaneando… {count} canciones");
            var result = await _library.ScanFolderAsync(folder, progress);
            ApplySnapshot(result.Snapshot);
            _ = CheckAvailabilityAsync();
            if (result.Relocated > 0) // ApplySnapshot escribe el estado general: este mensaje va después
                Status = $"{Plural.Of(result.Relocated, "canción reubicada", "canciones reubicadas")}, con sus playlists";
        }
        catch (Exception ex)
        {
            Status = $"Error al escanear: {ex.Message}";
        }
    }

    private void ApplySnapshot(LibrarySnapshot snapshot)
    {
        _missingIds.IntersectWith(snapshot.Tracks.Select(t => t.Id));
        _allTracks = snapshot.Tracks
            .Select(t => new TrackItemViewModel(t, _covers) { IsAvailable = !_missingIds.Contains(t.Id) })
            .ToList();
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
            Artists = new(LibraryGrouper.GroupArtists(visible.Select(i => i.Track))
                .Select(g => new ArtistItemViewModel(g, _covers)));
            if (_artistScope is { } artist)
                visible = visible.Where(i =>
                    string.Equals(i.Track.AlbumOwner.Trim(), artist, StringComparison.OrdinalIgnoreCase)).ToList();
            Albums = new(LibraryGrouper.GroupAlbums(visible.Select(i => i.Track))
                .Select(g => new AlbumItemViewModel(g, _covers)));
            if (_albumScope is { } album)
                visible = visible.Where(i => LibraryGrouper.AlbumKey(i.Track) == album.Key).ToList();
        }
        Tracks = new ObservableCollection<TrackItemViewModel>(visible);
        var searching = !string.IsNullOrWhiteSpace(SearchText);
        Status = playlist is not null
            ? source.Count == 0 ? $"\"{playlist.Name}\" está vacía. Clic derecho en una canción para agregarla"
              : searching ? $"{Tracks.Count} de {source.Count} canciones"
              : $"{playlist.Name}: {source.Count} canciones"
            : _allTracks.Count == 0 ? "Agrega una carpeta para empezar"
            : ShowArtists ? Plural.Of(Artists.Count, "artista", "artistas")
            : ShowAlbums ? Plural.Of(Albums.Count, "álbum", "álbumes")
            : _albumScope is not null || _artistScope is not null ? Plural.Of(Tracks.Count, "canción", "canciones")
            : searching ? $"{Tracks.Count} de {source.Count} canciones"
            : $"{_allTracks.Count} canciones en la biblioteca";
        NotifyBrowse();
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

    /// <summary>Avanza la cola con <paramref name="advance"/> saltando las pistas cuyo archivo ya no existe.</summary>
    private TrackItemViewModel? MoveToAvailable(Func<TrackItemViewModel?> advance)
    {
        // Tope de una vuelta completa: sin él, "Repetir todas" con todo ausente no terminaría nunca.
        for (var attempts = 0; attempts <= _queue.Count; attempts++)
        {
            if (advance() is not { } candidate) return null;
            var exists = File.Exists(candidate.Track.FilePath);
            MarkAvailability(candidate, exists);
            if (exists) return candidate;
        }
        return null;
    }

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
        try
        {
            _player.PrepareNext(null); // suelta el archivo preparado: en Windows no se puede borrar un archivo abierto

            // Una sola regla: si suena la canción quitada, se detiene. Además, en Windows
            // el archivo debe soltarse antes de poder borrarlo.
            if (CurrentTrack?.Track.Id == item.Track.Id)
                ClearCurrentTrack();

            var result = await _library.RemoveTracksAsync(new[] { item.Track.Id }, deleteFile);
            var removedIds = result.Removed.Select(t => t.Id).ToList();
            _queue.RemoveWhere(q => removedIds.Contains(q.Track.Id));
            UpdateGapless();
            await Playlists.ForgetTracksAsync(removedIds);
            ApplySnapshot(result.Snapshot);

            // ApplySnapshot escribe el estado general: el mensaje de esta acción va después.
            Status = result.Errors.Count > 0 ? $"No se pudo eliminar: {result.Errors[0]}"
                : deleteFile ? $"\"{item.Title}\" eliminada del disco"
                : $"\"{item.Title}\" quitada de la biblioteca";
        }
        catch (Exception ex)
        {
            Status = $"No se pudo quitar la canción: {ex.Message}";
        }
    }

    private void ClearCurrentTrack()
    {
        _coverCts?.Cancel(); // evita que una carátula en curso reaparezca
        Lyrics.Clear();
        _player.Unload();
        _isUpdatingFromTimer = true;
        try
        {
            PositionSeconds = 0;
            DurationSeconds = 1;
        }
        finally { _isUpdatingFromTimer = false; }

        PositionText = "0:00";
        DurationText = "0:00";
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

    private void StartPlayback(TrackItemViewModel item)
    {
        if (!File.Exists(item.Track.FilePath))
        {
            MarkAvailability(item, false);
            Status = $"\"{item.Title}\" no está disponible: el archivo se movió o se borró";
            return;
        }
        try
        {
            _player.Load(item.Track.FilePath);
            MarkAvailability(item, true);
            ShowTrack(item);
            _player.Play();
            UpdateGapless();
        }
        catch (Exception ex)
        {
            // Load ya soltó lo anterior: la interfaz no debe seguir diciendo que suena algo.
            ClearCurrentTrack();
            Status = ex.Message;
        }
    }

    /// <summary>Actualiza lo que se ve (título, duración, letra, carátula) para la pista que suena.</summary>
    private void ShowTrack(TrackItemViewModel item)
    {
        // Reiniciar posición ANTES de cambiar el máximo: si no, el Slider ajusta su
        // valor al nuevo rango y dispara un Seek no deseado.
        _isUpdatingFromTimer = true;
        try
        {
            PositionSeconds = 0;
            DurationSeconds = Math.Max(_player.Duration.TotalSeconds, 1);
        }
        finally { _isUpdatingFromTimer = false; }

        DurationText = TimeFormatter.Format(_player.Duration);
        CurrentTrack = item;
        _ = Lyrics.LoadAsync(item.Track);
        _ = LoadNowPlayingCoverAsync(item.Track);
        _settings.Update(s => s with { LastTrackId = item.Track.Id });
        NowPlaying = $"{item.Title} — {item.Artist}";
    }

    // El reproductor pasó solo a la pista preparada: aquí solo se pone al día la cola y lo que se ve.
    private void OnAdvancedToNext(string path)
    {
        var next = _queue.MoveNextAfterEnd();
        if (next is null) return;

        if (!string.Equals(next.Track.FilePath, path, StringComparison.OrdinalIgnoreCase))
        {
            // La cola cambió justo antes del salto y lo que suena ya no coincide: se reinicia con lo correcto.
            StartPlayback(next);
            return;
        }

        if (ReferenceEquals(next, CurrentTrack))
        {
            // "Repetir una": misma letra y carátula, solo vuelve el contador al principio.
            _isUpdatingFromTimer = true;
            try { PositionSeconds = 0; }
            finally { _isUpdatingFromTimer = false; }
        }
        else
        {
            ShowTrack(next);
        }

        UpdateGapless();
    }

    /// <summary>Le dice al reproductor cuál es la pista que sigue para que empiece sin silencio.</summary>
    private void UpdateGapless() =>
        _player.PrepareNext(CurrentTrack is null ? null : _queue.PeekNextAfterEnd()?.Track.FilePath);

    private async Task LoadNowPlayingCoverAsync(Track track)
    {
        _coverCts?.Cancel();
        var cts = _coverCts = new CancellationTokenSource();
        try
        {
            var cover = await _covers.LoadLargeAsync(track, cts.Token);
            if (!cts.IsCancellationRequested)
                NowPlayingCover = cover; // null si la pista no tiene carátula
        }
        catch (OperationCanceledException)
        {
            // Se cambió de canción antes de terminar.
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
                if (CurrentTrack is not null)
                {
                    _player.Play();
                    break;
                }
                var target = SelectedTrack ?? Tracks.FirstOrDefault();
                if (target is not null) PlayTrack(target);
                break;
        }
    }

    [RelayCommand]
    private void Next()
    {
        if (MoveToAvailable(_queue.MoveNext) is { } next)
            StartPlayback(next);
    }

    [RelayCommand]
    private void Previous()
    {
        // Convención habitual: pasados 3 s, "anterior" reinicia la canción actual.
        if (_player.CurrentTime > TimeSpan.FromSeconds(3)
            || MoveToAvailable(_queue.MovePrevious) is not { } previous)
        {
            _player.Seek(TimeSpan.Zero);
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
        if (MoveToAvailable(_queue.MoveNextAfterEnd) is not { } next) return;
        if (ReferenceEquals(next, CurrentTrack))
        {
            // "Repetir una": reinicia sin recargar el archivo, la letra ni la carátula.
            _player.Seek(TimeSpan.Zero);
            _player.Play();
        }
        else
        {
            StartPlayback(next);
        }
    }

    // El archivo debe quedar libre: en Windows no se puede escribir en uno que el reproductor tiene abierto.
    private void OnTagsSaving(Track track)
    {
        _player.PrepareNext(null);
        if (CurrentTrack?.Track.Id == track.Id)
            ClearCurrentTrack();
    }

    private void OnTagsSaved(LibrarySnapshot snapshot)
    {
        // El álbum o artista abierto pudo cambiar de clave: se vuelve a la vista general.
        _artistScope = null;
        _albumScope = null;
        ApplySnapshot(snapshot);
        UpdateGapless();
    }

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

    partial void OnRepeatChanged(RepeatMode value)
    {
        _queue.Repeat = value;
        UpdateGapless();
        if (!_restoring)
            _settings.Update(s => s with { Repeat = value });
    }

    // ---- Barra de progreso y volumen ----

    private void OnTick(object? sender, EventArgs e)
    {
        if (_player.State != PlaybackState.Playing) return;

        _isUpdatingFromTimer = true;
        try { PositionSeconds = _player.CurrentTime.TotalSeconds; }
        finally { _isUpdatingFromTimer = false; }
    }

    // Método parcial generado por CommunityToolkit: se ejecuta al cambiar PositionSeconds.
    partial void OnPositionSecondsChanged(double value)
    {
        Lyrics.UpdatePosition(TimeSpan.FromSeconds(value));

        PositionText = TimeFormatter.Format(TimeSpan.FromSeconds(value));

        // Si el cambio vino del usuario (arrastrando el Slider), saltar a ese punto.
        if (!_isUpdatingFromTimer)
            _player.Seek(TimeSpan.FromSeconds(value));
    }

    partial void OnVolumeChanged(double value)
    {
        _player.Volume = value;
        if (!_restoring)
            _settings.Update(s => s with { Volume = value });
    }
}