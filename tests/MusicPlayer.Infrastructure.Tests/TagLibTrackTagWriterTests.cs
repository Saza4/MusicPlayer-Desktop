using MusicPlayer.Core.Models;
using MusicPlayer.Infrastructure;

namespace MusicPlayer.Infrastructure.Tests;

public class TagLibTrackTagWriterTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("tagwriter-tests").FullName;

    private string CreateMp3(string name)
    {
        var path = Path.Combine(_dir, name);
        var frame = new byte[417]; // MPEG1 capa 3, 128 kbps, 44,1 kHz
        frame[0] = 0xFF; frame[1] = 0xFB; frame[2] = 0x90; frame[3] = 0x00;

        using var stream = File.Create(path);
        for (int i = 0; i < 8; i++) stream.Write(frame);
        return path;
    }

    [Fact]
    public void Write_ThenRead_RoundTripsTheTags()
    {
        var path = CreateMp3("song.mp3");

        new TagLibTrackTagWriter().Write(path,
            new TrackTags("Título", "Artista", "Various Artists", "Álbum", 2020, 3, 2));
        var track = new TagLibTrackMetadataReader().Read(path);

        Assert.NotNull(track);
        Assert.Equal("Título", track.Title);
        Assert.Equal("Artista", track.Artist);
        Assert.Equal("Various Artists", track.AlbumArtist);
        Assert.Equal("Álbum", track.Album);
        Assert.Equal(2020u, track.Year);
        Assert.Equal(3u, track.TrackNumber);
        Assert.Equal(2u, track.DiscNumber);
    }

    [Fact]
    public void Write_EmptyFields_ClearTheTags()
    {
        var path = CreateMp3("song.mp3");
        var writer = new TagLibTrackTagWriter();
        writer.Write(path, new TrackTags("Título", "Artista", "Various Artists", "Álbum", 2020, 3, 2));

        writer.Write(path, new TrackTags("", "", "", "", 0, 0, 0));
        var track = new TagLibTrackMetadataReader().Read(path);

        Assert.NotNull(track);
        Assert.Equal("song", track.Title); // sin título, el lector usa el nombre del archivo
        Assert.Equal(Track.UnknownArtist, track.Artist);
        Assert.Equal(Track.UnknownAlbum, track.Album);
        Assert.Equal("", track.AlbumArtist);
        Assert.Equal(0u, track.Year);
    }

    [Fact]
    public void Write_LeavesNoTemporaryFilesBehind()
    {
        var path = CreateMp3("song.mp3");
        new TagLibTrackTagWriter().Write(path, new TrackTags("Título", "Artista", "", "", 0, 1, 0));
        Assert.Single(Directory.GetFiles(_dir));
    }

    [Fact]
    public void Write_ReadOnlyFile_ThrowsAndKeepsTheFileUntouched()
    {
        var path = CreateMp3("readonly.mp3");
        var before = File.ReadAllBytes(path);
        File.SetAttributes(path, FileAttributes.ReadOnly);
        try
        {
            Assert.Throws<UnauthorizedAccessException>(() =>
                new TagLibTrackTagWriter().Write(path, new TrackTags("Nuevo", "", "", "", 0, 0, 0)));
            Assert.Equal(before, File.ReadAllBytes(path));
            Assert.Single(Directory.GetFiles(_dir));
        }
        finally { File.SetAttributes(path, FileAttributes.Normal); } // para que Dispose pueda borrar la carpeta
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);
}