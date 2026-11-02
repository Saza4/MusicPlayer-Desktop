using System.Threading.Tasks;

namespace MusicPlayer.Desktop.Services;

public interface IImagePickerService
{
    /// <summary>Ruta de la imagen elegida, o null si el usuario cancela.</summary>
    Task<string?> PickImageAsync();
}