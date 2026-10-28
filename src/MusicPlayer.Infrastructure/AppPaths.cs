namespace MusicPlayer.Infrastructure;

/// <summary>%APPDATA%\MusicPlayer en Windows, ~/.config/MusicPlayer en Linux.</summary>
public static class AppPaths
{
    public static string DataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MusicPlayer");

    public static string Settings => Path.Combine(DataDirectory, "settings.json");
    public static string Playlists => Path.Combine(DataDirectory, "playlists.json");
    public static string Library => Path.Combine(DataDirectory, "library.json");
}