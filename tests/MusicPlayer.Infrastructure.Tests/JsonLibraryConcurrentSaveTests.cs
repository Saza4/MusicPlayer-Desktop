using MusicPlayer.Core.Models;
using MusicPlayer.Infrastructure;

namespace MusicPlayer.Infrastructure.Tests;

/// <summary>docs/specs/library-data-safety.md, criterio 5: dos guardados a la vez no se pisan.</summary>
public class JsonLibraryConcurrentSaveTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("repo-concurrent-tests").FullName;

    [Fact]
    public async Task ConcurrentSaves_DoNotThrow_AndLeaveAValidFile_WithNoTemporaries()
    {
        var path = Path.Combine(_dir, "library.json");
        var repo = new JsonLibraryRepository(path);
        var big = new LibrarySnapshot(Enumerable.Range(0, 20_000)
            .Select(i => new Track { FilePath = $@"C:\Music\{i}.mp3", Title = $"Pista {i}" }).ToList());

        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(() => repo.SaveAsync(big))));

        var loaded = await new JsonLibraryRepository(path).LoadAsync();
        Assert.Equal(20_000, loaded.Tracks.Count);
        Assert.Empty(Directory.GetFiles(_dir, "*.tmp"));
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);
}
