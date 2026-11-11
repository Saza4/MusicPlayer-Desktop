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

    // ---- Temporales abandonados (docs/specs/library-data-safety-2.md, criterio 7) ----

    private const string Hex = "0123456789abcdef0123456789ABCDEF";

    private string TouchAged(string relativePath, double hoursOld)
    {
        Touch(relativePath);
        var full = Path.Combine(_root, relativePath);
        File.SetLastWriteTimeUtc(full, DateTime.UtcNow.AddHours(-hoursOld));
        return full;
    }

    [Fact]
    public async Task CleanStaleTemporaries_DeletesOldOnes_IncludingSubfolders_AndCountsThem()
    {
        var conv = TouchAged($"~conv-{Hex}.mp3", 25);
        var tags = TouchAged($"sub/~tags-{Hex}.flac", 72);
        var song = TouchAged("cancion.mp3", 72);

        var removed = await new LibraryScanner(new FakeReader()).CleanStaleTemporariesAsync(_root);

        Assert.Equal(2, removed);
        Assert.False(File.Exists(conv));
        Assert.False(File.Exists(tags));
        Assert.True(File.Exists(song));
    }

    [Fact]
    public async Task CleanStaleTemporaries_KeepsRecentOnes()
    {
        var recent = TouchAged($"~conv-{Hex}.mp3", 1);

        Assert.Equal(0, await new LibraryScanner(new FakeReader()).CleanStaleTemporariesAsync(_root));
        Assert.True(File.Exists(recent));
    }

    [Theory]
    [InlineData("~tags-notas.mp3")]                             // no es un Guid
    [InlineData("~conv-0123456789abcdef0123456789abcdef.txt")]  // no es audio
    [InlineData("~conv-0123456789abcdef0123456789abcde.mp3")]   // 31 dígitos
    [InlineData("~conv-0123456789abcdef0123456789abcdef0.mp3")] // 33 dígitos
    [InlineData("~otro-0123456789abcdef0123456789abcdef.mp3")]  // otro prefijo
    [InlineData("conv-0123456789abcdef0123456789abcdef.mp3")]   // sin la tilde
    public async Task CleanStaleTemporaries_KeepsOldFilesThatAreNotOurs(string name)
    {
        var path = TouchAged(name, 72);

        Assert.Equal(0, await new LibraryScanner(new FakeReader()).CleanStaleTemporariesAsync(_root));
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void IsStaleTemporary_NeedsMoreThanADay_AndIgnoresFutureDates()
    {
        var now = new DateTime(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);
        var path = $"~tags-{Hex}.m4a";

        Assert.True(LibraryScanner.IsStaleTemporary(path, now.AddHours(-24).AddSeconds(-1), now));
        Assert.False(LibraryScanner.IsStaleTemporary(path, now.AddHours(-24), now));
        Assert.False(LibraryScanner.IsStaleTemporary(path, now.AddDays(3), now));
    }

    [Fact]
    public async Task ScanFolder_ReportsTheTemporariesItRemoved()
    {
        Touch("a.mp3");
        TouchAged($"~conv-{Hex}.mp3", 30);
        var service = new LibraryService(new LibraryScanner(new FakeReader()), new NullRepository());

        var result = await service.ScanFolderAsync(_root);

        Assert.Equal(1, result.TemporariesRemoved);
        Assert.Single(result.Snapshot.Tracks);
    }

    private sealed class NullRepository : ILibraryRepository
    {
        public Task<LibrarySnapshot> LoadAsync(CancellationToken ct = default) => Task.FromResult(LibrarySnapshot.Empty);
        public Task SaveAsync(LibrarySnapshot snapshot, CancellationToken ct = default) => Task.CompletedTask;
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}