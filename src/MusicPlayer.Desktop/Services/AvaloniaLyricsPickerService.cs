using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace MusicPlayer.Desktop.Services;

public sealed class AvaloniaLyricsPickerService : ILyricsPickerService
{
    public async Task<string?> PickLyricsAsync(string title)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime
            { MainWindow: { } owner })
            return null;

        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Letras (.lrc o .txt)") { Patterns = new[] { "*.lrc", "*.txt" } }
            }
        });

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }
}