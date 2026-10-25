using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;

namespace MusicPlayer.Core.Tests;

public class LibraryScannerTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("scanner-tests").FullName;

    private sealed class FakeReader : ITrackMetadataReader
    {
        public Track? Read(string filePath) =>
            filePath.Contains("corrupt") ? null
            : new Track { FilePath = filePath, Title = Path.GetFileNameWithoutExtension(filePath) };
    }

    private void Touch(string relativePath)
    {
        var full = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, "");
    }

    [Fact]
    public async Task ScanAsync_OnlyReturnsSupportedFiles_IncludingSubfolders()
    {
        Touch("a.mp3");
        Touch("sub/b.FLAC");
        Touch("notes.txt");
        Touch("cover.jpg");

        var tracks = await new LibraryScanner(new FakeReader()).ScanAsync(_root);

        Assert.Equal(2, tracks.Count);
    }

    [Fact]
    public async Task ScanAsync_SkipsFilesTheReaderRejects()
    {
        Touch("ok.mp3");
        Touch("corrupt.mp3");

        var tracks = await new LibraryScanner(new FakeReader()).ScanAsync(_root);

        Assert.Single(tracks);
        Assert.Equal("ok", tracks[0].Title);
    }

    [Fact]
    public async Task ScanAsync_CancelledToken_Throws()
    {
        Touch("a.mp3");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new LibraryScanner(new FakeReader()).ScanAsync(_root, ct: cts.Token));
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}