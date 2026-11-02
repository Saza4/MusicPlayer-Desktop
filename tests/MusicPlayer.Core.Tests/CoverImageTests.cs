using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Tests;

public class CoverImageTests
{
    [Fact]
    public void Jpeg_IsDetected() =>
        Assert.Equal("image/jpeg", CoverImage.DetectMime(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0 }));

    [Fact]
    public void Png_IsDetected() =>
        Assert.Equal("image/png", CoverImage.DetectMime(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1 }));

    [Fact]
    public void OtherFormats_AreRejected() =>
        Assert.Null(CoverImage.DetectMime(new byte[] { 0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0 })); // WebP/RIFF

    [Fact]
    public void TooShortOrEmpty_IsRejected()
    {
        Assert.Null(CoverImage.DetectMime(Array.Empty<byte>()));
        Assert.Null(CoverImage.DetectMime(new byte[] { 0xFF, 0xD8 }));
    }
}