using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;

namespace MusicPlayer.Core.Tests;

public class CoverEditorTests
{
    private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3 };

    private sealed class NullReader : ITrackMetadataReader
    {
        public Track? Read(string filePath) => null;
    }

    private sealed class MemoryRepository : ILibraryRepository
    {
        public LibrarySnapshot Stored { get; set; } = LibrarySnapshot.Empty;
        public Task<LibrarySnapshot> LoadAsync(CancellationToken ct = default) => Task.FromResult(Stored);
        public Task SaveAsync(LibrarySnapshot snapshot, CancellationToken ct = default)
        {
            Stored = snapshot;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeWriter : ICoverArtWriter
    {
        public List<(string Path, byte[]? Image)> Calls { get; } = new();
        public HashSet<string> Failing { get; } = new();

        public void Write(string filePath, byte[]? image)
        {
            if (Failing.Contains(filePath)) throw new IOException("bloqueado");
            Calls.Add((filePath, image));
        }
    }

    private static async Task<(CoverEditor Editor, FakeWriter Writer, Track Track)> SetUp()
    {
        var track = new Track { FilePath = "song.mp3", Title = "T" };
        var repo = new MemoryRepository { Stored = new LibrarySnapshot(new[] { track }, Array.Empty<string>()) };
        var library = new LibraryService(new LibraryScanner(new NullReader()), repo);
        await library.LoadAsync();

        var writer = new FakeWriter();
        return (new CoverEditor(writer, library), writer, track);
    }

    [Fact]
    public async Task Set_WritesTheImageToTheTrackFile()
    {
        var (editor, writer, track) = await SetUp();

        await editor.SetAsync(track.Id, Png);

        var call = Assert.Single(writer.Calls);
        Assert.Equal("song.mp3", call.Path);
        Assert.Equal(Png, call.Image);
    }

    [Fact]
    public async Task Set_Null_RemovesTheCover()
    {
        var (editor, writer, track) = await SetUp();

        await editor.SetAsync(track.Id, null);

        Assert.Null(Assert.Single(writer.Calls).Image);
    }

    [Fact]
    public async Task Set_NotAnImage_ThrowsAndWritesNothing()
    {
        var (editor, writer, track) = await SetUp();

        await Assert.ThrowsAsync<ArgumentException>(() => editor.SetAsync(track.Id, new byte[] { 1, 2, 3 }));

        Assert.Empty(writer.Calls);
    }

    [Fact]
    public async Task Set_TooBig_ThrowsAndWritesNothing()
    {
        var (editor, writer, track) = await SetUp();
        var big = new byte[CoverImage.MaxBytes + 1];
        Png.CopyTo(big, 0);

        await Assert.ThrowsAsync<ArgumentException>(() => editor.SetAsync(track.Id, big));

        Assert.Empty(writer.Calls);
    }

    [Fact]
    public async Task Set_UnknownTrack_Throws()
    {
        var (editor, writer, _) = await SetUp();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => editor.SetAsync(Guid.NewGuid(), Png));

        Assert.Empty(writer.Calls);
    }

    private static Track Song(string file) => new() { FilePath = file, Title = file };

    [Fact]
    public async Task SetMany_WritesTheImageToEveryTrack()
    {
        var (editor, writer, _) = await SetUp();

        var result = await editor.SetManyAsync(new[] { Song("a.mp3"), Song("b.mp3") }, Png);

        Assert.Equal(2, result.Written);
        Assert.Empty(result.Errors);
        Assert.Equal(new[] { "a.mp3", "b.mp3" }, writer.Calls.Select(c => c.Path));
    }

    [Fact]
    public async Task SetMany_OneFailure_KeepsGoingAndReportsIt()
    {
        var (editor, writer, _) = await SetUp();
        writer.Failing.Add("a.mp3");

        var result = await editor.SetManyAsync(new[] { Song("a.mp3"), Song("b.mp3") }, Png);

        Assert.Equal(1, result.Written);
        Assert.Contains("a.mp3", Assert.Single(result.Errors));
        Assert.Equal("b.mp3", Assert.Single(writer.Calls).Path);
    }

    [Fact]
    public async Task SetMany_InvalidImage_ThrowsBeforeWritingAnything()
    {
        var (editor, writer, _) = await SetUp();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            editor.SetManyAsync(new[] { Song("a.mp3") }, new byte[] { 1, 2, 3 }));

        Assert.Empty(writer.Calls);
    }
}