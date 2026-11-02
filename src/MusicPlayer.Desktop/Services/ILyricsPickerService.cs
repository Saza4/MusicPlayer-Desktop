using System.Threading.Tasks;

namespace MusicPlayer.Desktop.Services;

public interface ILyricsPickerService
{
    /// <summary>Ruta del archivo de letra elegido (.lrc o .txt), o null si el usuario cancela.</summary>
    Task<string?> PickLyricsAsync(string title);
}