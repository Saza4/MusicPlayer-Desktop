using MusicPlayer.Core.Models;
using MusicPlayer.Infrastructure;

namespace MusicPlayer.Infrastructure.Tests;

public class TagLibLyricsWriterTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("lyricswriter-tests").FullName;

    private const string Lrc = "[00:01.00]Hola\r\n[00:02.50]Mundo";

    private string CreateMp3(string name)
    {
        var path = Path.Combine(_dir, name);
        var frame = new byte[417]; // MPEG1 capa 3, 128 kbps, 44,1 kHz
        frame[0] = 0xFF; frame[1] = 0xFB; frame[2] = 0x90; frame[3] = 0x00;

        using var stream = File.Create(path);
        for (int i = 0; i < 8; i++) stream.Write(frame);
        return path;
    }

    private static string? ReadLyrics(string path)
    {
        using var file = TagLib.File.Create(path);
        return file.Tag.Lyrics;
    }

    // Solo los bytes de audio, sin etiquetas.
    private static byte[] AudioBytes(string path)
    {
        long start, end;
        using (var file = TagLib.File.Create(path))
        {
            start = file.InvariantStartPosition;
            end = file.InvariantEndPosition;
        }
        return File.ReadAllBytes(path)[(int)start..(int)end];
    }

    [Fact]
    public void Write_EmbedsTheLyricsInTheFile()
    {
        var path = CreateMp3("song.mp3");

        new TagLibLyricsWriter().Write(path, Lrc);

        Assert.Equal(Lrc, ReadLyrics(path));
    }

    [Fact]
    public void Write_PreservesAccentsAndOtherScripts()
    {
        var path = CreateMp3("song.mp3");
        var text = "[00:01.00]夜に駆ける — canción ñ";

        new TagLibLyricsWriter().Write(path, text);

        Assert.Equal(text, ReadLyrics(path));
    }

    [Fact]
    public void Write_ReplacesThePreviousLyrics()
    {
        var path = CreateMp3("song.mp3");
        var writer = new TagLibLyricsWriter();
        writer.Write(path, "[00:01.00]Primera");

        writer.Write(path, "[00:01.00]Segunda");

        Assert.Equal("[00:01.00]Segunda", ReadLyrics(path));
    }

    [Fact]
    public void Write_KeepsTheTextTags()
    {
        var path = CreateMp3("song.mp3");
        new TagLibTrackTagWriter().Write(path, new TrackTags("Título", "Artista", "", "Álbum", 2020, 3, 0));

        new TagLibLyricsWriter().Write(path, Lrc);
        var track = new TagLibTrackMetadataReader().Read(path);

        Assert.NotNull(track);
        Assert.Equal("Título", track.Title);
        Assert.Equal("Artista", track.Artist);
        Assert.Equal(3u, track.TrackNumber);
    }

    [Fact]
    public void Write_DoesNotTouchTheAudio()
    {
        var path = CreateMp3("song.mp3");
        var before = File.ReadAllBytes(path);

        new TagLibLyricsWriter().Write(path, Lrc);

        Assert.Equal(before, AudioBytes(path));
    }

    [Fact]
    public void Write_LeavesNoTemporaryFilesBehind()
    {
        var path = CreateMp3("song.mp3");

        new TagLibLyricsWriter().Write(path, Lrc);

        Assert.Single(Directory.GetFiles(_dir));
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);
}