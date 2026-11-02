using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;

namespace MusicPlayer.Core.Tests;

public class ConversionTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("conversion-tests").FullName;

    private sealed class FakeConverter : IAudioConverter
    {
        public bool IsAvailable => true;
        public HashSet<string> Failing { get; } = new();

        public Task ConvertAsync(string input, string output, ConversionOptions options, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (Failing.Contains(Path.GetFileName(input))) throw new IOException("ffmpeg falló");
            File.WriteAllText(output, ""); // el archivo existe a partir de aquí, como con el conversor real
            return Task.CompletedTask;
        }
    }

    private Track Make(string name)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, "");
        return new Track { FilePath = path, Title = name };
    }

    private static ConversionOptions Mp3 => new(AudioFormat.Mp3);

    [Fact]
    public void OutputPath_SameFolderWithTheNewExtension() =>
        Assert.Equal(Path.Combine("m", "a.mp3"), ConversionPlanner.OutputPath(Path.Combine("m", "a.flac"), AudioFormat.Mp3, _ => false));

    [Fact]
    public void OutputPath_WhenItExists_AddsACounterUntilFree()
    {
        var taken = new HashSet<string> { Path.Combine("m", "a.mp3"), Path.Combine("m", "a (1).mp3") };

        Assert.Equal(Path.Combine("m", "a (2).mp3"),
            ConversionPlanner.OutputPath(Path.Combine("m", "a.flac"), AudioFormat.Mp3, taken.Contains));
    }

    [Fact]
    public void IsAlreadyFormat_IgnoresCase() =>
        Assert.True(ConversionPlanner.IsAlreadyFormat("a.MP3", AudioFormat.Mp3));

    [Fact]
    public async Task Convert_CreatesTheFileNextToTheSourceAndKeepsTheOriginal()
    {
        var track = Make("a.flac");

        var result = await new ConversionService(new FakeConverter()).ConvertAsync(new[] { track }, Mp3);

        Assert.Equal(Path.Combine(_dir, "a.mp3"), Assert.Single(result.Created));
        Assert.True(File.Exists(track.FilePath));
    }

    [Fact]
    public async Task Convert_SkipsTracksAlreadyInTheTargetFormat()
    {
        var result = await new ConversionService(new FakeConverter()).ConvertAsync(new[] { Make("a.mp3") }, Mp3);

        Assert.Empty(result.Created);
        Assert.Equal(1, result.Skipped);
    }

    [Fact]
    public async Task Convert_NeverOverwritesAnExistingFile()
    {
        Make("a.mp3");
        var source = Make("a.flac");

        var result = await new ConversionService(new FakeConverter()).ConvertAsync(new[] { source }, Mp3);

        Assert.Equal(Path.Combine(_dir, "a (1).mp3"), Assert.Single(result.Created));
    }

    [Fact]
    public async Task Convert_KeepsGoingAfterAFailure()
    {
        var converter = new FakeConverter();
        converter.Failing.Add("a.flac");

        var result = await new ConversionService(converter)
            .ConvertAsync(new[] { Make("a.flac"), Make("b.flac") }, Mp3);

        Assert.Equal(Path.Combine(_dir, "b.mp3"), Assert.Single(result.Created));
        Assert.Contains("a.flac", Assert.Single(result.Errors));
    }

    [Fact]
    public async Task Convert_CancelledBeforeStart_DoesNothing()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await new ConversionService(new FakeConverter())
            .ConvertAsync(new[] { Make("a.flac") }, Mp3, ct: cts.Token);

        Assert.Empty(result.Created);
        Assert.True(result.Cancelled);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);
}