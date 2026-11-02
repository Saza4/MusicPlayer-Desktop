using MusicPlayer.Core.Models;
using MusicPlayer.Infrastructure;

namespace MusicPlayer.Infrastructure.Tests;

public class FfmpegArgumentsTests
{
    private static IReadOnlyList<string> Args(AudioFormat format, int kbps = 192) =>
        FfmpegAudioConverter.BuildArguments("in.flac", "out.x", new ConversionOptions(format, kbps));

    [Fact]
    public void Mp3_UsesLameWithTheBitrateAndCopiesTheCover()
    {
        var args = Args(AudioFormat.Mp3, 256);

        Assert.Contains("libmp3lame", args);
        Assert.Contains("256k", args);
        Assert.Contains("0:v:0?", args);
        Assert.Equal("out.x", args[^1]);
    }

    [Fact]
    public void Wav_DropsTheCover()
    {
        var args = Args(AudioFormat.Wav);

        Assert.Contains("-vn", args);
        Assert.DoesNotContain("0:v:0?", args);
    }

    [Fact]
    public void Flac_IsLosslessWithoutBitrate()
    {
        var args = Args(AudioFormat.Flac);

        Assert.Contains("flac", args);
        Assert.DoesNotContain("-b:a", args);
    }
}