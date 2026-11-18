using System.Globalization;

namespace MusicPlayer.Desktop.ViewModels;

public static class Plural
{
    /// <summary>«1 canción», «1.284 canciones»: el número lleva el separador de miles del idioma del sistema.</summary>
    public static string Of(int count, string one, string many) =>
        count == 1 ? $"1 {one}" : $"{count.ToString("N0", CultureInfo.CurrentCulture)} {many}";
}
