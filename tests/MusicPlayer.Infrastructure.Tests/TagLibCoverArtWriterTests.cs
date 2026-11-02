using MusicPlayer.Core.Models;
using MusicPlayer.Infrastructure;

namespace MusicPlayer.Infrastructure.Tests;

public class TagLibCoverArtWriterTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("coverwriter-tests").FullName;

    // PNG de 1×1 píxel.
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private string CreateMp3(string name)
    {
        var path = Path.Combine(_dir, name);
        var frame = new byte[417]; // MPEG1 capa 3, 128 kbps, 44,1 kHz
        frame[0] = 0xFF; frame[1] = 0xFB; frame[2] = 0x90; frame[3] = 0x00;

        using var stream = File.Create(path);
        for (int i = 0; i < 8; i++) stream.Write(frame);
        return path;
    }

    private static Task<byte[]?> ReadCover(string path) =>
        new EmbeddedCoverArtProvider().GetCoverAsync(new Track { FilePath = path, Title = "x" });

    [Fact]
    public async Task Write_EmbedsTheImageInTheFile()
    {
        var path = CreateMp3("song.mp3");

        new TagLibCoverArtWriter().Write(path, Png);

        Assert.Equal(Png, await ReadCover(path));
        Assert.Single(Directory.GetFiles(_dir)); // sin temporales
    }

    [Fact]
    public async Task Write_Null_RemovesTheCover()
    {
        var path = CreateMp3("song.mp3");
        var writer = new TagLibCoverArtWriter();
        writer.Write(path, Png);

        writer.Write(path, null);

        Assert.Null(await ReadCover(path));
    }

    [Fact]
    public void Write_KeepsTheTextTags()
    {
        var path = CreateMp3("song.mp3");
        new TagLibTrackTagWriter().Write(path, new TrackTags("Título", "Artista", "", "Álbum", 2020, 3, 0));

        new TagLibCoverArtWriter().Write(path, Png);
        var track = new TagLibTrackMetadataReader().Read(path);

        Assert.NotNull(track);
        Assert.Equal("Título", track.Title);
        Assert.Equal("Artista", track.Artist);
        Assert.Equal(3u, track.TrackNumber);
    }

    [Fact]
    public void Write_NotAnImage_ThrowsAndLeavesTheFileUntouched()
    {
        var path = CreateMp3("song.mp3");
        var before = File.ReadAllBytes(path);

        Assert.Throws<ArgumentException>(() => new TagLibCoverArtWriter().Write(path, new byte[] { 1, 2, 3 }));

        Assert.Equal(before, File.ReadAllBytes(path));
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);
}