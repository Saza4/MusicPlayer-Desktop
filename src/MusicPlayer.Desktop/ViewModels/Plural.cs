namespace MusicPlayer.Desktop.ViewModels;

public static class Plural
{
    public static string Of(int count, string one, string many) =>
        count == 1 ? $"1 {one}" : $"{count} {many}";
}