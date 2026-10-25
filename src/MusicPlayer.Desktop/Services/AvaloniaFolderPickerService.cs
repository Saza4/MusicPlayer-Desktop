using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace MusicPlayer.Desktop.Services;

public sealed class AvaloniaFolderPickerService(TopLevel owner) : IFolderPickerService
{
    public async Task<string?> PickFolderAsync()
    {
        var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Selecciona tu carpeta de música",
            AllowMultiple = false
        });

        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }
}