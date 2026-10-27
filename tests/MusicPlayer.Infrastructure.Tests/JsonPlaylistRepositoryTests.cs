using MusicPlayer.Core.Models;
using MusicPlayer.Infrastructure;

namespace MusicPlayer.Infrastructure.Tests;

public class JsonPlaylistRepositoryTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("playlist-tests").FullName;
    private string FilePath => Path.Combine(_dir, "nested", "playlists.json");

    [Fact]
    public async Task SaveThenLoad_RoundTripsNamesOrderAndTracks()
    {
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var playlists = new[]
        {
            new Playlist { Name = "Canciones de viaje", TrackIds = ids },
            new Playlist { Name = "Vacía" }
        };

        await new JsonPlaylistRepository(FilePath).SaveAsync(playlists);
        var loaded = await new JsonPlaylistRepository(FilePath).LoadAsync();

        Assert.Equal(2, loaded.Count);
        Assert.Equal(playlists[0].Id, loaded[0].Id);
        Assert.Equal("Canciones de viaje", loaded[0].Name);
        Assert.Equal(ids, loaded[0].TrackIds);
        Assert.Equal(playlists[0].CreatedAt, loaded[0].CreatedAt);
        Assert.Empty(loaded[1].TrackIds);
    }

    [Fact]
    public async Task Load_MissingFile_ReturnsEmpty()
    {
        Assert.Empty(await new JsonPlaylistRepository(FilePath).LoadAsync());
    }

    [Fact]
    public async Task Load_CorruptFile_ReturnsEmptyAndKeepsBackup()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        await File.WriteAllTextAsync(FilePath, "{ esto no es json");

        var loaded = await new JsonPlaylistRepository(FilePath).LoadAsync();

        Assert.Empty(loaded);
        Assert.True(File.Exists(FilePath + ".corrupt"));
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);
}