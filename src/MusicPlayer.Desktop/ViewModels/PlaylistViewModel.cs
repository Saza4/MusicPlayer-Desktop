using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;

namespace MusicPlayer.Desktop.ViewModels;

public enum PlaylistEditMode { None, Create, Rename, ConfirmDelete }

public partial class PlaylistViewModel : ObservableObject
{
    private readonly PlaylistManager _manager;
    private readonly PlaylistItemViewModel _library = new(null);
    private Guid? _pendingTrackId; // canción que se añadirá al crear la playlist

    public PlaylistViewModel(PlaylistManager manager)
    {
        _manager = manager;
        Items = new ObservableCollection<PlaylistItemViewModel> { _library };
        Selected = _library;
    }

    /// <summary>El primer elemento es siempre "Biblioteca"; el resto, las playlists.</summary>
    public ObservableCollection<PlaylistItemViewModel> Items { get; }

    /// <summary>Cambió la selección o el contenido de la lista visible.</summary>
    public event EventHandler? Changed;

    /// <summary>Mensaje breve para la barra de estado.</summary>
    public event Action<string>? Notified;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentPlaylist))]
    [NotifyPropertyChangedFor(nameof(HasPlaylistSelected))]
    private PlaylistItemViewModel? _selected;

    /// <summary>Null cuando se está viendo la biblioteca.</summary>
    public Playlist? CurrentPlaylist => Selected?.Playlist;
    public bool HasPlaylistSelected => CurrentPlaylist is not null;

    // ---- Panel de edición en línea ----

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditing), nameof(IsNameInput), nameof(IsConfirmDelete),
        nameof(Prompt), nameof(ConfirmText))]
    private PlaylistEditMode _mode;

    [ObservableProperty] private string _nameDraft = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEditError))]
    private string _editError = "";

    public bool IsEditing => Mode != PlaylistEditMode.None;
    public bool IsNameInput => Mode is PlaylistEditMode.Create or PlaylistEditMode.Rename;
    public bool IsConfirmDelete => Mode == PlaylistEditMode.ConfirmDelete;
    public bool HasEditError => EditError.Length > 0;

    public string Prompt => Mode switch
    {
        PlaylistEditMode.Create => "Nueva playlist",
        PlaylistEditMode.Rename => "Renombrar playlist",
        PlaylistEditMode.ConfirmDelete =>
            $"¿Eliminar \"{CurrentPlaylist?.Name}\"? Las canciones no se borran del disco.",
        _ => ""
    };

    public string ConfirmText => Mode == PlaylistEditMode.ConfirmDelete ? "Eliminar" : "Aceptar";

    partial void OnSelectedChanged(PlaylistItemViewModel? value)
    {
        CancelEdit();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task InitializeAsync()
    {
        try
        {
            foreach (var playlist in await _manager.LoadAsync())
                Items.Add(new PlaylistItemViewModel(playlist));
        }
        catch (Exception ex)
        {
            // Las playlists no deben impedir que la biblioteca cargue.
            Notified?.Invoke($"No se pudieron cargar las playlists: {ex.Message}");
        }
    }

    // ---- Comandos del panel ----

    [RelayCommand]
    private void BeginCreate() => StartEdit(PlaylistEditMode.Create, "", pendingTrack: null);

    /// <summary>Crea una playlist y le añade esta canción (desde el menú contextual).</summary>
    public void BeginCreateWith(Guid trackId) => StartEdit(PlaylistEditMode.Create, "", trackId);

    [RelayCommand]
    private void BeginRename()
    {
        if (CurrentPlaylist is { } playlist)
            StartEdit(PlaylistEditMode.Rename, playlist.Name, pendingTrack: null);
    }

    [RelayCommand]
    private void BeginDelete()
    {
        if (CurrentPlaylist is not null)
            StartEdit(PlaylistEditMode.ConfirmDelete, "", pendingTrack: null);
    }

    [RelayCommand]
    private void CancelEdit()
    {
        _pendingTrackId = null;
        NameDraft = "";
        EditError = "";
        Mode = PlaylistEditMode.None;
    }

    [RelayCommand]
    private async Task ConfirmEditAsync()
    {
        try
        {
            switch (Mode)
            {
                case PlaylistEditMode.Create: await CreateAsync(); break;
                case PlaylistEditMode.Rename: await RenameAsync(); break;
                case PlaylistEditMode.ConfirmDelete: await DeleteAsync(); break;
            }
        }
        catch (Exception ex)
        {
            if (IsEditing)
                EditError = ex.Message; // el panel sigue abierto para corregir el nombre
            else
                Notified?.Invoke(ex.Message);
        }
    }

    // ---- Acciones sobre canciones (las invoca el menú contextual) ----

    public async Task AddTrackAsync(Track track, PlaylistItemViewModel target)
    {
        if (target.Playlist is not { } playlist) return;

        try
        {
            var added = await _manager.AddTracksAsync(playlist.Id, new[] { track.Id });
            Refresh(target);

            if (ReferenceEquals(Selected, target))
                Changed?.Invoke(this, EventArgs.Empty);

            Notified?.Invoke(added > 0
                ? $"Añadida a \"{playlist.Name}\""
                : $"Ya estaba en \"{playlist.Name}\"");
        }
        catch (Exception ex)
        {
            Notified?.Invoke($"No se pudo agregar: {ex.Message}");
        }
    }

    public async Task RemoveTrackAsync(Track track)
    {
        if (Selected is not { Playlist: { } playlist } item) return;

        try
        {
            await _manager.RemoveTrackAsync(playlist.Id, track.Id);
            Refresh(item);
            Changed?.Invoke(this, EventArgs.Empty);
            Notified?.Invoke($"Quitada de \"{playlist.Name}\"");
        }
        catch (Exception ex)
        {
            Notified?.Invoke($"No se pudo quitar: {ex.Message}");
        }
    }

    // ---- Internos ----

    private void StartEdit(PlaylistEditMode mode, string draft, Guid? pendingTrack)
    {
        _pendingTrackId = pendingTrack;
        NameDraft = draft;
        EditError = "";
        Mode = mode;
    }

    private async Task CreateAsync()
    {
        var created = await _manager.CreateAsync(NameDraft);
        var item = new PlaylistItemViewModel(created);
        Items.Add(item);

        var pending = _pendingTrackId;
        CancelEdit();

        if (pending is { } trackId)
        {
            await _manager.AddTracksAsync(created.Id, new[] { trackId });
            Refresh(item);
            Notified?.Invoke($"Añadida a \"{created.Name}\"");
        }
        else
        {
            Selected = item;
        }
    }

    private async Task RenameAsync()
    {
        if (Selected is not { Playlist: { } current } item) return;

        item.Playlist = await _manager.RenameAsync(current.Id, NameDraft);
        CancelEdit();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private async Task DeleteAsync()
    {
        if (Selected is not { Playlist: { } current } item) return;

        await _manager.DeleteAsync(current.Id);
        CancelEdit();

        Selected = _library; // primero cambiar la selección, luego quitar el elemento
        Items.Remove(item);
    }

    private void Refresh(PlaylistItemViewModel item)
    {
        if (item.Playlist is { } playlist && _manager.Find(playlist.Id) is { } fresh)
            item.Playlist = fresh;
    }
}