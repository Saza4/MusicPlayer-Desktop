using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace MusicPlayer.Desktop.Services;

public sealed class AvaloniaImagePickerService : IImagePickerService
{
    public async Task<string?> PickImageAsync(string title = "Elige la carátula")
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
                new FilePickerFileType("Imágenes JPG o PNG")
                {
                    Patterns = new[] { "*.jpg", "*.jpeg", "*.png" },
                    MimeTypes = new[] { "image/jpeg", "image/png" }
                }
            }
        });

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }
}