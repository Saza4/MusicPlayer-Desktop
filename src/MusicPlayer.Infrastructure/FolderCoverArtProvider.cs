using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;

namespace MusicPlayer.Infrastructure;

/// <summary>Busca cover/folder/front/album/albumart (.jpg, .jpeg, .png) junto al audio.</summary>
public sealed class FolderCoverArtProvider : ICoverArtProvider
{
    // El orden define la prioridad.
    private static readonly string[] Names = { "cover", "folder", "front", "album", "albumart" };
    private static readonly string[] Extensions = { ".jpg", ".jpeg", ".png" };

    public async Task<byte[]?> GetCoverAsync(Track track, CancellationToken ct = default)
    {
        var directory = Path.GetDirectoryName(track.FilePath);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            return null;

        try
        {
            // Mismo tope que al elegir una carátula: una imagen mayor se trata como si no estuviera y se pasa a la
            // siguiente candidata. El tamaño se mira antes de abrir (docs/specs/audit-followup.md, criterio 6).
            var file = Directory.EnumerateFiles(directory)
                .Where(f => Extensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase)
                            && Names.Contains(Path.GetFileNameWithoutExtension(f), StringComparer.OrdinalIgnoreCase))
                .OrderBy(f => Array.FindIndex(Names, n =>
                    n.Equals(Path.GetFileNameWithoutExtension(f), StringComparison.OrdinalIgnoreCase)))
                .FirstOrDefault(f => new FileInfo(f).Length <= CoverImage.MaxBytes);

            return file is null ? null : await File.ReadAllBytesAsync(file, ct);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}