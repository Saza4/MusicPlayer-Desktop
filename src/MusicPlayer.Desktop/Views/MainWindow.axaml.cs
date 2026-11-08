using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MusicPlayer.Infrastructure;
using MusicPlayer.Desktop.ViewModels;

namespace MusicPlayer.Desktop.Views;

public partial class MainWindow : Window
{
    private const long RepeatIntervalMs = 100;

    private WindowState _stateBeforeImmersive = WindowState.Normal;
    private long _lastStepAt;
    private bool _muteKeyDown;
    private bool _layersMenuOpen;

    public MainWindow()
    {
        InitializeComponent();

#if DEBUG
        // Diagnóstico del redibujo (spec de letras progresivas, M2): MUSICPLAYER_RENDER_OVERLAY=DirtyRects,Fps,...
        // pinta encima lo que Avalonia repinta en cada fotograma.
        if (Enum.TryParse<Avalonia.Rendering.RendererDebugOverlays>(
                Environment.GetEnvironmentVariable("MUSICPLAYER_RENDER_OVERLAY"), out var overlays))
            RendererDiagnostics.DebugOverlays = overlays;
#endif

        // Tunnel: la ventana ve la tecla antes que el control enfocado, que la usaría para lo suyo.
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, OnPreviewKeyUp, RoutingStrategies.Tunnel);
        Deactivated += (_, _) => _muteKeyDown = false; // si se suelta la M con otra ventana en primer plano
        Opened += OnOpened;

        DataContextChanged += (_, _) =>
        {
            if (DataContext is not MainViewModel vm) return;

            vm.PropertyChanged += OnViewModelPropertyChanged;
            vm.Playlists.PropertyChanged += OnPlaylistsPropertyChanged;
            vm.Lyrics.PropertyChanged += OnLyricsPropertyChanged;
        };
    }

    // Al abrir: la ventana toma el foco del teclado y el foco lógico queda en la lista, no en el aire.
    private void OnOpened(object? sender, EventArgs e)
    {
        Activate();
        this.FindControl<ListBox>("SongList")?.Focus();
    }

    // Atajos en toda la ventana, salvo donde el control enfocado necesita la tecla para lo suyo:
    //   Espacio reproduce/pausa · M silencia · ← → saltan 5 s · Ctrl+↑ Ctrl+↓ cambian el volumen.
    // ↑ ↓ sin modificador no se tocan: mueven la selección de las listas.
    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        // Con el selector de tema abierto, las teclas son suyas (flechas, Enter, Esc): ningún atajo global.
        // El foco se queda en el botón que abrió el Flyout, así que es la ventana quien las recibe.
        if (vm.Themes.IsOpen)
        {
            HandleThemeKey(vm, e);
            return;
        }

        // Con el menú de capas abierto, las teclas son suyas (Espacio y Enter activan la opción, Esc cierra):
        // criterio 16 de docs/specs/lyrics-layers.md. Cerrado, Espacio en su botón reproduce o pausa, como en los demás.
        if (_layersMenuOpen || OwnsKeyboard(e.Source))
            return;

        switch (e.Key, e.KeyModifiers)
        {
            case (Key.Space, KeyModifiers.None) when e.Source is not CheckBox:
                vm.TogglePlayPauseCommand.Execute(null);
                break;

            case (Key.M, KeyModifiers.None):
                if (!_muteKeyDown) // mantener la tecla no debe alternar una y otra vez
                {
                    _muteKeyDown = true;
                    vm.ToggleMuteCommand.Execute(null);
                }
                break;

            case (Key.Left, KeyModifiers.None):
                if (!IsTooSoon()) vm.SeekBy(-1);
                break;
            case (Key.Right, KeyModifiers.None):
                if (!IsTooSoon()) vm.SeekBy(+1);
                break;
            case (Key.Up, KeyModifiers.Control):
                if (!IsTooSoon()) vm.ChangeVolume(+1);
                break;
            case (Key.Down, KeyModifiers.Control):
                if (!IsTooSoon()) vm.ChangeVolume(-1);
                break;

            default:
                return;
        }

        e.Handled = true; // el botón, el slider o la fila enfocados no reciben la tecla
    }

    private void OnPreviewKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.M) _muteKeyDown = false;
    }

    // Limita la repetición de una tecla mantenida (saltos y volumen) a un paso cada RepeatInterval ms.
    private bool IsTooSoon()
    {
        var now = Environment.TickCount64;
        if (now >= _lastStepAt && now - _lastStepAt < RepeatIntervalMs)
            return true;

        _lastStepAt = now;
        return false;
    }

    // Controles que usan estas teclas para lo suyo: escribir, abrir una lista desplegable, navegar un menú.
    // ---- Menú de capas del modo inmersivo (docs/specs/lyrics-layers.md, criterios 12-16) ----

    // Las opciones se recalculan al abrir: etiquetas según lo que se ve y desactivadas si lo dejarían todo oculto.
    private void OnLayersMenuOpening(object? sender, EventArgs e)
    {
        if (sender is not MenuFlyout menu || DataContext is not MainViewModel vm) return;

        menu.Items.Clear();
        foreach (var option in vm.Lyrics.LayerMenu())
        {
            var item = new MenuItem { Header = option.Label, IsEnabled = option.IsEnabled };
            var row = option.Row;
            item.Click += (_, _) => vm.Lyrics.ApplyLayerOption(row); // recalcula: nunca actúa sobre una capa que ya no está
            menu.Items.Add(item);
        }
        _layersMenuOpen = true;
    }

    private void OnLayersMenuClosed(object? sender, EventArgs e) => _layersMenuOpen = false;

    // Otra pista (o la misma sin capas): el menú abierto se cierra en lugar de quedar con opciones viejas.
    private void OnLyricsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_layersMenuOpen) return;
        if (e.PropertyName is nameof(LyricsViewModel.Lines) or nameof(LyricsViewModel.ShowLayersButton))
            LayersButton.Flyout?.Hide();
    }

    private static bool OwnsKeyboard(object? source) =>
        source is TextBox or ComboBox or ComboBoxItem or MenuItem or Avalonia.Controls.ContextMenu;

    // ---- Selector de tema (vista previa en vivo) ----

    private static ThemeChoice? ChoiceAt(object? source) =>
        (source as Visual)?.FindAncestorOfType<ListBoxItem>(includeSelf: true)?.DataContext as ThemeChoice;

    // El foco va a la lista cuando ya tiene sus elementos, para que las flechas previsualicen desde el primer momento.
    private async void OnThemeFlyoutOpened(object? sender, EventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        await vm.Themes.OpenAsync();
        ThemeListOf(sender)?.Focus();
    }

    // La lista vive dentro del Flyout, que no forma parte del árbol de la ventana hasta abrirse: se busca desde él.
    private static ListBox? ThemeListOf(object? flyout) =>
        ((flyout as Flyout)?.Content as Panel)?.Children.OfType<ListBox>().FirstOrDefault();

    // Se cerró sin confirmar (Esc o clic fuera): se vuelve al tema elegido. El foco regresa a la lista de canciones.
    private void OnThemeFlyoutClosed(object? sender, EventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.Themes.Close();
        Dispatcher.UIThread.Post(() => this.FindControl<ListBox>("SongList")?.Focus());
    }

    private void OnThemeListPointerMoved(object? sender, PointerEventArgs e)
    {
        if (DataContext is MainViewModel vm && ChoiceAt(e.Source) is { } choice)
            vm.Themes.Preview(choice.Id);
    }

    // El cursor salió de la lista: se previsualiza lo resaltado con el teclado (o el tema confirmado si no hay nada).
    private void OnThemeListPointerExited(object? sender, PointerEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        if (vm.Themes.SelectedChoice is { } selected) vm.Themes.Preview(selected.Id);
        else vm.Themes.Cancel();
    }

    private void OnThemeListTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel vm && ChoiceAt(e.Source) is { } choice)
            ConfirmTheme(vm, choice);
    }

    private void HandleThemeKey(MainViewModel vm, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                vm.Themes.MoveSelection(+1);
                break;
            case Key.Up:
                vm.Themes.MoveSelection(-1);
                break;
            case Key.Enter when vm.Themes.SelectedChoice is { } choice:
                ConfirmTheme(vm, choice);
                break;
            default:
                return; // Esc cierra el Flyout por sí solo
        }

        e.Handled = true;
    }

    private void ConfirmTheme(MainViewModel vm, ThemeChoice choice)
    {
        vm.Themes.Commit(choice.Id);
        this.FindControl<Button>("ThemeButton")?.Flyout?.Hide();
    }

    private async void OnOpenThemesFolder(object? sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.Themes);
            await Launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(AppPaths.Themes));
        }
        catch (Exception)
        {
            if (DataContext is MainViewModel vm)
                vm.Status = $"Carpeta de temas: {AppPaths.Themes}";
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.IsImmersive) || sender is not MainViewModel vm)
            return;

        if (vm.IsImmersive)
        {
            _stateBeforeImmersive = WindowState == WindowState.FullScreen ? WindowState.Normal : WindowState;
            WindowState = WindowState.FullScreen;
        }
        else
        {
            WindowState = _stateBeforeImmersive;
        }
    }

    // Al abrir el panel de nombre, enfoca el cuadro de texto.
    private void OnPlaylistsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PlaylistViewModel.IsNameInput)
            || sender is not PlaylistViewModel { IsNameInput: true })
            return;

        // Se espera a que el cuadro sea visible antes de enfocarlo.
        Dispatcher.UIThread.Post(() =>
        {
            var box = this.FindControl<TextBox>("PlaylistNameBox");
            box?.Focus();
            box?.SelectAll();
        });
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && DataContext is MainViewModel { IsImmersive: true } vm)
        {
            vm.CloseImmersiveCommand.Execute(null);
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    private void OnPlaylistNameKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        switch (e.Key)
        {
            case Key.Enter:
                vm.Playlists.ConfirmEditCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Escape:
                vm.Playlists.CancelEditCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    // Puro código de vista: delega toda la lógica en el ViewModel.
    private void OnTrackDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.SelectedTrack is { } track)
            vm.PlayTrackCommand.Execute(track);
    }

    private void OnLyricTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel vm && (e.Source as Control)?.DataContext is LyricLineViewModel line)
            vm.Lyrics.SeekToLineCommand.Execute(line);
    }

    private void OnTrackListContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        // Solo si el clic derecho cayó sobre una fila de canción.
        if (sender is not Control owner
            || DataContext is not MainViewModel vm
            || e.Source is not Control { DataContext: TrackItemViewModel item })
            return;

        var entries = new List<Control>();

        var play = new MenuItem { Header = "Reproducir" };
        play.Click += (_, _) =>
        {
            vm.SelectedTrack = item;
            vm.PlayTrackCommand.Execute(item);
        };
        entries.Add(play);

        var playNext = new MenuItem { Header = "Reproducir siguiente" };
        playNext.Click += (_, _) => vm.PlayNextCommand.Execute(item);
        entries.Add(playNext);

        var enqueue = new MenuItem { Header = "Agregar a la cola" };
        enqueue.Click += (_, _) => vm.AddToQueueCommand.Execute(item);
        entries.Add(enqueue);

        entries.Add(new Separator());

        // Submenú: una entrada por playlist, más "Nueva playlist…".
        var targets = new List<Control>();
        foreach (var target in vm.Playlists.Items.Where(p => !p.IsLibrary))
        {
            var alreadyIn = target.Playlist!.TrackIds.Contains(item.Track.Id);
            var entry = new MenuItem
            {
                Header = MenuText(target.Name) + (alreadyIn ? "  ✓" : ""),
                IsEnabled = !alreadyIn
            };
            entry.Click += (_, _) => _ = vm.Playlists.AddTrackAsync(item.Track, target);
            targets.Add(entry);
        }

        if (targets.Count > 0)
            targets.Add(new Separator());

        var create = new MenuItem { Header = "Nueva playlist…" };
        create.Click += (_, _) => vm.Playlists.BeginCreateWith(item.Track.Id);
        targets.Add(create);

        entries.Add(new MenuItem { Header = "Agregar a una playlist", ItemsSource = targets });

        if (vm.Playlists.CurrentPlaylist is not null)
        {
            var remove = new MenuItem { Header = "Quitar de esta playlist" };
            remove.Click += (_, _) => _ = vm.Playlists.RemoveTrackAsync(item.Track);
            entries.Add(remove);
        }

        var edit = new MenuItem { Header = "Editar etiquetas…" };
        edit.Click += (_, _) => vm.TagEditor.Begin(item);
        entries.Add(edit);

        var convert = new MenuItem { Header = "Convertir formato…" };
        convert.Click += (_, _) => vm.Conversion.Begin(new[] { item.Track }, item.Title);
        entries.Add(convert);

        var albumCover = new MenuItem { Header = "Carátula del álbum…" };
        albumCover.Click += (_, _) => _ = vm.AlbumCover.ApplyAsync(item.Track);
        entries.Add(albumCover);

        var embedLyrics = new MenuItem { Header = "Incrustar letra (.lrc)…" };
        embedLyrics.Click += (_, _) => _ = vm.EmbedLyrics.EmbedAsync(item.Track);
        entries.Add(embedLyrics);

        entries.Add(new Separator());
        var unlink = new MenuItem { Header = "Quitar de la biblioteca" };
        unlink.Click += (_, _) => vm.UnlinkTrackCommand.Execute(item);
        entries.Add(unlink);

        var delete = new MenuItem { Header = "Eliminar del disco…" };
        delete.Click += (_, _) => vm.AskDeleteTrackCommand.Execute(item);
        entries.Add(delete);

        new ContextMenu { ItemsSource = entries }.Open(owner);
        e.Handled = true;
    }

    private void OnBrowseTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        switch ((e.Source as Control)?.DataContext)
        {
            case ArtistItemViewModel artist: vm.OpenArtistCommand.Execute(artist); break;
            case AlbumItemViewModel album: vm.OpenAlbumCommand.Execute(album); break;
        }
    }

    // En el encabezado de un MenuItem, "_" marca una tecla de acceso: "__" muestra un guion bajo literal.
    private static string MenuText(string text) => text.Replace("_", "__");
}