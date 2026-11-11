using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Services;

public sealed class LibraryScanner(ITrackMetadataReader reader)
{
    public static readonly IReadOnlySet<string> SupportedExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mp3", ".flac", ".wav", ".m4a" };

    /// <summary>
    /// Temporales de la propia app (<c>~tags-*</c> de las ediciones, <c>~conv-*</c> de la conversión): pueden quedar
    /// tras un cierre brusco o estar a medio escribir, y no son música (docs/specs/library-data-safety.md).
    /// </summary>
    public static bool IsTemporary(string path)
    {
        var name = Path.GetFileName(path);
        return name.StartsWith("~tags-", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("~conv-", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Escanea en segundo plano para no congelar la UI.</summary>
    public Task<IReadOnlyList<Track>> ScanAsync(
        string rootFolder,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        return Task.Run<IReadOnlyList<Track>>(() =>
        {
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true
            };

            var tracks = new List<Track>();
            foreach (var path in Directory.EnumerateFiles(rootFolder, "*.*", options))
            {
                ct.ThrowIfCancellationRequested();

                if (!SupportedExtensions.Contains(Path.GetExtension(path)) || IsTemporary(path))
                    continue;

                var track = SafeRead(path);
                if (track is null) continue;

                tracks.Add(track);
                progress?.Report(tracks.Count);
            }
            return tracks;
        }, ct);
    }

    /// <summary>Lee los metadatos de archivos concretos, sin recorrer carpetas.</summary>
    public Task<IReadOnlyList<Track>> ReadFilesAsync(IEnumerable<string> paths, CancellationToken ct = default) =>
        Task.Run<IReadOnlyList<Track>>(() => paths.Select(SafeRead).OfType<Track>().ToList(), ct);

    // El lector no debería lanzar, pero las etiquetas son una entrada externa: un archivo que lo haga lanzar se
    // omite y los demás entran (docs/specs/untrusted-input-limits.md, criterio 7).
    private Track? SafeRead(string path)
    {
        try
        {
            return reader.Read(path);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }
}