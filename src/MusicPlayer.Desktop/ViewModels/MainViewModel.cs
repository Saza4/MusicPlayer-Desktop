using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
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

    public MainViewModel(IAudioPlayer player, LibraryService library, IFolderPickerService folderPicker)
    {
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
    }

    // ---- Biblioteca ----

    public async Task InitializeAsync()
    {
        try
        {
            Status = "Cargando biblioteca…";
            ApplySnapshot(await _library.LoadAsync());
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
        _allTracks = snapshot.Tracks.Select(t => new TrackItemViewModel(t)).ToList();
        ApplyFilter();
    }

    partial void OnSearchTextChanged(string value)
    {
        _searchDebounce.Stop();
        _searchDebounce.Start();
    }

    private void ApplyFilter()
    {
        var ids = TrackFilter.Filter(_allTracks.Select(i => i.Track), SearchText)
            .Select(t => t.Id)
            .ToHashSet();

        Tracks = new ObservableCollection<TrackItemViewModel>(
            _allTracks.Where(i => ids.Contains(i.Track.Id))); // conserva el orden de la biblioteca

        Status = _allTracks.Count == 0 ? "Agrega una carpeta para empezar"
            : string.IsNullOrWhiteSpace(SearchText) ? $"{_allTracks.Count} canciones en la biblioteca"
            : $"{Tracks.Count} de {_allTracks.Count} canciones";
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
            NowPlaying = $"{item.Title} — {item.Artist}";

            _player.Play();
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

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

    // ---- Barra de progreso ----

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
        PositionText = TimeFormatter.Format(TimeSpan.FromSeconds(value));

        // Si el cambio vino del usuario (arrastrando el Slider), saltar a ese punto.
        if (!_isUpdatingFromTimer)
            _player.Seek(TimeSpan.FromSeconds(value));
    }
}