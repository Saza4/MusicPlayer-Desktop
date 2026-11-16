using Avalonia;
using System;
using MusicPlayer.Desktop.Services;

namespace MusicPlayer.Desktop;

sealed class Program
{
    /// <summary>La instancia única de este proceso (solo Windows; null en Linux o si no se pudo crear).</summary>
    public static SingleInstance? Instance { get; private set; }

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Instancia única (docs/specs/background-playback.md): si ya hay otra, se le avisa y esta termina sin
        // cargar nada. Va antes de arrancar Avalonia.
        if (OperatingSystem.IsWindows())
        {
            Instance = SingleInstance.TryAcquire(out var otherRunning);
            if (otherRunning) return;
        }

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            if (OperatingSystem.IsWindows()) Instance?.Dispose();
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
