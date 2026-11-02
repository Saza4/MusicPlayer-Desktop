using System.Text;
using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;

namespace MusicPlayer.Core.Tests;

public class LyricsEditorTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("lyrics-tests").FullName;
    private static readonly Track Song = new() { FilePath = "song.mp3", Title = "T" };

    private sealed class FakeWriter : ILyricsWriter
    {
        public List<(string Path, string Lyrics)> Calls { get; } = new();
        public void Write(string filePath, string lyrics) => Calls.Add((filePath, lyrics));
    }

    private string LrcFile(byte[] content)
    {
        var path = Path.Combine(_dir, "song.lrc");
        File.WriteAllBytes(path, content);
        return path;
    }

    private string LrcFile(string text, bool bom = false) =>
        LrcFile(new UTF8Encoding(bom).GetPreamble().Concat(Encoding.UTF8.GetBytes(text)).ToArray());

    [Fact]
    public async Task Embed_WritesTheFileTextToTheTrack()
    {
        var text = "[00:01.00]Hola\r\n[00:02.50]Mundo";
        var writer = new FakeWriter();

        await new LyricsEditor(writer).EmbedAsync(Song, LrcFile(text));

        var call = Assert.Single(writer.Calls);
        Assert.Equal("song.mp3", call.Path);
        Assert.Equal(text, call.Lyrics);
    }

    [Fact]
    public async Task Embed_KeepsAccentsAndOtherScripts()
    {
        var text = "[00:01.00]夜に駆ける — canción ñ";
        var writer = new FakeWriter();

        await new LyricsEditor(writer).EmbedAsync(Song, LrcFile(text));

        Assert.Equal(text, Assert.Single(writer.Calls).Lyrics);
    }

    [Fact]
    public async Task Embed_Utf8WithBom_DropsTheBom()
    {
        var writer = new FakeWriter();

        await new LyricsEditor(writer).EmbedAsync(Song, LrcFile("[00:01.00]Hola", bom: true));

        Assert.Equal("[00:01.00]Hola", Assert.Single(writer.Calls).Lyrics);
    }

    [Fact]
    public async Task Embed_NotUtf8_ThrowsAndWritesNothing()
    {
        var writer = new FakeWriter();
        var latin1 = LrcFile(new byte[] { 0x4C, 0xE9, 0x6F }); // "Léo" en Latin-1: UTF-8 inválido

        await Assert.ThrowsAsync<ArgumentException>(() => new LyricsEditor(writer).EmbedAsync(Song, latin1));

        Assert.Empty(writer.Calls);
    }

    [Fact]
    public async Task Embed_EmptyFile_ThrowsAndWritesNothing()
    {
        var writer = new FakeWriter();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            new LyricsEditor(writer).EmbedAsync(Song, LrcFile("  \r\n ")));

        Assert.Empty(writer.Calls);
    }

    [Fact]
    public async Task Embed_TooBig_ThrowsAndWritesNothing()
    {
        var writer = new FakeWriter();
        var big = LrcFile(new string('a', (int)LyricsEditor.MaxBytes + 1));

        await Assert.ThrowsAsync<ArgumentException>(() => new LyricsEditor(writer).EmbedAsync(Song, big));

        Assert.Empty(writer.Calls);
    }

    [Fact]
    public async Task Embed_MissingFile_ThrowsAndWritesNothing()
    {
        var writer = new FakeWriter();

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            new LyricsEditor(writer).EmbedAsync(Song, Path.Combine(_dir, "no-existe.lrc")));

        Assert.Empty(writer.Calls);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);
}