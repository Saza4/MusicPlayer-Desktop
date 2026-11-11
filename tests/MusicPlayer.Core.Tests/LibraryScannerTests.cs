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

    // docs/specs/untrusted-input-limits.md, criterio 7: un archivo que hace lanzar al lector no tumba el escaneo.
    private sealed class ThrowingReader : ITrackMetadataReader
    {
        public Track? Read(string filePath) =>
            filePath.Contains("explota")
                ? throw new ArgumentOutOfRangeException("count")
                : new Track { FilePath = filePath, Title = Path.GetFileNameWithoutExtension(filePath) };
    }

    [Fact]
    public async Task ScanAsync_ReaderThrows_SkipsThatFileAndKeepsTheRest()
    {
        Touch("a.mp3");
        Touch("explota.flac");
        Touch("b.mp3");

        var tracks = await new LibraryScanner(new ThrowingReader()).ScanAsync(_root);

        Assert.Equal(new[] { "a", "b" }, tracks.Select(t => t.Title).Order());
    }

    [Fact]
    public async Task ReadFilesAsync_ReaderThrows_SkipsThatFileAndKeepsTheRest()
    {
        var paths = new[] { "a.mp3", "explota.flac", "b.mp3" }.Select(n => Path.Combine(_root, n));

        var tracks = await new LibraryScanner(new ThrowingReader()).ReadFilesAsync(paths);

        Assert.Equal(new[] { "a", "b" }, tracks.Select(t => t.Title));
    }

    [Fact]
    public async Task ScanAsync_ReaderThrowsOnEveryFile_ReturnsNothingWithoutFailing()
    {
        Touch("explota1.mp3");
        Touch("explota2.mp3");

        Assert.Empty(await new LibraryScanner(new ThrowingReader()).ScanAsync(_root));
    }

    private sealed class CancellingReader : ITrackMetadataReader
    {
        public Track? Read(string filePath) => throw new OperationCanceledException();
    }

    [Fact]
    public async Task ScanAsync_CancellationInsideTheReader_IsNotSwallowed()
    {
        Touch("a.mp3");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new LibraryScanner(new CancellingReader()).ScanAsync(_root));
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}