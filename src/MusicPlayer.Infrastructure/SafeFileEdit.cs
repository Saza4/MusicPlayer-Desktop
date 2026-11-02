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
            editCopy(temp);
            File.Replace(temp, filePath, destinationBackupFileName: null);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }
}