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

        var snapshot = await service.PruneMissingAsync();

        Assert.Single(snapshot.Tracks);
        Assert.Equal("keep", snapshot.Tracks[0].Title);
        Assert.Equal(1, repo.SaveCount);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}