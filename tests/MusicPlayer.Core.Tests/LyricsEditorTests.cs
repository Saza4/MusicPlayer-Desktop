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
        public List<(string Path, string Lyrics, LyricsLayers Layers)> Calls { get; } = new();
        public void Write(string filePath, LyricsLayers layers) => Calls.Add((filePath, layers.Main, layers));
    }

    private static LyricsEditor NewEditor(ILyricsWriter writer) => new(writer, new LrcLyricParser());

    private string LrcFile(byte[] content, string name = "song.lrc")
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, content);
        return path;
    }

    private string LrcFile(string text, bool bom = false, string name = "song.lrc") =>
        LrcFile(new UTF8Encoding(bom).GetPreamble().Concat(Encoding.UTF8.GetBytes(text)).ToArray(), name);

    private const string Main = "[00:01.00]Hola\r\n[00:02.50]Mundo";

    private LyricsLayerSource Translation(string code, string text) =>
        new(LyricsLayerKind.Translation, code, LrcFile(text, name: $"song.{code}.lrc"));

    private LyricsLayerSource Pronunciation(string text) =>
        new(LyricsLayerKind.Pronunciation, null, LrcFile(text, name: "song.rom.lrc"));

    [Fact]
    public async Task Embed_WritesTheFileTextToTheTrack()
    {
        var text = "[00:01.00]Hola\r\n[00:02.50]Mundo";
        var writer = new FakeWriter();

        await NewEditor(writer).EmbedAsync(Song, LrcFile(text));

        var call = Assert.Single(writer.Calls);
        Assert.Equal("song.mp3", call.Path);
        Assert.Equal(text, call.Lyrics);
    }

    [Fact]
    public async Task Embed_KeepsAccentsAndOtherScripts()
    {
        var text = "[00:01.00]夜に駆ける — canción ñ";
        var writer = new FakeWriter();

        await NewEditor(writer).EmbedAsync(Song, LrcFile(text));

        Assert.Equal(text, Assert.Single(writer.Calls).Lyrics);
    }

    [Fact]
    public async Task Embed_Utf8WithBom_DropsTheBom()
    {
        var writer = new FakeWriter();

        await NewEditor(writer).EmbedAsync(Song, LrcFile("[00:01.00]Hola", bom: true));

        Assert.Equal("[00:01.00]Hola", Assert.Single(writer.Calls).Lyrics);
    }

    [Fact]
    public async Task Embed_NotUtf8_ThrowsAndWritesNothing()
    {
        var writer = new FakeWriter();
        var latin1 = LrcFile(new byte[] { 0x4C, 0xE9, 0x6F }); // "Léo" en Latin-1: UTF-8 inválido

        await Assert.ThrowsAsync<ArgumentException>(() => NewEditor(writer).EmbedAsync(Song, latin1));

        Assert.Empty(writer.Calls);
    }

    [Fact]
    public async Task Embed_EmptyFile_ThrowsAndWritesNothing()
    {
        var writer = new FakeWriter();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            NewEditor(writer).EmbedAsync(Song, LrcFile("  \r\n ")));

        Assert.Empty(writer.Calls);
    }

    [Fact]
    public async Task Embed_TooBig_ThrowsAndWritesNothing()
    {
        var writer = new FakeWriter();
        var big = LrcFile(new string('a', (int)LyricsEditor.MaxBytes + 1));

        await Assert.ThrowsAsync<ArgumentException>(() => NewEditor(writer).EmbedAsync(Song, big));

        Assert.Empty(writer.Calls);
    }

    [Fact]
    public async Task Embed_MissingFile_ThrowsAndWritesNothing()
    {
        var writer = new FakeWriter();

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            NewEditor(writer).EmbedAsync(Song, Path.Combine(_dir, "no-existe.lrc")));

        Assert.Empty(writer.Calls);
    }

    [Fact]
    public async Task EmbedWithLayers_WritesMainAndEveryLayerInOneWrite()
    {
        var writer = new FakeWriter();
        var es = Translation("es", "[00:01.00]Hola traducida");
        var rom = Pronunciation("[00:01.00]Ola");

        var report = await NewEditor(writer).EmbedAsync(Song, LrcFile(Main), [rom, es]);

        var call = Assert.Single(writer.Calls); // una sola escritura atómica
        Assert.Equal(Main, call.Layers.Main);
        Assert.Equal(
            [new LyricsLayer(LyricsLayerKind.Pronunciation, null, "[00:01.00]Ola"),
             new LyricsLayer(LyricsLayerKind.Translation, "es", "[00:01.00]Hola traducida")],
            call.Layers.Extras);
        Assert.Equal([rom, es], report.Embedded);
        Assert.Empty(report.Skipped);
    }

    [Fact]
    public async Task EmbedWithLayers_DoesNotTouchTheSiblingFiles()
    {
        var es = Translation("es", "[00:01.00]Hola traducida");
        var before = File.ReadAllBytes(es.Path);

        await NewEditor(new FakeWriter()).EmbedAsync(Song, LrcFile(Main), [es]);

        Assert.True(File.Exists(es.Path));
        Assert.Equal(before, File.ReadAllBytes(es.Path));
    }

    [Fact]
    public async Task EmbedWithLayers_InvalidMain_ThrowsAndWritesNothing()
    {
        var writer = new FakeWriter();
        var es = Translation("es", "[00:01.00]Hola traducida");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            NewEditor(writer).EmbedAsync(Song, LrcFile(new byte[] { 0x4C, 0xE9, 0x6F }), [es]));

        Assert.Empty(writer.Calls);
    }

    [Fact]
    public async Task EmbedWithLayers_InvalidLayers_AreSkippedWithTheirReason_AndTheRestIsWritten()
    {
        var writer = new FakeWriter();
        var good = Translation("es", "[00:01.00]Hola traducida");
        var latin1 = new LyricsLayerSource(LyricsLayerKind.Translation, "en",
            LrcFile(new byte[] { 0x4C, 0xE9, 0x6F }, "song.en.lrc"));
        var empty = Translation("fr", "  \r\n ");
        var big = new LyricsLayerSource(LyricsLayerKind.Translation, "it",
            LrcFile(new string('a', (int)LyricsEditor.MaxBytes + 1), name: "song.it.lrc"));
        var missing = new LyricsLayerSource(LyricsLayerKind.Pronunciation, null, Path.Combine(_dir, "no-existe.rom.lrc"));
        var noLyrics = Translation("de", "[ti:Solo cabecera]\n[ar:Nadie]");

        var report = await NewEditor(writer).EmbedAsync(Song, LrcFile(Main), [good, latin1, empty, big, missing, noLyrics]);

        Assert.Equal([new LyricsLayer(LyricsLayerKind.Translation, "es", "[00:01.00]Hola traducida")],
            Assert.Single(writer.Calls).Layers.Extras);
        Assert.Equal([good], report.Embedded);
        Assert.Equal(
            [new LyricsLayerSkip(latin1, "no está en UTF-8"),
             new LyricsLayerSkip(empty, "está vacía"),
             new LyricsLayerSkip(big, "pesa más de 1 MB"),
             new LyricsLayerSkip(missing, "no se encontró"),
             new LyricsLayerSkip(noLyrics, "no tiene letra que leer")],
            report.Skipped);
    }

    [Fact]
    public async Task EmbedWithLayers_ALayerOfExactly1MB_IsAccepted()
    {
        var writer = new FakeWriter();
        var text = "[00:01.00]" + new string('a', (int)LyricsEditor.MaxBytes - 10);
        var es = Translation("es", text);

        var report = await NewEditor(writer).EmbedAsync(Song, LrcFile(Main), [es]);

        Assert.Equal(LyricsEditor.MaxBytes, new FileInfo(es.Path).Length);
        Assert.Equal([es], report.Embedded);
    }

    [Fact]
    public async Task EmbedWithoutLayers_WritesOnlyTheMain()
    {
        var writer = new FakeWriter();

        var report = await NewEditor(writer).EmbedAsync(Song, LrcFile(Main), []);

        Assert.Empty(Assert.Single(writer.Calls).Layers.Extras);
        Assert.Empty(report.Embedded);
        Assert.Empty(report.Skipped);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);
}