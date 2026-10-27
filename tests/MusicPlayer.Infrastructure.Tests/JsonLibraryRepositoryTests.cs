using MusicPlayer.Core.Models;
using MusicPlayer.Infrastructure;

namespace MusicPlayer.Infrastructure.Tests;

public class JsonLibraryRepositoryTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("repo-tests").FullName;
    private string FilePath => Path.Combine(_dir, "nested", "library.json");

    [Fact]
    public async Task SaveThenLoad_RoundTripsTracksAndFolders()
    {
        var track = new Track
        {
            FilePath = @"C:\Music\a.mp3",
            Title = "Título",
            Artist = "Artista",
            Album = "Álbum",
            Year = 2020,
            Genre = "Rock",
            TrackNumber = 3,
            Duration = TimeSpan.FromSeconds(125.5)
        };

        await new JsonLibraryRepository(FilePath)
            .SaveAsync(new LibrarySnapshot(new[] { track }, new[] { @"C:\Music" }));

        var loaded = await new JsonLibraryRepository(FilePath).LoadAsync();

        var t = Assert.Single(loaded.Tracks);
        Assert.Equal(track.Id, t.Id);
        Assert.Equal(track.FilePath, t.FilePath);
        Assert.Equal("Título", t.Title);
        Assert.Equal("Álbum", t.Album);
        Assert.Equal(track.Duration, t.Duration);
        Assert.Equal(track.AddedAt, t.AddedAt);
        Assert.Equal(new[] { @"C:\Music" }, loaded.Folders);
    }

    [Fact]
    public async Task Load_MissingFile_ReturnsEmpty()
    {
        var loaded = await new JsonLibraryRepository(FilePath).LoadAsync();

        Assert.Empty(loaded.Tracks);
        Assert.Empty(loaded.Folders);
    }

    [Fact]
    public async Task Load_CorruptFile_ReturnsEmptyAndKeepsBackup()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        await File.WriteAllTextAsync(FilePath, "{ esto no es json");

        var loaded = await new JsonLibraryRepository(FilePath).LoadAsync();

        Assert.Empty(loaded.Tracks);
        Assert.True(File.Exists(FilePath + ".corrupt"));
        Assert.False(File.Exists(FilePath));
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);
}