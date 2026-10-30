using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;

namespace MusicPlayer.Core.Tests;

public class TagEditorTests
{
    private sealed class NullReader : ITrackMetadataReader
    {
        public Track? Read(string filePath) => null;
    }

    private sealed class MemoryRepository : ILibraryRepository
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

    private sealed class FakeWriter : ITrackTagWriter
    {
        public List<(string Path, TrackTags Tags)> Calls { get; } = new();
        public Exception? Fail { get; set; }

        public void Write(string filePath, TrackTags tags)
        {
            if (Fail is not null) throw Fail;
            Calls.Add((filePath, tags));
        }
    }

    private static async Task<(TagEditor Editor, LibraryService Library, MemoryRepository Repo, FakeWriter Writer, Track Track)> SetUp()
    {
        var track = new Track { FilePath = "song.mp3", Title = "Old", Artist = "A", Album = "X" };
        var repo = new MemoryRepository
        {
            Stored = new LibrarySnapshot(new[] { track }, Array.Empty<string>())
        };
        var library = new LibraryService(new LibraryScanner(new NullReader()), repo);
        await library.LoadAsync();

        var writer = new FakeWriter();
        return (new TagEditor(writer, library), library, repo, writer, track);
    }

    [Fact]
    public async Task Save_WritesTheFileAndUpdatesTheLibraryKeepingTheId()
    {
        var (editor, _, repo, writer, track) = await SetUp();

        var snapshot = await editor.SaveAsync(
            track.Id, new TrackTags("  New  ", "B", "Various Artists", "Y", 2020, 3, 2));

        var saved = Assert.Single(snapshot.Tracks);
        Assert.Equal(track.Id, saved.Id);
        Assert.Equal("New", saved.Title);
        Assert.Equal("B", saved.Artist);
        Assert.Equal("Various Artists", saved.AlbumArtist);
        Assert.Equal(3u, saved.TrackNumber);
        Assert.Equal(2u, saved.DiscNumber);

        var call = Assert.Single(writer.Calls);
        Assert.Equal("song.mp3", call.Path);
        Assert.Equal("New", call.Tags.Title);
        Assert.Equal(1, repo.SaveCount);
    }

    [Fact]
    public async Task Save_BlankTitle_ThrowsAndTouchesNothing()
    {
        var (editor, _, repo, writer, track) = await SetUp();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            editor.SaveAsync(track.Id, new TrackTags("   ", "A", "", "X", 0, 0, 0)));

        Assert.Empty(writer.Calls);
        Assert.Equal(0, repo.SaveCount);
    }

    [Fact]
    public async Task Save_UnknownTrack_ThrowsAndTouchesNothing()
    {
        var (editor, _, _, writer, _) = await SetUp();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            editor.SaveAsync(Guid.NewGuid(), new TrackTags("T", "A", "", "X", 0, 0, 0)));

        Assert.Empty(writer.Calls);
    }

    [Fact]
    public async Task Save_WhenTheFileCannotBeWritten_LeavesTheLibraryUntouched()
    {
        var (editor, library, repo, writer, track) = await SetUp();
        writer.Fail = new IOException("en uso");

        await Assert.ThrowsAsync<IOException>(() =>
            editor.SaveAsync(track.Id, new TrackTags("New", "A", "", "X", 0, 0, 0)));

        Assert.Equal("Old", library.Current.Tracks[0].Title);
        Assert.Equal(0, repo.SaveCount);
    }
}