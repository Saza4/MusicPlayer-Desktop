using System.Collections.Concurrent;
using System.Text.Json;

namespace MusicPlayer.Infrastructure;

internal static class AtomicJson
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    // Un candado por archivo: en Windows, dos File.Move con sobrescritura sobre el mismo destino chocan
    // (UnauthorizedAccessException). docs/specs/library-data-safety-2.md, criterio 3.
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>El candado de escritura de ese archivo. Lo comparte <see cref="JsonLibraryRepository"/>.</summary>
    public static SemaphoreSlim GateFor(string path) =>
        Gates.GetOrAdd(Path.GetFullPath(path), _ => new SemaphoreSlim(1, 1));

    /// <summary>Null si el archivo no existe o está corrupto (en ese caso queda como respaldo, ver <see cref="CorruptBackup"/>).</summary>
    public static async Task<T?> ReadAsync<T>(string path, CancellationToken ct, Func<DateTimeOffset>? clock = null) where T : class
    {
        if (!File.Exists(path))
            return null;

        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<T>(stream, Options, ct);
        }
        catch (JsonException)
        {
            CorruptBackup.Move(path, clock);
            return null;
        }
    }

    /// <summary>Escribe a un temporal y luego mueve: un cierre a medias no corrompe el archivo.</summary>
    public static async Task WriteAsync<T>(string path, T value, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);

        var gate = GateFor(path);
        await gate.WaitAsync(ct);
        try
        {
            // Un temporal único por escritura, que no queda atrás si algo falla.
            var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
            try
            {
                await using (var stream = File.Create(tempPath))
                    await JsonSerializer.SerializeAsync(stream, value, Options, ct);

                File.Move(tempPath, path, overwrite: true);
            }
            finally
            {
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch (IOException) { }
            }
        }
        finally
        {
            gate.Release();
        }
    }
}
