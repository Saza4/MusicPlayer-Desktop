namespace MusicPlayer.Infrastructure;

/// <summary>%APPDATA%\MusicPlayer en Windows, ~/.config/MusicPlayer en Linux.</summary>
public static class AppPaths
{
    public static string DataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MusicPlayer");

    public static string Settings => Path.Combine(DataDirectory, "settings.json");
    public static string Playlists => Path.Combine(DataDirectory, "playlists.json");
    public static string Library => Path.Combine(DataDirectory, "library.json");

    /// <summary>Registro de errores inesperados (texto, hasta 256 KB).</summary>
    public static string Errors => Path.Combine(DataDirectory, "errores.log");

    /// <summary>Carpeta de los temas de usuario (archivos <c>.json</c>).</summary>
    public static string Themes => Path.Combine(DataDirectory, "themes");
}