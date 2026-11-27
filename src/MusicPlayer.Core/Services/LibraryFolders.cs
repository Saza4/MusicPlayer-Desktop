namespace MusicPlayer.Core.Services;

/// <summary>
/// Lista de carpetas agregadas sin duplicados ni solapes (docs/specs/auto-refresh-folders.md, criterios 2 y 3): una
/// carpeta dentro de otra ya agregada no se añade, y una que contiene a otras las sustituye. Así cada archivo se
/// revisa una sola vez.
/// </summary>
public static class LibraryFolders
{
    /// <summary>Ruta absoluta y sin separador final (salvo la raíz de la unidad, que lo conserva).</summary>
    public static string Normalize(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    /// <summary>True si <paramref name="path"/> es <paramref name="folder"/> o está dentro de ella.</summary>
    public static bool Contains(string folder, string path) => Contains(folder, path, FilePaths.Comparer);

    public static bool Contains(string folder, string path, StringComparer comparer)
    {
        if (comparer.Equals(folder, path)) return true;
        var prefix = Path.EndsInDirectorySeparator(folder) ? folder : folder + Path.DirectorySeparatorChar;
        return path.Length > prefix.Length && comparer.Equals(path[..prefix.Length], prefix);
    }

    public static IReadOnlyList<string> Add(IReadOnlyList<string> folders, string folder) =>
        Add(folders, folder, FilePaths.Comparer);

    public static IReadOnlyList<string> Add(IReadOnlyList<string> folders, string folder, StringComparer comparer)
    {
        var added = Normalize(folder);
        if (folders.Any(f => Contains(f, added, comparer)))
            return folders;

        return folders.Where(f => !Contains(added, f, comparer)).Append(added).ToList();
    }

    /// <summary>
    /// Lo leído de <c>library.json</c>, que pudo editarse a mano: se descartan las entradas vacías, relativas o
    /// inválidas, y se quitan duplicados y solapes. Nunca lanza.
    /// </summary>
    public static IReadOnlyList<string> Clean(IEnumerable<string?> folders)
    {
        IReadOnlyList<string> result = Array.Empty<string>();
        foreach (var folder in folders)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Path.IsPathFullyQualified(folder)) continue;
            try { result = Add(result, folder); }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                // Ruta con caracteres no válidos: se ignora.
            }
        }
        return result;
    }
}
