using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;

namespace MusicPlayer.Core.Tests;

public class TagBatchTests
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
        public HashSet<string> Failing { get; } = new();

        public void Write(string filePath, TrackTags tags)
        {
            if (Failing.Contains(filePath)) throw new IOException("en uso");
            Calls.Add((filePath, tags));
        }
    }

    private static async Task<(TagEditor Editor, LibraryService Library, MemoryRepository Repo, FakeWriter Writer, Track[] Tracks)>
        SetUp(int count = 3)
    {
        var tracks = Enumerable.Range(1, count)
            .Select(i => new Track { FilePath = $"s{i}.mp3", Title = $"T{i}", Artist = "A", Album = "X" })
            .ToArray();
        var repo = new MemoryRepository { Stored = new LibrarySnapshot(tracks, Array.Empty<string>()) };
        var library = new LibraryService(new LibraryScanner(new NullReader()), repo);
        await library.LoadAsync();

        var writer = new FakeWriter();
        return (new TagEditor(writer, library), library, repo, writer, tracks);
    }

    private static TagChange Rename(Track track, string title)
    {
        var before = TrackTags.From(track);
        return new TagChange(track.Id, before, before with { Title = title });
    }

    [Fact]
    public async Task Batch_WritesEveryFileAndSavesTheLibraryOnce()
    {
        var (editor, library, repo, writer, tracks) = await SetUp();

        var result = await editor.ApplyBatchAsync(tracks.Select(t => Rename(t, t.Title + "!")).ToList());

        Assert.Equal(3, result.Applied.Count);
        Assert.Empty(result.Errors);
        Assert.Equal(3, writer.Calls.Count);
        Assert.Equal(1, repo.SaveCount);
        Assert.All(library.Current.Tracks, t => Assert.EndsWith("!", t.Title));
    }

    [Fact]
    public async Task Batch_ContinuesAfterAFailure_AndOnlyUpdatesWhatWasWritten()
    {
        var (editor, library, _, writer, tracks) = await SetUp();
        writer.Failing.Add("s2.mp3");

        var result = await editor.ApplyBatchAsync(tracks.Select(t => Rename(t, "New " + t.Title)).ToList());

        Assert.Equal(2, result.Applied.Count);
        Assert.Contains("s2.mp3", Assert.Single(result.Errors));
        Assert.Equal("T2", library.Current.Tracks.Single(t => t.Id == tracks[1].Id).Title);
        Assert.Equal("New T1", library.Current.Tracks.Single(t => t.Id == tracks[0].Id).Title);
    }

    [Fact]
    public async Task Batch_CancelledBeforeStart_WritesNothingAndDoesNotSave()
    {
        var (editor, _, repo, writer, tracks) = await SetUp();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await editor.ApplyBatchAsync(
            tracks.Select(t => Rename(t, "New")).ToList(), ct: cts.Token);

        Assert.Empty(result.Applied);
        Assert.Empty(writer.Calls);
        Assert.Equal(0, repo.SaveCount);
    }

    [Fact]
    public async Task Undo_RestoresThePreviousTagsInFilesAndLibrary()
    {
        var (editor, library, _, writer, tracks) = await SetUp(2);
        var applied = (await editor.ApplyBatchAsync(
            tracks.Select(t => Rename(t, "New " + t.Title)).ToList())).Applied;

        await editor.UndoAsync(applied);

        Assert.Equal(new[] { "T1", "T2" }, library.Current.Tracks.Select(t => t.Title).OrderBy(t => t));
        Assert.Equal(4, writer.Calls.Count);
        Assert.Equal("T1", writer.Calls[2].Tags.Title);
    }

    [Fact]
    public async Task Batch_RejectsBlankTitles()
    {
        var (editor, _, _, writer, tracks) = await SetUp(1);

        var result = await editor.ApplyBatchAsync(new[] { Rename(tracks[0], "   ") });

        Assert.Empty(result.Applied);
        Assert.Single(result.Errors);
        Assert.Empty(writer.Calls);
    }
}