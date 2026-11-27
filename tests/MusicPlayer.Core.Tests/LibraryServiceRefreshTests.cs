using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;

namespace MusicPlayer.Core.Tests;

/// <summary>docs/specs/auto-refresh-folders.md: carpetas recordadas y revisión que solo agrega.</summary>
public class LibraryServiceRefreshTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("library-refresh-tests").FullName;

    private sealed class CountingReader : ITrackMetadataReader
    {
        public List<string> Reads { get; } = new();

        public Track? Read(string filePath)
        {
            lock (Reads) Reads.Add(filePath);
            return new Track { FilePath = filePath, Title = Path.GetFileNameWithoutExtension(filePath) };
        }
    }

    private sealed class Repository : ILibraryRepository
    {
        public LibrarySnapshot Stored { get; set; } = LibrarySnapshot.Empty;
        public int SaveCount { get; private set; }
        public bool FailLoad { get; set; }
        public bool FailSaves { get; set; }

        public Task<LibrarySnapshot> LoadAsync(CancellationToken ct = default) =>
            FailLoad ? throw new IOException("archivo en uso") : Task.FromResult(Stored);

        public Task SaveAsync(LibrarySnapshot snapshot, CancellationToken ct = default)
        {
            if (FailSaves) throw new IOException("disco lleno");
            Stored = snapshot;
            SaveCount++;
            return Task.CompletedTask;
        }
    }

    private readonly CountingReader _reader = new();
    private readonly Repository _repo = new();

    private LibraryService NewService() => new(new LibraryScanner(_reader), _repo);

    private string Folder(string name) => Path.Combine(_root, name);

    private string Touch(string relativePath)
    {
        var full = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, "");
        return full;
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    // ---- Criterios 1 y 2: «Agregar carpeta» la recuerda ----

    [Fact]
    public async Task ScanFolder_RemembersTheFolder()
    {
        Touch("a/one.mp3");

        var result = await NewService().ScanFolderAsync(Folder("a"));

        Assert.Equal([Folder("a")], result.Snapshot.Folders);
        Assert.Equal([Folder("a")], _repo.Stored.Folders);
    }

    [Fact]
    public async Task ScanFolder_SameFolderTwice_IsRememberedOnce()
    {
        Touch("a/one.mp3");
        var service = NewService();

        await service.ScanFolderAsync(Folder("a"));
        await service.ScanFolderAsync(Folder("a") + Path.DirectorySeparatorChar);

        Assert.Equal([Folder("a")], service.Current.Folders);
    }

    [Fact]
    public async Task ScanFolder_EmptyFolder_IsRememberedToo()
    {
        Directory.CreateDirectory(Folder("vacía"));

        var result = await NewService().ScanFolderAsync(Folder("vacía"));

        Assert.Equal([Folder("vacía")], result.Snapshot.Folders);
    }

    [Fact]
    public async Task ScanFolder_SaveFails_DoesNotRememberTheFolder()
    {
        Touch("a/one.mp3");
        var service = NewService();
        _repo.FailSaves = true;

        await Assert.ThrowsAsync<IOException>(() => service.ScanFolderAsync(Folder("a")));

        Assert.Empty(service.Current.Folders);
        Assert.Empty(service.Current.Tracks);
    }

    [Fact]
    public async Task OtherOperations_KeepTheFolders()
    {
        Touch("a/one.mp3");
        var service = NewService();
        await service.ScanFolderAsync(Folder("a"));

        await service.RemoveTracksAsync([service.Current.Tracks[0].Id], deleteFiles: false);

        Assert.Equal([Folder("a")], _repo.Stored.Folders);
    }

    // ---- Criterio 4: solo lee lo nuevo ----

    [Fact]
    public async Task Refresh_AddsNewFilesAndKeepsTheKnownOnes()
    {
        Touch("a/one.mp3");
        var service = NewService();
        await service.ScanFolderAsync(Folder("a"));
        var known = service.Current.Tracks.Single();
        Touch("a/sub/two.flac");

        var result = await service.RefreshFoldersAsync();

        Assert.Equal(1, result.Added);
        Assert.Equal(2, result.Snapshot.Tracks.Count);
        Assert.Contains(result.Snapshot.Tracks, t => t.Id == known.Id && t.AddedAt == known.AddedAt);
        Assert.Same(result.Snapshot, _repo.Stored);
        Assert.Equal([Folder("a")], result.Snapshot.Folders);
    }

    [Fact]
    public async Task Refresh_ReadsTagsOnlyOfUnknownFiles()
    {
        Touch("a/one.mp3");
        var service = NewService();
        await service.ScanFolderAsync(Folder("a"));
        var two = Touch("a/two.mp3");
        _reader.Reads.Clear();

        await service.RefreshFoldersAsync();

        Assert.Equal([Path.GetFullPath(two)], _reader.Reads);
    }

    [Fact]
    public async Task Refresh_IgnoresTemporariesAndOtherExtensions()
    {
        Directory.CreateDirectory(Folder("a"));
        var service = NewService();
        await service.ScanFolderAsync(Folder("a"));
        Touch($"a/~tags-{Guid.NewGuid():N}.mp3");
        Touch("a/portada.jpg");

        var result = await service.RefreshFoldersAsync();

        Assert.Equal(0, result.Added);
        Assert.Empty(result.Snapshot.Tracks);
    }

    [Fact]
    public async Task Refresh_ScansEveryRememberedFolder()
    {
        Touch("a/one.mp3");
        Touch("b/two.mp3");
        var service = NewService();
        await service.ScanFolderAsync(Folder("a"));
        await service.ScanFolderAsync(Folder("b"));
        Touch("a/three.mp3");
        Touch("b/four.mp3");
        var saves = _repo.SaveCount;

        var result = await service.RefreshFoldersAsync();

        Assert.Equal(2, result.Added);
        Assert.Equal(4, result.Snapshot.Tracks.Count);
        Assert.Equal(saves + 1, _repo.SaveCount); // criterio 10: un único guardado
    }

    // ---- Criterio 5: solo agrega ----

    [Fact]
    public async Task Refresh_DoesNotRemoveTracksWhoseFileIsGone()
    {
        var one = Touch("a/one.mp3");
        var service = NewService();
        await service.ScanFolderAsync(Folder("a"));
        File.Delete(one);
        Touch("a/two.mp3");

        var result = await service.RefreshFoldersAsync();

        Assert.Equal(2, result.Snapshot.Tracks.Count);
    }

    [Fact]
    public async Task Refresh_DoesNotDeleteStaleTemporaries()
    {
        Directory.CreateDirectory(Folder("a"));
        var service = NewService();
        await service.ScanFolderAsync(Folder("a"));
        var stale = Touch($"a/~conv-{Guid.NewGuid():N}.mp3");
        File.SetLastWriteTimeUtc(stale, DateTime.UtcNow.AddDays(-3));

        await service.RefreshFoldersAsync();

        Assert.True(File.Exists(stale));
    }

    // ---- Criterio 6: archivos movidos ----

    [Fact]
    public async Task Refresh_MovedFile_KeepsItsId()
    {
        var original = Touch("a/one.mp3");
        var service = NewService();
        await service.ScanFolderAsync(Folder("a"));
        var id = service.Current.Tracks.Single().Id;
        Directory.CreateDirectory(Folder("a/otra"));
        File.Move(original, Path.Combine(Folder("a/otra"), "one.mp3"));

        var result = await service.RefreshFoldersAsync();

        Assert.Equal(1, result.Relocated);
        Assert.Equal(0, result.Added);
        var moved = Assert.Single(result.Snapshot.Tracks);
        Assert.Equal(id, moved.Id);
    }

    // ---- Criterio 7: copias a medias ----

    [Fact]
    public async Task Refresh_FileStillBeingWritten_IsLeftForNextTime()
    {
        if (!OperatingSystem.IsWindows()) return; // solo Windows bloquea el archivo mientras otro lo escribe

        Directory.CreateDirectory(Folder("a"));
        var service = NewService();
        await service.ScanFolderAsync(Folder("a"));
        var copying = Path.Combine(Folder("a"), "copiando.mp3");

        using (new FileStream(copying, FileMode.Create, FileAccess.Write, FileShare.Read))
        {
            var during = await service.RefreshFoldersAsync();
            Assert.Equal(0, during.Added);
        }

        var after = await service.RefreshFoldersAsync();
        Assert.Equal(1, after.Added);
    }

    // ---- Criterio 8: carpeta no accesible ----

    [Fact]
    public async Task Refresh_MissingFolder_IsSkippedAndKept()
    {
        Touch("a/one.mp3");
        Touch("b/two.mp3");
        var service = NewService();
        await service.ScanFolderAsync(Folder("a"));
        await service.ScanFolderAsync(Folder("b"));
        Directory.Delete(Folder("a"), recursive: true);
        Touch("b/three.mp3");

        var result = await service.RefreshFoldersAsync();

        Assert.Equal(1, result.Added);
        Assert.Equal(1, result.FoldersUnreachable);
        Assert.Equal(3, result.Snapshot.Tracks.Count); // la de la carpeta que falta sigue
        Assert.Equal([Folder("a"), Folder("b")], _repo.Stored.Folders);
    }

    // ---- Criterio 9: sin cambios, sin guardado ----

    [Fact]
    public async Task Refresh_NothingNew_DoesNotSave()
    {
        Touch("a/one.mp3");
        var service = NewService();
        await service.ScanFolderAsync(Folder("a"));
        var saves = _repo.SaveCount;
        var before = service.Current;

        var result = await service.RefreshFoldersAsync();

        Assert.Equal(saves, _repo.SaveCount);
        Assert.Same(before, result.Snapshot);
        Assert.False(result.Changed);
    }

    [Fact]
    public async Task Refresh_NoFolders_DoesNothing()
    {
        var service = NewService();
        await service.LoadAsync();

        var result = await service.RefreshFoldersAsync();

        Assert.False(result.Changed);
        Assert.Equal(0, _repo.SaveCount);
    }

    // ---- Criterio 10: datos de la biblioteca ----

    [Fact]
    public async Task Refresh_AfterAFailedLoad_DoesNotSave()
    {
        _repo.FailLoad = true;
        var service = NewService();
        await Assert.ThrowsAsync<IOException>(() => service.LoadAsync());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RefreshFoldersAsync());

        Assert.Equal(0, _repo.SaveCount);
    }

    [Fact]
    public async Task Refresh_SaveFails_LeavesTheLibraryAsItWas()
    {
        Touch("a/one.mp3");
        var service = NewService();
        await service.ScanFolderAsync(Folder("a"));
        var before = service.Current;
        Touch("a/two.mp3");
        _repo.FailSaves = true;

        await Assert.ThrowsAsync<IOException>(() => service.RefreshFoldersAsync());

        Assert.Same(before, service.Current);
    }

    [Fact]
    public async Task Refresh_LoadedFolders_AreRefreshed()
    {
        Touch("a/one.mp3");
        _repo.Stored = LibrarySnapshot.Empty with { Folders = [Folder("a")] };
        var service = NewService();
        await service.LoadAsync();

        var result = await service.RefreshFoldersAsync();

        Assert.Equal(1, result.Added);
    }

    [Fact]
    public async Task Refresh_AndScanFolderAtTheSameTime_DoNotDuplicate()
    {
        Touch("a/one.mp3");
        var service = NewService();
        await service.ScanFolderAsync(Folder("a"));
        Touch("a/two.mp3");

        await Task.WhenAll(service.RefreshFoldersAsync(), service.ScanFolderAsync(Folder("a")));

        Assert.Equal(2, service.Current.Tracks.Count);
        Assert.Equal(2, service.Current.Tracks.Select(t => t.Id).Distinct().Count());
    }
}
