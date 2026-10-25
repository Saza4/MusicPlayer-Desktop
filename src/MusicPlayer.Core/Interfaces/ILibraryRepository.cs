using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Interfaces;

public interface ILibraryRepository
{
    /// <summary>Devuelve <see cref="LibrarySnapshot.Empty"/> si aún no hay datos guardados.</summary>
    Task<LibrarySnapshot> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(LibrarySnapshot snapshot, CancellationToken ct = default);
}