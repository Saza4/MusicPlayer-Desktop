namespace MusicPlayer.Infrastructure;

internal static class SafeFileEdit
{
    /// <summary>
    /// Edita una copia temporal y, si todo sale bien, la intercambia por el original.
    /// Si algo falla, el original queda intacto.
    /// </summary>
    public static void Run(string filePath, Action<string> editCopy)
    {
        if (new FileInfo(filePath).IsReadOnly)
            throw new UnauthorizedAccessException("El archivo es de solo lectura.");

        // La copia conserva la extensión: TagLib decide el formato por ella.
        var directory = Path.GetDirectoryName(Path.GetFullPath(filePath))!;
        var temp = Path.Combine(directory, $"~tags-{Guid.NewGuid():N}{Path.GetExtension(filePath)}");

        try
        {
            File.Copy(filePath, temp);
            // La copia hereda la fecha del original: sin esto, el temporal de un archivo antiguo parece abandonado
            // (más de 24 h) y un escaneo simultáneo podría borrarlo (docs/specs/audit-3.md, H7).
            try { File.SetLastWriteTimeUtc(temp, DateTime.UtcNow); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            editCopy(temp);
            File.Replace(temp, filePath, destinationBackupFileName: null);
        }
        finally
        {
            // Si el borrado falla (el antivirus retiene el temporal), no debe tapar el error de la edición. El
            // temporal que quede se limpia al escanear la carpeta pasadas 24 h.
            try { if (File.Exists(temp)) File.Delete(temp); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }
}