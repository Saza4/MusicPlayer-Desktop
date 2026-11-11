using System.Text.RegularExpressions;
using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Services;

public sealed partial class LibraryScanner(ITrackMetadataReader reader)
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

    /// <summary>Antigüedad a partir de la cual un temporal de la app se da por abandonado.</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromHours(24);

    // Exactamente como los nombra la app: el prefijo y un Guid en formato "N".
    [GeneratedRegex("^~(?:tags|conv)-[0-9a-fA-F]{32}$")]
    private static partial Regex OwnTemporaryName();

    /// <summary>
    /// True si el archivo es un temporal de la app abandonado y se puede borrar (docs/specs/library-data-safety-2.md,
    /// criterio 7): el nombre exacto que genera la app, extensión de audio y más de 24 horas sin modificarse. El
    /// patrón es estrecho a propósito: se borra un archivo de la carpeta de música del usuario.
    /// </summary>
    public static bool IsStaleTemporary(string path, DateTime lastWriteUtc, DateTime nowUtc) =>
        SupportedExtensions.Contains(Path.GetExtension(path))
        && OwnTemporaryName().IsMatch(Path.GetFileNameWithoutExtension(path))
        && nowUtc - lastWriteUtc > StaleAfter;

    /// <summary>
    /// Borra los temporales abandonados de la carpeta y sus subcarpetas, y devuelve cuántos. Uno que no se pueda
    /// borrar se deja donde está.
    /// </summary>
    public Task<int> CleanStaleTemporariesAsync(string rootFolder, CancellationToken ct = default) =>
        Task.Run(() =>
        {
            var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };
            var now = DateTime.UtcNow;
            var removed = 0;
            foreach (var path in Directory.EnumerateFiles(rootFolder, "~*", options))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    if (!IsStaleTemporary(path, File.GetLastWriteTimeUtc(path), now)) continue;
                    File.Delete(path);
                    removed++;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // En uso o sin permiso: se queda.
                }
            }
            return removed;
        }, ct);

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