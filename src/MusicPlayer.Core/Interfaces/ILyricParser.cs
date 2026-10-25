using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Interfaces;

public interface ILyricParser
{
    /// <summary>Parsea contenido .lrc ([mm:ss.xx]) o texto plano.</summary>
    LyricBlock Parse(string content);
}