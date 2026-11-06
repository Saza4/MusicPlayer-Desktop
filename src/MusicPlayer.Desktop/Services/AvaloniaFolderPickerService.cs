using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using MusicPlayer.Core.Services;

namespace MusicPlayer.Desktop.Services;

// El selector se abre dentro de ModalTracker.Begin(): mientras está abierto, la isla no aparece.
public sealed class AvaloniaFolderPickerService(ModalTracker modals) : IFolderPickerService
{
    public async Task<string?> PickFolderAsync()
    {
        // La ventana se busca al usar el servicio, no al construirlo: así el contenedor
        // no necesita que la ventana exista antes que el ViewModel.
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime
            { MainWindow: { } owner })
            return null;

        IReadOnlyList<IStorageFolder> folders;
        using (modals.Begin())
        {
            folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Selecciona tu carpeta de música",
                AllowMultiple = false
            });
        }

        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }
}