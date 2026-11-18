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
using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;
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
    private bool _spaceWasShortcut;
    private bool _scrubbing; // puntero pulsado sobre una barra de progreso
    private bool _layersMenuHooked;

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
        Deactivated += (_, _) => _muteKeyDown = _spaceWasShortcut = false; // si se suelta la tecla con otra ventana en primer plano
        Opened += OnOpened;

        // Arrastre de las barras de progreso (la principal y la del inmersivo, Classes="progress"): el salto se hace
        // al soltar (docs/specs/ui-responsiveness.md, criterio 4). Tunnel, porque el pulgar marca el evento como
        // atendido; la captura perdida (cambio de ventana a mitad) cuenta como soltar.
        AddHandler(PointerPressedEvent, OnProgressPressed, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, (_, _) => EndProgressScrub(), RoutingStrategies.Tunnel);
        AddHandler(PointerCaptureLostEvent, (_, _) => EndProgressScrub(), RoutingStrategies.Bubble, handledEventsToo: true);
        Deactivated += (_, _) => EndProgressScrub();
        SizeChanged += (_, _) => ApplyLayout();

        DataContextChanged += (_, _) =>
        {
            if (DataContext is not MainViewModel vm) return;

            vm.PropertyChanged += OnViewModelPropertyChanged;
            vm.Playlists.PropertyChanged += OnPlaylistsPropertyChanged;
            vm.Lyrics.PropertyChanged += OnLyricsPropertyChanged;
            FillLayersMenu();
            ApplyLayout();
        };
    }

    // ---- Diseño adaptable (docs/specs/responsive-layout.md) ----

    // Con la ventana estrecha la barra lateral arranca plegada; su botón de la barra superior la muestra.
    private bool _sidebarOpenWhenNarrow;

    // Solo visual: los umbrales salen de Core (LayoutBreakpoints) y aquí se aplican clases, anchos y visibilidad.
    //  · Barra superior «compact»: los botones de acción quedan solo con icono (ActionButtonStyles.axaml).
    //  · Barra de reproducción: el grupo derecho recibe el ancho que necesita y, más estrecha aún («narrow»), el
    //    deslizador de volumen pasa a un desplegable.
    //  · Zona central: la barra lateral se pliega y el panel de letra ocupa el sitio de la lista si no caben los dos.
    //  · Paneles de edición: se desplazan cuando no caben a lo alto.
    private void ApplyLayout()
    {
        var width = ClientSize.Width;
        var layout = LayoutBreakpoints.For(width);

        TopBar.Classes.Set("compact", layout.TopBarCompact);

        // Controles centrados en la ventana: columna central fija entre dos lados iguales (los dos son «*»).
        PlayerGrid.ColumnDefinitions[1].Width =
            new GridLength(LayoutBreakpoints.PlayerCenterWidth(width, layout.VolumeInFlyout));
        PlayerBar.Classes.Set("narrow", layout.VolumeInFlyout);
        if (!layout.VolumeInFlyout) VolumeButton.Flyout?.Hide();

        var sidebarVisible = !layout.SidebarCollapsible || _sidebarOpenWhenNarrow;
        SidebarButton.IsVisible = layout.SidebarCollapsible;
        // En Artistas, la lista de artistas ocupa el sitio de la barra lateral (mockup 1c); el ≡ pliega la que toque.
        var artists = DataContext is MainViewModel { ShowArtistSidebar: true };
        if (artists != _artistSidebarShown)
        {
            _artistSidebarShown = artists;
            SlideIn(artists ? ArtistSidebar : Sidebar);
        }
        Sidebar.IsVisible = sidebarVisible && !artists;
        ArtistSidebar.IsVisible = sidebarVisible && artists;
        SidebarBackground.IsVisible = sidebarVisible;
        CentralGrid.ColumnDefinitions[0].Width = new GridLength(sidebarVisible ? LayoutBreakpoints.SidebarWidth : 0);

        var lyricsOnly = DataContext is MainViewModel { Lyrics.ShowPanel: true }
                         && LayoutBreakpoints.LyricsReplacesList(width, sidebarVisible);
        ListArea.IsVisible = !lyricsOnly;
        CentralGrid.ColumnDefinitions[1].Width = lyricsOnly ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        CentralGrid.ColumnDefinitions[2].Width = lyricsOnly ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;
        LyricsPanel.Width = lyricsOnly ? double.NaN : LayoutBreakpoints.LyricsPanelWidth;

        var panelHeight = LayoutBreakpoints.EditPanelMaxHeight(ClientSize.Height);
        ConvertScroll.MaxHeight = AutoFixScroll.MaxHeight = TagEditorScroll.MaxHeight = panelHeight;
    }

    private bool _artistSidebarShown;

    // La animación de la clase «slide-in» corre al ponerla: se quita del otro panel y se vuelve a poner en este.
    private void SlideIn(Control panel)
    {
        Sidebar.Classes.Remove("slide-in");
        ArtistSidebar.Classes.Remove("slide-in");
        panel.Classes.Add("slide-in");
    }

    // Si el foco estaba en el panel que se va, pasa a la lista del que llega: con teclado no se pierde.
    private void MoveFocusToSidebar()
    {
        var focused = FocusManager?.GetFocusedElement() as Visual;
        var hidden = _artistSidebarShown ? Sidebar : ArtistSidebar;
        if (focused is null || !hidden.IsVisualAncestorOf(focused)) return;

        Control target = _artistSidebarShown ? ArtistList : LibraryNav;
        Dispatcher.UIThread.Post(() => target.Focus(NavigationMethod.Tab));
    }

    // «Artistas» ya elegido no cambia la selección: el clic trae de vuelta la lista si se había ocultado con ←.
    private void OnArtistsNavTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel { ShowArtistPage: true, ShowArtistSidebar: false } vm)
            vm.ShowArtistListCommand.Execute(null);
    }

    private void OnSidebarButtonClick(object? sender, RoutedEventArgs e)
    {
        _sidebarOpenWhenNarrow = !_sidebarOpenWhenNarrow;
        ApplyLayout();
    }

    // La rueda sobre el botón de volumen lo sube o lo baja un paso, sin abrir nada.
    private void OnVolumeWheel(object? sender, PointerWheelEventArgs e)
    {
        if (DataContext is not MainViewModel vm || e.Delta.Y == 0) return;
        vm.ChangeVolume(e.Delta.Y > 0 ? +1 : -1);
        e.Handled = true;
    }

    // El foco va al deslizador: las flechas cambian el volumen desde el primer momento.
    private void OnVolumeFlyoutOpened(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(() => VolumeFlyoutSliderOf(sender)?.Focus(NavigationMethod.Tab));

    // El contenido del desplegable no forma parte del árbol de la ventana: se busca desde él.
    private static Slider? VolumeFlyoutSliderOf(object? flyout) =>
        ((flyout as Flyout)?.Content as Panel)?.Children.OfType<Slider>().FirstOrDefault();

    // Al abrir: la ventana toma el foco del teclado y el foco lógico queda en la lista, no en el aire.
    private void OnOpened(object? sender, EventArgs e)
    {
        ApplyLayout();
        Activate();
        this.FindControl<ListBox>("SongList")?.Focus();    }

    private void OnProgressPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        if ((e.Source as Visual)?.FindAncestorOfType<Slider>(includeSelf: true) is not { } slider
            || !slider.Classes.Contains("progress"))
            return;

        _scrubbing = true;
        vm.BeginScrub();
    }

    private void EndProgressScrub()
    {
        if (!_scrubbing) return;
        _scrubbing = false;
        (DataContext as MainViewModel)?.EndScrub();
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
                if (!_spaceWasShortcut) // mantener la tecla no debe alternar una y otra vez
                {
                    _spaceWasShortcut = true;
                    vm.TogglePlayPauseCommand.Execute(null);
                }
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

        // Un botón hace clic con Espacio al soltar la tecla (Button.OnKeyUp), aunque la pulsación ya fuera el atajo de
        // reproducir/pausar. Sin esto, Espacio en un botón enfocado pausaba y además lo pulsaba (en el de capas del
        // inmersivo, abría el menú; criterio 16 de docs/specs/lyrics-layers.md).
        if (e.Key == Key.Space && _spaceWasShortcut)
        {
            _spaceWasShortcut = false;
            e.Handled = true;
        }
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

    // ---- Menú de capas del modo inmersivo (docs/specs/lyrics-layers.md, criterios 12-16) ----

    // Las opciones se recalculan al abrir: etiquetas según lo que se ve y desactivadas si lo dejarían todo oculto.
    private void OnLayersMenuOpening(object? sender, EventArgs e)
    {
        FillLayersMenu();
        _layersMenuOpen = true;
    }

    private void OnLayersMenuClosed(object? sender, EventArgs e) => _layersMenuOpen = false;

    // Con el menú abierto, el foco está en el menú: Avalonia ya lleva ↑ ↓, Enter y Esc, pero no Espacio. Espacio activa
    // la opción resaltada (criterio 16). El presentador se crea una vez y se reutiliza: se engancha la primera vez.
    private void OnLayersMenuOpened(object? sender, EventArgs e)
    {
        if (_layersMenuHooked || sender is not MenuFlyout menu) return;
        if (menu.Items.OfType<MenuItem>().FirstOrDefault()?.Parent is not Control presenter) return;
        presenter.AddHandler(KeyDownEvent, (_, args) =>
        {
            if (args.Key != Key.Space || args.KeyModifiers != KeyModifiers.None) return;
            args.Handled = true;
            _spaceWasShortcut = true; // al soltar, el botón enfocado no debe reabrir el menú
            if (menu.Items.OfType<MenuItem>().FirstOrDefault(i => i.IsSelected && i.IsEnabled) is not { } item) return;
            item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            menu.Hide();
        }, RoutingStrategies.Tunnel);
        _layersMenuHooked = true;
    }

    // El menú nunca está vacío al abrirse: en Avalonia 11.2.5, un MenuFlyout vacío se abre sin nada a la vista aunque
    // se llene en Opening (comprobado con una reproducción mínima). Por eso se llena, con el menú cerrado, cada vez
    // que cambian las capas o las preferencias, y otra vez al abrir. Siempre hay opciones: las dos de incrustar van
    // al final aunque no haya capas. Con el menú abierto no se rellena: borraría la opción recién pulsada y el menú
    // no se cerraría.
    private void FillLayersMenu()
    {
        if (LayersButton.Flyout is not MenuFlyout menu || DataContext is not MainViewModel vm) return;

        // Aspecto del inmersivo (vidrio oscuro, ver App.axaml): una sola vez, el presentador se reutiliza.
        if (!menu.FlyoutPresenterClasses.Contains("immersive"))
            menu.FlyoutPresenterClasses.Add("immersive");

        menu.Items.Clear();
        foreach (var option in vm.Lyrics.LayerMenu())
        {
            var item = new MenuItem { Header = option.Label, IsEnabled = option.IsEnabled };
            var row = option.Row;
            item.Click += (_, _) => vm.Lyrics.ApplyLayerOption(row); // recalcula: nunca actúa sobre una capa que ya no está
            menu.Items.Add(item);
        }

        // Criterio 12b: dos acciones que abren el selector para incrustar una capa de cualquier carpeta.
        if (menu.Items.Count > 0) menu.Items.Add(new Separator());
        menu.Items.Add(EmbedItem(vm, LyricsLayerMenu.EmbedTranslationLabel, LyricsLayerKind.Translation));
        menu.Items.Add(EmbedItem(vm, LyricsLayerMenu.EmbedPronunciationLabel, LyricsLayerKind.Pronunciation));
    }

    private static MenuItem EmbedItem(MainViewModel vm, string label, LyricsLayerKind kind)
    {
        var item = new MenuItem { Header = label };
        item.Click += (_, _) => _ = vm.EmbedLayerInImmersiveAsync(kind);
        return item;
    }

    // Otra pista (o la misma sin capas): el menú abierto se cierra en lugar de quedar con opciones viejas.
    private void OnLyricsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Abrir o cerrar el panel de letra puede cambiar quién ocupa la zona central.
        if (e.PropertyName is nameof(LyricsViewModel.ShowPanel)) ApplyLayout();

        if (_layersMenuOpen && e.PropertyName is nameof(LyricsViewModel.Lines) or nameof(LyricsViewModel.ShowLayersButton))
        {
            LayersButton.Flyout?.Hide();
            return;
        }
        if (!_layersMenuOpen && e.PropertyName is nameof(LyricsViewModel.Lines) or nameof(LyricsViewModel.HasTranslation)
            or nameof(LyricsViewModel.HasPronunciation) or nameof(LyricsViewModel.TranslationLanguage)
            or nameof(LyricsViewModel.ShowTranslation) or nameof(LyricsViewModel.ShowPronunciation)
            or nameof(LyricsViewModel.HideOriginal))
            FillLayersMenu();
    }

    // Controles que usan estas teclas para lo suyo: escribir, abrir una lista desplegable, navegar un menú.
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

    // Se cerró sin confirmar (Esc o clic fuera): se vuelve al tema elegido. El foco regresa al botón «Tema», que
    // está en Configuración (la lista de canciones queda tapada por esa vista).
    private void OnThemeFlyoutClosed(object? sender, EventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.Themes.Close();
        Dispatcher.UIThread.Post(() => this.FindControl<Button>("ThemeButton")?.Focus());
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
            case Key.Escape:
                // Solo cierra la lista: sin esto, el mismo Esc llegaría a OnKeyDown y cerraría también Configuración.
                this.FindControl<Button>("ThemeButton")?.Flyout?.Hide();
                break;
            default:
                return;
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

    private async void OnOpenDataFolder(object? sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.DataDirectory);
            await Launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(AppPaths.DataDirectory));
        }
        catch (Exception)
        {
            if (DataContext is MainViewModel vm)
                vm.Status = $"Carpeta de datos: {AppPaths.DataDirectory}";
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.ShowArtistSidebar))
        {
            var before = _artistSidebarShown;
            ApplyLayout();
            if (before != _artistSidebarShown) MoveFocusToSidebar();
            return;
        }

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

        // El cuadro del nombre vive en la barra lateral: si está plegada (ventana estrecha), se despliega.
        if (!_sidebarOpenWhenNarrow)
        {
            _sidebarOpenWhenNarrow = true;
            ApplyLayout();
        }

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

        // Configuración se cierra con Esc. El inmersivo va antes (y al abrirse ya la cierra); un cuadro de texto o
        // una lista desplegable que usan Esc para lo suyo marcan la tecla como atendida y no se llega aquí.
        if (e.Key == Key.Escape && DataContext is MainViewModel { Settings.IsOpen: true } open)
        {
            open.Settings.CloseCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // Esc con el foco en la lista de artistas vuelve a «Tu biblioteca», como su botón ←. Fuera de ella, Esc no cambia.
        if (e.Key == Key.Escape && DataContext is MainViewModel { ShowArtistSidebar: true } page
            && FocusManager?.GetFocusedElement() is Visual focused && ArtistSidebar.IsVisualAncestorOf(focused))
        {
            page.ShowLibraryNavCommand.Execute(null);
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

    // Clic y doble clic en la lista de canciones. Puro código de vista: si reproduce o no lo decide Core
    // (TrackClick, docs/specs/settings-panel.md, criterios 9 y 10) y reproducir es cosa del ViewModel.
    private void OnTrackTapped(object? sender, TappedEventArgs e) => OnTrackClicked(e, clickCount: 1);

    private void OnTrackDoubleTapped(object? sender, TappedEventArgs e) => OnTrackClicked(e, clickCount: 2);

    private void OnTrackClicked(TappedEventArgs e, int clickCount)
    {
        // Solo cuenta la fila pulsada: la barra de desplazamiento o el hueco vacío de la lista no reproducen nada
        // (antes el doble clic ahí reproducía la pista seleccionada; docs/specs/audit-3.md, H5).
        if (DataContext is not MainViewModel vm
            || e.Source is not Control { DataContext: TrackItemViewModel item })
            return;

        var playingThis = vm.IsPlaying && vm.CurrentTrack?.Track.Id == item.Track.Id;
        var modifier = (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Shift)) != 0;
        if (TrackClick.ShouldPlay(vm.Settings.PlayOnSingleClick, clickCount, isLeftButton: true, modifier, playingThis))
            vm.PlayTrackCommand.Execute(item);
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