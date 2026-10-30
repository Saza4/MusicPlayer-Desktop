using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;

namespace MusicPlayer.Core.Tests;

public class LibraryServiceTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("library-tests").FullName;

    private sealed class FakeReader : ITrackMetadataReader
    {
        // Genera un Id nuevo en cada lectura, como el lector real.
        public Track? Read(string filePath) =>
            new() { FilePath = filePath, Title = Path.GetFileNameWithoutExtension(filePath) };
    }

    private sealed class InMemoryRepository : ILibraryRepository
    {
        public LibrarySnapshot Stored { get; set; } = LibrarySnapshot.Empty;
        public int SaveCount { get; private set; }

        public Task<LibrarySnapshot> LoadAsync(CancellationToken ct = default) => Task.FromResult(Stored);

        public Task SaveAsync(LibrarySnapshot snapshot, CancellationToken ct = default)
        {
            Stored = snapshot;
            SaveCount++;
            return Task.CompletedTask;
        }
    }

    private string Touch(string relativePath)
    {
        var full = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, "");
        return full;
    }

    private static LibraryService NewService(InMemoryRepository repo) =>
        new(new LibraryScanner(new FakeReader()), repo);

    [Fact]
    public async Task AddFolderAsync_PersistsTracksAndFolder()
    {
        Touch("a/one.mp3");
        var repo = new InMemoryRepository();

        var snapshot = await NewService(repo).AddFolderAsync(Path.Combine(_root, "a"));

        Assert.Single(snapshot.Tracks);
        Assert.Single(snapshot.Folders);
        Assert.Equal(1, repo.SaveCount);
        Assert.Same(snapshot, repo.Stored);
    }

    [Fact]
    public async Task AddFolderAsync_Rescan_PreservesTrackIdAndDoesNotDuplicate()
    {
        Touch("a/one.mp3");
        var service = NewService(new InMemoryRepository());
        var folder = Path.Combine(_root, "a");

        var first = await service.AddFolderAsync(folder);
        var second = await service.AddFolderAsync(folder);

        Assert.Single(second.Tracks);
        Assert.Single(second.Folders);
        Assert.Equal(first.Tracks[0].Id, second.Tracks[0].Id);
        Assert.Equal(first.Tracks[0].AddedAt, second.Tracks[0].AddedAt);
    }

    [Fact]
    public async Task AddFolderAsync_TwoFolders_MergesBoth()
    {
        Touch("a/one.mp3");
        Touch("b/two.mp3");
        var service = NewService(new InMemoryRepository());

        await service.AddFolderAsync(Path.Combine(_root, "a"));
        var snapshot = await service.AddFolderAsync(Path.Combine(_root, "b"));

        Assert.Equal(2, snapshot.Tracks.Count);
        Assert.Equal(2, snapshot.Folders.Count);
    }

    [Fact]
    public async Task PruneMissingAsync_RemovesDeletedFilesAndSaves()
    {
        var keep = Touch("a/keep.mp3");
        var gone = Path.Combine(_root, "a", "gone.mp3"); // nunca se crea
        var repo = new InMemoryRepository
        {
            Stored = new LibrarySnapshot(
                new[]
                {
                new Track { FilePath = keep, Title = "keep" },
                new Track { FilePath = gone, Title = "gone" }
                },
                Array.Empty<string>())
        };
        var service = NewService(repo);
        await service.LoadAsync();

        var result = await service.PruneMissingAsync();

        Assert.Equal("keep", Assert.Single(result.Snapshot.Tracks).Title);
        Assert.Equal("gone", Assert.Single(result.Removed).Title);
        Assert.Equal(1, repo.SaveCount);
    }

    [Fact]
    public async Task FindMissingAsync_ReturnsOnlyTracksWhoseFileIsGone()
    {
        Touch("a/keep.mp3");
        var doomed = Touch("a/gone.mp3");
        var service = NewService(new InMemoryRepository());
        await service.AddFolderAsync(Path.Combine(_root, "a"));

        File.Delete(doomed);
        var missing = await service.FindMissingAsync();

        Assert.Equal("gone", Assert.Single(missing).Title);
    }

    [Fact]
    public async Task FindMissingAsync_NothingMissing_ReturnsEmpty()
    {
        Touch("a/one.mp3");
        var service = NewService(new InMemoryRepository());
        await service.AddFolderAsync(Path.Combine(_root, "a"));

        Assert.Empty(await service.FindMissingAsync());
    }

    [Fact]
    public async Task PruneMissingAsync_NothingMissing_DoesNotSave()
    {
        Touch("a/one.mp3");
        var repo = new InMemoryRepository();
        var service = NewService(repo);
        await service.AddFolderAsync(Path.Combine(_root, "a")); // un guardado

        var result = await service.PruneMissingAsync();

        Assert.Empty(result.Removed);
        Assert.Single(result.Snapshot.Tracks);
        Assert.Equal(1, repo.SaveCount);
    }

    [Fact]
    public async Task RemoveTracksAsync_Unlink_RemovesFromLibraryButKeepsFile()
    {
        var file = Touch("a/one.mp3");
        Touch("a/two.mp3");
        var repo = new InMemoryRepository();
        var service = NewService(repo);
        var snapshot = await service.AddFolderAsync(Path.Combine(_root, "a"));
        var target = snapshot.Tracks.Single(t => t.Title == "one");

        var result = await service.RemoveTracksAsync(new[] { target.Id }, deleteFiles: false);

        Assert.Equal("two", Assert.Single(result.Snapshot.Tracks).Title);
        Assert.Single(result.Removed);
        Assert.Empty(result.Errors);
        Assert.True(File.Exists(file));
        Assert.Same(result.Snapshot, repo.Stored);
    }

    [Fact]
    public async Task RemoveTracksAsync_DeleteFiles_RemovesFileAndTrack()
    {
        var file = Touch("a/one.mp3");
        var service = NewService(new InMemoryRepository());
        var snapshot = await service.AddFolderAsync(Path.Combine(_root, "a"));

        var result = await service.RemoveTracksAsync(new[] { snapshot.Tracks[0].Id }, deleteFiles: true);

        Assert.Empty(result.Snapshot.Tracks);
        Assert.False(File.Exists(file));
    }

    [Fact]
    public async Task RemoveTracksAsync_UnknownIds_DoesNothingAndDoesNotSave()
    {
        Touch("a/one.mp3");
        var repo = new InMemoryRepository();
        var service = NewService(repo);
        await service.AddFolderAsync(Path.Combine(_root, "a"));

        var result = await service.RemoveTracksAsync(new[] { Guid.NewGuid() }, deleteFiles: true);

        Assert.Single(result.Snapshot.Tracks);
        Assert.Empty(result.Removed);
        Assert.Equal(1, repo.SaveCount); // solo el guardado de AddFolderAsync
    }

    [Fact]
    public async Task ScanFolderAsync_FileMovedToAnotherFolder_KeepsTheOriginalId()
    {
        var original = Touch("a/song.mp3");
        var service = NewService(new InMemoryRepository());
        var first = await service.AddFolderAsync(Path.Combine(_root, "a"));

        var moved = Path.Combine(_root, "b", "song.mp3");
        Directory.CreateDirectory(Path.GetDirectoryName(moved)!);
        File.Move(original, moved);

        var result = await service.ScanFolderAsync(Path.Combine(_root, "b"));

        var track = Assert.Single(result.Snapshot.Tracks);
        Assert.Equal(1, result.Relocated);
        Assert.Equal(first.Tracks[0].Id, track.Id);
        Assert.Equal(first.Tracks[0].AddedAt, track.AddedAt);
        Assert.Equal(moved, track.FilePath);
    }

    [Fact]
    public async Task ScanFolderAsync_CopiedFile_IsANewTrackNotARelocation()
    {
        Touch("a/song.mp3");
        var service = NewService(new InMemoryRepository());
        await service.AddFolderAsync(Path.Combine(_root, "a"));

        Touch("b/song.mp3"); // el original sigue existiendo
        var result = await service.ScanFolderAsync(Path.Combine(_root, "b"));

        Assert.Equal(0, result.Relocated);
        Assert.Equal(2, result.Snapshot.Tracks.Count);
    }

    [Fact]
    public async Task ScanFolderAsync_AmbiguousMatch_LeavesEverythingAlone()
    {
        var first = Touch("a/song.mp3");
        var second = Touch("c/song.mp3");
        var service = NewService(new InMemoryRepository());
        await service.AddFolderAsync(Path.Combine(_root, "a"));
        await service.AddFolderAsync(Path.Combine(_root, "c"));

        File.Delete(first);
        File.Delete(second);
        Touch("b/song.mp3"); // un candidato para dos ausentes idénticas

        var result = await service.ScanFolderAsync(Path.Combine(_root, "b"));

        Assert.Equal(0, result.Relocated);
        Assert.Equal(3, result.Snapshot.Tracks.Count);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}