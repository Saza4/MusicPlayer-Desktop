using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
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
            var services = CompositionRoot.Build();
            var viewModel = services.GetRequiredService<MainViewModel>();

            // Red de seguridad (docs/specs/playback-resilience.md, criterio 3): un error no controlado en el hilo
            // de la interfaz se anota y se avisa, pero no cierra la app.
            var errors = services.GetRequiredService<ErrorLog>();
            Dispatcher.UIThread.UnhandledException += (_, e) =>
            {
                errors.Write("interfaz", e.Exception);
                viewModel.Status = $"Error inesperado: {e.Exception.Message}";
                e.Handled = true;
            };
            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                errors.Write("tarea", e.Exception);
                e.SetObserved();
            };

            // El tema elegido se aplica antes de mostrar la ventana, para que no parpadee en Oscuro.
            viewModel.Themes.Initialize();

            var main = new MainWindow { DataContext = viewModel };
            desktop.MainWindow = main;

            // La app termina cuando se pide (Shutdown), no al cerrarse una ventana: la isla es una segunda ventana
            // y la principal puede ocultarse a la bandeja (docs/specs/background-playback.md).
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var island = services.GetRequiredService<IslandController>();
            island.Attach(main);
            SetUpBackground(desktop, main, viewModel, island);

            // Guarda los ajustes pendientes y libera lo que el contenedor creó (incluido BASS).
            desktop.Exit += (_, _) =>
            {
                Task.Run(() => services.GetRequiredService<SettingsService>().FlushAsync())
                    .GetAwaiter().GetResult();
                services.Dispose();
            };

            _ = viewModel.InitializeAsync(); // carga la biblioteca sin bloquear el arranque
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Cerrar la ventana la oculta a la bandeja o termina la app, según <see cref="CloseBehavior"/>. El icono de
    /// la bandeja solo existe en Windows y solo se ve con la ventana oculta; si no se puede crear, cerrar termina
    /// la app como siempre (nunca se oculta la ventana sin una forma de volver).
    /// </summary>
    private void SetUpBackground(
        IClassicDesktopStyleApplicationLifetime desktop, MainWindow main, MainViewModel viewModel, IslandController island)
    {
        var exiting = false;
        var explained = false;
        TrayIcon? tray = null;

        void Exit()
        {
            if (exiting) return;
            exiting = true;
            if (tray is not null) tray.IsVisible = false;
            desktop.Shutdown();
        }

        if (OperatingSystem.IsWindows())
        {
            try { tray = CreateTray(viewModel, island.ShowMain, Exit); }
            catch (Exception ex)
            {
                // El ajuste no surte efecto y se dice (la línea de estado acumula los avisos del arranque).
                viewModel.Status = $"No se pudo crear el icono de la bandeja ({ex.Message}): cerrar la ventana cerrará el reproductor";
            }

            // Una segunda instancia pide a esta que muestre su ventana.
            Program.Instance?.Listen(() => Dispatcher.UIThread.Post(() => { if (!exiting) island.ShowMain(); }));
        }

        viewModel.ExitRequested += Exit;

        main.Closing += (_, e) =>
        {
            // El apagado de Windows o un Shutdown() ya en marcha siempre salen: no se cancela el apagado.
            var exitRequested = exiting
                || e.CloseReason is WindowCloseReason.OSShutdown or WindowCloseReason.ApplicationShutdown;
            if (CloseBehavior.Decide(viewModel.Settings.KeepRunningOnClose, tray is not null, exitRequested) != CloseAction.Hide)
                return;

            e.Cancel = true;
            if (viewModel.IsImmersive) viewModel.CloseImmersiveCommand.Execute(null);
            main.Hide();
        };
        main.Closed += (_, _) => Exit();

        main.PropertyChanged += (_, e) =>
        {
            if (e.Property != Visual.IsVisibleProperty || exiting) return;

            var hidden = !main.IsVisible;
            var wasHidden = viewModel.IsMainWindowHidden; // al arrancar la ventana también «se hace visible»: eso no cuenta
            viewModel.IsMainWindowHidden = hidden;
            if (tray is not null) tray.IsVisible = hidden;

            // La primera vez que vuelve de la bandeja, se explica cómo cerrar del todo (una vez por sesión).
            if (!hidden && wasHidden && !explained)
            {
                explained = true;
                viewModel.Status = "El reproductor siguió abierto en la bandeja. Para cerrarlo del todo: clic derecho en su icono y «Salir»";
            }
        };
    }

    private TrayIcon CreateTray(MainViewModel viewModel, Action open, Action exit)
    {
        var playPause = new NativeMenuItem(viewModel.IsPlaying ? "Pausar" : "Reproducir");
        playPause.Click += (_, _) => viewModel.TogglePlayPauseCommand.Execute(null);
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.IsPlaying))
                playPause.Header = viewModel.IsPlaying ? "Pausar" : "Reproducir";
        };

        var openItem = new NativeMenuItem("Abrir");
        openItem.Click += (_, _) => open();
        var next = new NativeMenuItem("Siguiente");
        next.Click += (_, _) => viewModel.NextCommand.Execute(null);
        var exitItem = new NativeMenuItem("Salir");
        exitItem.Click += (_, _) => exit();

        using var icon = AssetLoader.Open(new Uri("avares://MusicPlayer.Desktop/Assets/app.ico"));
        var tray = new TrayIcon
        {
            Icon = new WindowIcon(icon),
            ToolTipText = "Music Player",
            IsVisible = false,
            Menu = new NativeMenu { openItem, playPause, next, new NativeMenuItemSeparator(), exitItem },
        };
        tray.Clicked += (_, _) => open();
        TrayIcon.SetIcons(this, new TrayIcons { tray });
        return tray;
    }
}