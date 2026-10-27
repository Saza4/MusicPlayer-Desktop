using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

    public LyricsViewModel Lyrics { get; }
    public PlaylistViewModel Playlists { get; }

    private IReadOnlyList<TrackItemViewModel> _allTracks = Array.Empty<TrackItemViewModel>();
    private IReadOnlyList<TrackItemViewModel> _queue = Array.Empty<TrackItemViewModel>();
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
                         SettingsService settings)
    {
        _covers = covers;
        _settings = settings;
        Lyrics = lyrics;
        Playlists = playlists;
        _player = player;
        _library = library;
        _folderPicker = folderPicker;

        // BASS dispara estos eventos desde su propio hilo: hay que volver al hilo de la UI.
        _player.StateChanged += (_, state) =>
            Dispatcher.UIThread.Post(() => IsPlaying = state == PlaybackState.Playing);
        _player.TrackEnded += (_, _) =>
            Dispatcher.UIThread.Post(() => Next());

        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(250), DispatcherPriority.Background, OnTick);
        _timer.Start();

        // Espera 200 ms tras la última tecla para no refiltrar en cada pulsación.
        _searchDebounce = new DispatcherTimer(TimeSpan.FromMilliseconds(200), DispatcherPriority.Normal,
            (_, _) => { _searchDebounce.Stop(); ApplyFilter(); });

        Playlists.Changed += (_, _) => ApplyFilter();
        Playlists.Notified += message => Status = message;
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
            ApplySnapshot(await _library.AddFolderAsync(folder, progress));
        }
        catch (Exception ex)
        {
            Status = $"Error al escanear: {ex.Message}";
        }
    }

    private void ApplySnapshot(LibrarySnapshot snapshot)
    {
        _allTracks = snapshot.Tracks.Select(t => new TrackItemViewModel(t, _covers)).ToList();
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

        Tracks = new ObservableCollection<TrackItemViewModel>(
            source.Where(i => ids.Contains(i.Track.Id))); // conserva el orden de la fuente

        var searching = !string.IsNullOrWhiteSpace(SearchText);
        var playlist = Playlists.CurrentPlaylist;

        Status = playlist is null
            ? _allTracks.Count == 0 ? "Agrega una carpeta para empezar"
              : searching ? $"{Tracks.Count} de {source.Count} canciones"
              : $"{_allTracks.Count} canciones en la biblioteca"
            : source.Count == 0 ? $"\"{playlist.Name}\" está vacía. Clic derecho en una canción para agregarla"
              : searching ? $"{Tracks.Count} de {source.Count} canciones"
              : $"{playlist.Name}: {source.Count} canciones";
    }

    // ---- Reproducción ----

    [RelayCommand]
    private void PlayTrack(TrackItemViewModel? item)
    {
        if (item is null) return;
        _queue = Tracks.ToList(); // la cola es lo que el usuario veía al elegir
        StartPlayback(item);
    }

    private void StartPlayback(TrackItemViewModel item)
    {
        try
        {
            _player.Load(item.Track.FilePath);

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
            _ = Lyrics.LoadAsync(item.Track); // sin await: la letra aparece cuando esté lista
            _ = LoadNowPlayingCoverAsync(item.Track);
            _settings.Update(s => s with { LastTrackId = item.Track.Id });
            NowPlaying = $"{item.Title} — {item.Artist}";

            _player.Play();
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
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
        var index = IndexOfCurrent();
        if (index >= 0 && index + 1 < _queue.Count)
            StartPlayback(_queue[index + 1]);
    }

    [RelayCommand]
    private void Previous()
    {
        if (_player.CurrentTime > TimeSpan.FromSeconds(3))
        {
            _player.Seek(TimeSpan.Zero);
            return;
        }

        var index = IndexOfCurrent();
        if (index > 0)
            StartPlayback(_queue[index - 1]);
    }

    private int IndexOfCurrent()
    {
        if (CurrentTrack is null) return -1;
        for (int i = 0; i < _queue.Count; i++)
            if (string.Equals(_queue[i].Track.FilePath, CurrentTrack.Track.FilePath,
                    StringComparison.OrdinalIgnoreCase))
                return i;
        return -1;
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