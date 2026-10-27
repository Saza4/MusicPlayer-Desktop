using MusicPlayer.Core.Models;
using MusicPlayer.Infrastructure;

namespace MusicPlayer.Infrastructure.Tests;

public class FolderCoverArtProviderTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("cover-tests").FullName;
    private readonly FolderCoverArtProvider _provider = new();

    private Track NewTrack() => new() { FilePath = Path.Combine(_dir, "song.mp3"), Title = "song" };

    private void Write(string name, params byte[] bytes) =>
        File.WriteAllBytes(Path.Combine(_dir, name), bytes);

    [Fact]
    public async Task ReturnsCoverImageFromSameFolder()
    {
        Write("cover.jpg", 1, 2, 3);

        Assert.Equal(new byte[] { 1, 2, 3 }, await _provider.GetCoverAsync(NewTrack()));
    }

    [Fact]
    public async Task MatchesNamesAndExtensionsIgnoringCase()
    {
        Write("Folder.PNG", 9);

        Assert.Equal(new byte[] { 9 }, await _provider.GetCoverAsync(NewTrack()));
    }

    [Fact]
    public async Task PrefersCoverOverOtherNames()
    {
        Write("folder.jpg", 2);
        Write("cover.jpg", 1);

        Assert.Equal(new byte[] { 1 }, await _provider.GetCoverAsync(NewTrack()));
    }

    [Fact]
    public async Task NoMatchingImage_ReturnsNull()
    {
        Write("photo.jpg", 5);

        Assert.Null(await _provider.GetCoverAsync(NewTrack()));

        var missingFolder = new Track { FilePath = Path.Combine(_dir, "nope", "x.mp3"), Title = "x" };
        Assert.Null(await _provider.GetCoverAsync(missingFolder));
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);
}