using System.Threading.Tasks;

namespace MusicPlayer.Desktop.Services;

public interface IFolderPickerService
{
    /// <summary>Ruta de la carpeta elegida, o null si el usuario cancela.</summary>
    Task<string?> PickFolderAsync();
}