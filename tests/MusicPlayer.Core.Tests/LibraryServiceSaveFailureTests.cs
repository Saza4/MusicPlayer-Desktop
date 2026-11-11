using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;

namespace MusicPlayer.Core.Tests;

/// <summary>
/// docs/specs/library-data-safety-2.md, criterio 1: si el guardado falla, la biblioteca en memoria no cambia.
/// </summary>
public class LibraryServiceSaveFailureTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("library-save-failure-tests").FullName;

    private sealed class FakeReader : ITrackMetadataReader
    {
        public Track? Read(string filePath) =>
            new() { FilePath = filePath, Title = Path.GetFileNameWithoutExtension(filePath) };
    }

    private sealed class Repository : ILibraryRepository
    {
        public bool FailSaves { get; set; }
        public LibrarySnapshot? Saved { get; private set; }

        public Task<LibrarySnapshot> LoadAsync(CancellationToken ct = default) =>
            Task.FromResult(LibrarySnapshot.Empty);

        public Task SaveAsync(LibrarySnapshot snapshot, CancellationToken ct = default)
        {
            if (FailSaves) throw new IOException("disco lleno");
            Saved = snapshot;
            return Task.CompletedTask;
        }
    }

    private readonly Repository _repo = new();

    private string Touch(string name)
    {
        var full = Path.Combine(_root, name);
        File.WriteAllText(full, "");
        return full;
    }

    private LibraryService NewService() => new(new LibraryScanner(new FakeReader()), _repo);

    /// <summary>Una biblioteca con «one» ya guardada; a partir de aquí los guardados fallan.</summary>
    private async Task<(LibraryService Service, Track Track)> WithOneTrackAsync()
    {
        Touch("one.mp3");
        var service = NewService();
        await service.ScanFolderAsync(_root);
        _repo.FailSaves = true;
        return (service, service.Current.Tracks.Single());
    }

    private static TrackTags Tags(string title) => new(title, "Artista", "", "Álbum", 0, 0, 0);

    [Fact]
    public async Task ScanFolder_SaveFails_LeavesTheLibraryAsItWas()
    {
        Touch("one.mp3");
        _repo.FailSaves = true;
        var service = NewService();

        await Assert.ThrowsAsync<IOException>(() => service.ScanFolderAsync(_root));

        Assert.Empty(service.Current.Tracks);
    }

    [Fact]
    public async Task AddFiles_SaveFails_LeavesTheLibraryAsItWas()
    {
        var (service, _) = await WithOneTrackAsync();
        var before = service.Current;

        await Assert.ThrowsAsync<IOException>(() => service.AddFilesAsync(new[] { Touch("two.mp3") }));

        Assert.Same(before, service.Current);
    }

    [Fact]
    public async Task ApplyTags_SaveFails_LeavesTheLibraryAsItWas()
    {
        var (service, track) = await WithOneTrackAsync();
        var before = service.Current;

        await Assert.ThrowsAsync<IOException>(() => service.ApplyTagsAsync(track.Id, Tags("Otro título")));

        Assert.Same(before, service.Current);
        Assert.Equal("one", service.Current.Tracks.Single().Title);
    }

    [Fact]
    public async Task ApplyTagsBatch_SaveFails_LeavesTheLibraryAsItWas()
    {
        var (service, track) = await WithOneTrackAsync();
        var before = service.Current;

        await Assert.ThrowsAsync<IOException>(() => service.ApplyTagsBatchAsync(
            new Dictionary<Guid, TrackTags> { [track.Id] = Tags("Otro título") }));

        Assert.Same(before, service.Current);
    }

    [Fact]
    public async Task RemoveTracks_SaveFails_LeavesTheLibraryAsItWas()
    {
        var (service, track) = await WithOneTrackAsync();
        var before = service.Current;

        await Assert.ThrowsAsync<IOException>(() => service.RemoveTracksAsync(new[] { track.Id }, deleteFiles: false));

        Assert.Same(before, service.Current);
    }

    [Fact]
    public async Task FailedChange_IsNotCarriedByTheNextSuccessfulSave()
    {
        var (service, track) = await WithOneTrackAsync();
        await Assert.ThrowsAsync<IOException>(() => service.RemoveTracksAsync(new[] { track.Id }, deleteFiles: false));

        _repo.FailSaves = false;
        await service.ApplyTagsAsync(track.Id, Tags("Otro título"));

        Assert.Equal("Otro título", _repo.Saved!.Tracks.Single().Title); // la pista «quitada» sigue ahí
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
