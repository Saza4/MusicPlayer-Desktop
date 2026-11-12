using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;

namespace MusicPlayer.Core.Tests;

/// <summary>docs/specs/library-data-safety.md: criterios 1, 3, 4 y 5.</summary>
public class LibraryDataSafetyTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("library-safety-tests").FullName;

    private sealed class FakeReader : ITrackMetadataReader
    {
        public Track? Read(string filePath) =>
            new() { FilePath = filePath, Title = Path.GetFileNameWithoutExtension(filePath) };
    }

    private sealed class FlakyRepository : ILibraryRepository
    {
        public bool FailOnLoad { get; set; }
        public TimeSpan SaveDelay { get; set; }
        public int SaveCount;
        public int Concurrent;
        public int MaxConcurrent;

        public Task<LibrarySnapshot> LoadAsync(CancellationToken ct = default) =>
            FailOnLoad ? throw new IOException("archivo en uso") : Task.FromResult(LibrarySnapshot.Empty);

        public async Task SaveAsync(LibrarySnapshot snapshot, CancellationToken ct = default)
        {
            var now = Interlocked.Increment(ref Concurrent);
            InterlockedMax(ref MaxConcurrent, now);
            if (SaveDelay > TimeSpan.Zero) await Task.Delay(SaveDelay, ct);
            Interlocked.Increment(ref SaveCount);
            Interlocked.Decrement(ref Concurrent);
        }

        private static void InterlockedMax(ref int target, int value)
        {
            int seen;
            while (value > (seen = Volatile.Read(ref target)) &&
                   Interlocked.CompareExchange(ref target, value, seen) != seen) { }
        }
    }

    private string Touch(string relativePath)
    {
        var full = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, "");
        return full;
    }

    private static LibraryService NewService(ILibraryRepository repo) =>
        new(new LibraryScanner(new FakeReader()), repo);

    // ---- Criterio 1: carga fallida bloquea los guardados ----

    [Fact]
    public async Task FailedLoad_BlocksEverySave()
    {
        var one = Touch("a/one.mp3");
        var repo = new FlakyRepository { FailOnLoad = true };
        var service = NewService(repo);

        await Assert.ThrowsAsync<IOException>(() => service.LoadAsync());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ScanFolderAsync(Path.Combine(_root, "a")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AddFilesAsync(new[] { one }));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ApplyTagsBatchAsync(new Dictionary<Guid, TrackTags> { [Guid.NewGuid()] = default! }));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RemoveTracksAsync(new[] { Guid.NewGuid() }, deleteFiles: false));
        Assert.Equal(0, repo.SaveCount);
    }

    [Fact]
    public async Task SuccessfulLoadAfterAFailure_EnablesSavesAgain()
    {
        Touch("a/one.mp3");
        var repo = new FlakyRepository { FailOnLoad = true };
        var service = NewService(repo);
        await Assert.ThrowsAsync<IOException>(() => service.LoadAsync());

        repo.FailOnLoad = false;
        await service.LoadAsync();
        await service.ScanFolderAsync(Path.Combine(_root, "a"));

        Assert.Equal(1, repo.SaveCount);
    }

    [Fact]
    public async Task NeverLoaded_StillSaves()
    {
        Touch("a/one.mp3");
        var repo = new FlakyRepository();

        await NewService(repo).ScanFolderAsync(Path.Combine(_root, "a"));

        Assert.Equal(1, repo.SaveCount);
    }

    // ---- Criterio 3: playlists ----

    private sealed class FlakyPlaylists : IPlaylistRepository
    {
        public bool FailOnLoad { get; set; } = true;
        public int SaveCount;

        public Task<IReadOnlyList<Playlist>> LoadAsync(CancellationToken ct = default) =>
            FailOnLoad ? throw new IOException("archivo en uso") : Task.FromResult<IReadOnlyList<Playlist>>(Array.Empty<Playlist>());

        public Task SaveAsync(IReadOnlyList<Playlist> playlists, CancellationToken ct = default)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Playlists_FailedLoad_BlocksSaves_UntilALoadSucceeds()
    {
        var repo = new FlakyPlaylists();
        var manager = new PlaylistManager(repo);

        await Assert.ThrowsAsync<IOException>(() => manager.LoadAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.CreateAsync("Rock"));
        Assert.Equal(0, repo.SaveCount);

        repo.FailOnLoad = false;
        await manager.LoadAsync();
        await manager.CreateAsync("Rock");
        Assert.Equal(1, repo.SaveCount);
    }

    // ---- Criterio 4: temporales ----

    [Theory]
    [InlineData("~conv-0123abcd.mp3")]
    [InlineData("~tags-0123abcd.flac")]
    [InlineData("sub/~CONV-0123abcd.m4a")]
    public async Task Scan_IgnoresTemporaryFiles(string temp)
    {
        Touch("real.mp3");
        Touch(temp);

        var tracks = await new LibraryScanner(new FakeReader()).ScanAsync(_root);

        var track = Assert.Single(tracks);
        Assert.Equal("real", track.Title);
    }

    // ---- Criterio 5: serialización ----

    [Fact]
    public async Task ConcurrentOperations_NeverSaveAtTheSameTime_AndKeepEveryChange()
    {
        var a = Touch("a/one.mp3");
        var b = Touch("b/two.mp3");
        // Única espera real de las pruebas (docs/specs/test-waits-and-docs.md, criterio 4): solo ensancha la ventana
        // en la que dos guardados se solaparían. Lo que se comprueba no depende de cuánto dure.
        var repo = new FlakyRepository { SaveDelay = TimeSpan.FromMilliseconds(80) };
        var service = NewService(repo);

        await Task.WhenAll(service.AddFilesAsync(new[] { a }), service.AddFilesAsync(new[] { b }));

        Assert.Equal(1, repo.MaxConcurrent);
        Assert.Equal(2, service.Current.Tracks.Count);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
