using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using MusicPlayer.Core.Services;
using MusicPlayer.Desktop.Services;
using MusicPlayer.Desktop.ViewModels;
using MusicPlayer.Desktop.Views;

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

            // El tema elegido se aplica antes de mostrar la ventana, para que no parpadee en Oscuro.
            viewModel.Themes.Initialize();

            desktop.MainWindow = new MainWindow { DataContext = viewModel };

            // La isla es una segunda ventana: la app termina al cerrar la principal, no la última.
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            services.GetRequiredService<IslandController>().Attach(desktop.MainWindow);

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
}