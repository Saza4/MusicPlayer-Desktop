using System.Text.Json;

namespace MusicPlayer.Infrastructure;

internal static class AtomicJson
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    /// <summary>Null si el archivo no existe o está corrupto (en ese caso queda como .corrupt).</summary>
    public static async Task<T?> ReadAsync<T>(string path, CancellationToken ct) where T : class
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
            File.Move(path, path + ".corrupt", overwrite: true);
            return null;
        }
    }

    /// <summary>Escribe a un temporal y luego mueve: un cierre a medias no corrompe el archivo.</summary>
    public static async Task WriteAsync<T>(string path, T value, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);

        var tempPath = path + ".tmp";
        await using (var stream = File.Create(tempPath))
            await JsonSerializer.SerializeAsync(stream, value, Options, ct);

        File.Move(tempPath, path, overwrite: true);
    }
}