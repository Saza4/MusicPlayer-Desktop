namespace MusicPlayer.Core.Services;

/// <summary>
/// Cómo se comparan dos rutas de archivo (docs/specs/library-data-safety-2.md, criterio 8): en Linux «A.mp3» y
/// «a.mp3» son archivos distintos; en Windows y macOS, el mismo.
/// </summary>
public static class FilePaths
{
    /// <summary>El comparador del sistema en el que corre la app.</summary>
    public static StringComparer Comparer { get; } =
        For(caseSensitive: !(OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()));

    public static StringComparer For(bool caseSensitive) =>
        caseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
}
