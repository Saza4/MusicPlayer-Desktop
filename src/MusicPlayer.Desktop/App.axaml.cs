using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using MusicPlayer.Core.Services;
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
            // Si tu plantilla incluye esta llamada, consérvala aquí arriba:
            // DisableAvaloniaDataAnnotationValidation();

            var services = CompositionRoot.Build();
            var viewModel = services.GetRequiredService<MainViewModel>();
            desktop.MainWindow = new MainWindow { DataContext = viewModel };

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