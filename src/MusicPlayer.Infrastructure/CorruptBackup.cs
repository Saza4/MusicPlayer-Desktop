using System.Globalization;

namespace MusicPlayer.Infrastructure;

/// <summary>
/// Respaldo de un JSON de usuario dañado (docs/specs/library-data-safety-2.md, criterio 5): cada uno con su fecha,
/// para que un segundo archivo dañado no pise el respaldo del primero.
/// </summary>
internal static class CorruptBackup
{
    /// <summary>Respaldos que se conservan por archivo; los más antiguos se borran.</summary>
    public const int Keep = 5;

    /// <summary>Mueve el archivo a <c>nombre.corrupt-aaaaMMdd-HHmmss</c> y devuelve esa ruta.</summary>
    public static string Move(string path, Func<DateTimeOffset>? clock = null)
    {
        var stamp = (clock?.Invoke() ?? DateTimeOffset.Now).ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var prefix = $"{path}.corrupt-";

        // Dos en el mismo segundo: sufijo de dos cifras, que conserva el orden alfabético.
        var backup = prefix + stamp;
        for (var n = 2; File.Exists(backup); n++)
            backup = $"{prefix}{stamp}-{n:00}";

        File.Move(path, backup);
        Prune(prefix);
        return backup;
    }

    // El nombre lleva la fecha, así que el orden alfabético es el cronológico. Un «.corrupt» a secas, de versiones
    // anteriores, no coincide con el patrón y no se toca.
    private static void Prune(string prefix)
    {
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(prefix))!;
            var old = Directory.GetFiles(directory, Path.GetFileName(prefix) + "*")
                .OrderByDescending(f => f, StringComparer.Ordinal)
                .Skip(Keep);
            foreach (var file in old)
                File.Delete(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Limpiar respaldos viejos es un extra: el respaldo nuevo ya está hecho.
        }
    }
}
