using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using MusicPlayer.Core.Services;

namespace MusicPlayer.Desktop.Services;

// El selector se abre dentro de ModalTracker.Begin(): mientras está abierto, la isla no aparece.
public sealed class AvaloniaLyricsPickerService(ModalTracker modals) : ILyricsPickerService
{
    public async Task<string?> PickLyricsAsync(string title)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime
            { MainWindow: { } owner })
            return null;

        IReadOnlyList<IStorageFile> files;
        using (modals.Begin())
        {
            files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Letras (.lrc o .txt)") { Patterns = new[] { "*.lrc", "*.txt" } }
                }
            });
        }

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }
}