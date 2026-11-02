namespace MusicPlayer.Core.Models;

public static class CoverImage
{
    public const int MaxBytes = 10 * 1024 * 1024;

    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    /// <summary>"image/jpeg" o "image/png" según los primeros bytes; null si no es ninguno.</summary>
    public static string? DetectMime(ReadOnlySpan<byte> data)
    {
        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
            return "image/jpeg";
        if (data.Length >= 8 && data[..8].SequenceEqual(PngSignature))
            return "image/png";
        return null;
    }
}