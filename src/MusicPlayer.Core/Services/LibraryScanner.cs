using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Services;

public sealed class LibraryScanner(ITrackMetadataReader reader)
{
    public static readonly IReadOnlySet<string> SupportedExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mp3", ".flac", ".wav", ".m4a" };

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

                if (!SupportedExtensions.Contains(Path.GetExtension(path)))
                    continue;

                var track = reader.Read(path);
                if (track is null) continue;

                tracks.Add(track);
                progress?.Report(tracks.Count);
            }
            return tracks;
        }, ct);
    }
}