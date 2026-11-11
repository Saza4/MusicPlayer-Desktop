using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;

namespace MusicPlayer.Core.Tests;

/// <summary>docs/specs/library-data-safety-2.md, criterio 8: las rutas se comparan según el sistema.</summary>
public class FilePathsTests
{
    [Fact]
    public void CaseSensitive_TreatsDifferentCaseAsDifferentFiles()
    {
        Assert.False(FilePaths.For(caseSensitive: true).Equals("/musica/A.mp3", "/musica/a.mp3"));
    }

    [Fact]
    public void CaseInsensitive_TreatsDifferentCaseAsTheSameFile()
    {
        Assert.True(FilePaths.For(caseSensitive: false).Equals(@"C:\Musica\A.mp3", @"c:\musica\a.MP3"));
    }

    [Fact]
    public void Comparer_FollowsTheOperatingSystem()
    {
        var sameFile = FilePaths.Comparer.Equals("A.mp3", "a.mp3");

        Assert.Equal(OperatingSystem.IsWindows() || OperatingSystem.IsMacOS(), sameFile);
    }

    private sealed class Reader : ITrackMetadataReader
    {
        public Track? Read(string filePath) => new() { FilePath = filePath, Title = filePath };
    }

    private sealed class Repository : ILibraryRepository
    {
        public Task<LibrarySnapshot> LoadAsync(CancellationToken ct = default) => Task.FromResult(LibrarySnapshot.Empty);
        public Task SaveAsync(LibrarySnapshot snapshot, CancellationToken ct = default) => Task.CompletedTask;
    }

    // La biblioteca usa ese comparador: dos rutas que solo difieren en mayúsculas son una pista o dos según el sistema.
    [Fact]
    public async Task Library_MergesPathsThatDifferOnlyInCase_OnlyWhereTheSystemDoes()
    {
        var service = new LibraryService(new LibraryScanner(new Reader()), new Repository());

        await service.AddFilesAsync(new[] { "/musica/A.mp3" });
        var snapshot = await service.AddFilesAsync(new[] { "/musica/a.mp3" });

        var expected = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? 1 : 2;
        Assert.Equal(expected, snapshot.Tracks.Count);
    }
}
