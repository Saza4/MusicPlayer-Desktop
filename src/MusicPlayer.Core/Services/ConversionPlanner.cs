using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Services;

public static class ConversionPlanner
{
    public static string Extension(AudioFormat format) => format switch
    {
        AudioFormat.Mp3 => ".mp3",
        AudioFormat.Flac => ".flac",
        AudioFormat.Wav => ".wav",
        AudioFormat.M4a => ".m4a",
        _ => throw new ArgumentOutOfRangeException(nameof(format))
    };

    public static bool IsAlreadyFormat(string path, AudioFormat format) =>
        string.Equals(Path.GetExtension(path), Extension(format), StringComparison.OrdinalIgnoreCase);

    /// <summary>Junto al original, con la nueva extensión. Si ya existe, añade " (1)", " (2)"… Nunca sobrescribe.</summary>
    public static string OutputPath(string source, AudioFormat format, Func<string, bool> exists)
    {
        var directory = Path.GetDirectoryName(source) ?? "";
        var name = Path.GetFileNameWithoutExtension(source);
        var extension = Extension(format);

        var candidate = Path.Combine(directory, name + extension);
        for (var i = 1; exists(candidate); i++)
            candidate = Path.Combine(directory, $"{name} ({i}){extension}");
        return candidate;
    }
}