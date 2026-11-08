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
        public List<(string Path, IReadOnlyList<LyricsLayer> Layers)> LayerCalls { get; } = new();
        public void WriteLayers(string filePath, IReadOnlyList<LyricsLayer> layers) => LayerCalls.Add((filePath, layers));
        public bool Layers { get; init; } = true;
        public bool SupportsLayers(string filePath) => Layers;
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

    // ---- Una sola capa elegida con el selector (criterio 12b) ----

    private static readonly LyricBlock SyncedMain = new LrcLyricParser().Parse(Main);

    private LyricsLayerSource Picked(LyricsLayerKind kind, string? language, string text, string name = "elegida.lrc") =>
        new(kind, language, LrcFile(text, name: name));

    [Fact]
    public async Task EmbedLayer_ATranslation_WritesOnlyThatLayerAndNeverTheMain()
    {
        var writer = new FakeWriter();
        var layer = Picked(LyricsLayerKind.Translation, "en", "[00:01.00]Hello\r\n[00:02.50]World");

        var report = await NewEditor(writer).EmbedLayerAsync(Song, layer, SyncedMain);

        Assert.Empty(writer.Calls);
        var call = Assert.Single(writer.LayerCalls);
        Assert.Equal("song.mp3", call.Path);
        var written = Assert.Single(call.Layers);
        Assert.Equal(new LyricsLayer(LyricsLayerKind.Translation, "en", "[00:01.00]Hello\r\n[00:02.50]World"), written);
        Assert.Equal([layer], report.Embedded);
        Assert.Empty(report.Skipped);
    }

    [Fact]
    public async Task EmbedLayer_APronunciation_WritesItWithoutLanguage()
    {
        var writer = new FakeWriter();

        await NewEditor(writer).EmbedLayerAsync(
            Song, Picked(LyricsLayerKind.Pronunciation, null, "[00:01.00]Ora\r\n[00:02.50]Mundo"), SyncedMain);

        var written = Assert.Single(Assert.Single(writer.LayerCalls).Layers);
        Assert.Equal(LyricsLayerKind.Pronunciation, written.Kind);
        Assert.Null(written.Language);
    }

    [Fact]
    public async Task EmbedLayer_Utf8WithBom_DropsTheBom()
    {
        var writer = new FakeWriter();
        var source = new LyricsLayerSource(LyricsLayerKind.Translation, "es", LrcFile("[00:01.00]Hola", bom: true, name: "b.lrc"));

        await NewEditor(writer).EmbedLayerAsync(Song, source, SyncedMain);

        Assert.Equal("[00:01.00]Hola", Assert.Single(Assert.Single(writer.LayerCalls).Layers).Text);
    }

    [Fact]
    public async Task EmbedLayer_WithoutTimestampsOnASyncedMain_IsSkippedWithItsReasonAndWritesNothing()
    {
        var writer = new FakeWriter();
        var layer = Picked(LyricsLayerKind.Translation, "es", "Hola\r\nMundo");

        var report = await NewEditor(writer).EmbedLayerAsync(Song, layer, SyncedMain);

        Assert.Empty(report.Embedded);
        var skip = Assert.Single(report.Skipped);
        Assert.Equal("no tiene marcas de tiempo como el original", skip.Reason);
        Assert.Empty(writer.LayerCalls);
        Assert.Empty(writer.Calls);
    }

    [Fact]
    public async Task EmbedLayer_NotUtf8_IsSkippedAndWritesNothing()
    {
        var writer = new FakeWriter();
        var source = new LyricsLayerSource(LyricsLayerKind.Translation, "es", LrcFile(new byte[] { 0x4C, 0xE9, 0x6F }, "l1.lrc"));

        var report = await NewEditor(writer).EmbedLayerAsync(Song, source, SyncedMain);

        Assert.Equal("no está en UTF-8", Assert.Single(report.Skipped).Reason);
        Assert.Empty(writer.LayerCalls);
    }

    [Fact]
    public async Task EmbedLayer_Empty_IsSkippedAndWritesNothing()
    {
        var writer = new FakeWriter();

        var report = await NewEditor(writer).EmbedLayerAsync(
            Song, Picked(LyricsLayerKind.Translation, "es", "  \r\n "), SyncedMain);

        Assert.Equal("está vacía", Assert.Single(report.Skipped).Reason);
        Assert.Empty(writer.LayerCalls);
    }

    [Fact]
    public async Task EmbedLayer_TooBig_IsSkippedAndWritesNothing()
    {
        var writer = new FakeWriter();
        var source = Picked(LyricsLayerKind.Translation, "es", new string('a', (int)LyricsEditor.MaxBytes + 1));

        var report = await NewEditor(writer).EmbedLayerAsync(Song, source, SyncedMain);

        Assert.Equal("pesa más de 1 MB", Assert.Single(report.Skipped).Reason);
        Assert.Empty(writer.LayerCalls);
    }

    [Fact]
    public async Task EmbedLayer_MissingFile_IsSkippedAndWritesNothing()
    {
        var writer = new FakeWriter();
        var source = new LyricsLayerSource(LyricsLayerKind.Translation, "es", Path.Combine(_dir, "no-existe.lrc"));

        var report = await NewEditor(writer).EmbedLayerAsync(Song, source, SyncedMain);

        Assert.Equal("no se encontró", Assert.Single(report.Skipped).Reason);
        Assert.Empty(writer.LayerCalls);
    }

    [Fact]
    public async Task EmbedLayer_FormatWithoutLayers_IsSkippedAndWritesNothing()
    {
        var writer = new FakeWriter { Layers = false };

        var report = await NewEditor(writer).EmbedLayerAsync(
            Song, Picked(LyricsLayerKind.Translation, "es", "[00:01.00]Hola"), SyncedMain);

        Assert.Equal("este formato no la guarda", Assert.Single(report.Skipped).Reason);
        Assert.Empty(writer.LayerCalls);
    }

    [Fact]
    public async Task EmbedLayer_MainWithoutLyrics_IsSkippedAndWritesNothing()
    {
        var writer = new FakeWriter();

        var report = await NewEditor(writer).EmbedLayerAsync(
            Song, Picked(LyricsLayerKind.Translation, "es", "[00:01.00]Hola"), LyricBlock.Empty);

        Assert.Equal("el original no tiene letra que leer", Assert.Single(report.Skipped).Reason);
        Assert.Empty(writer.LayerCalls);
    }

    [Fact]
    public async Task EmbedLayer_PlainMain_NeedsTheSameNumberOfLines()
    {
        var writer = new FakeWriter();
        var plainMain = new LrcLyricParser().Parse("Hola\r\nMundo");
        var same = Picked(LyricsLayerKind.Translation, "en", "Hello\r\nWorld", "same.lrc");
        var other = Picked(LyricsLayerKind.Translation, "fr", "Bonjour", "other.lrc");

        var ok = await NewEditor(writer).EmbedLayerAsync(Song, same, plainMain);
        var bad = await NewEditor(writer).EmbedLayerAsync(Song, other, plainMain);

        Assert.Single(ok.Embedded);
        Assert.Equal("no tiene tantas líneas como el original", Assert.Single(bad.Skipped).Reason);
        Assert.Single(writer.LayerCalls);
    }

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

    // Criterio 5: una capa de otro tipo que el principal se ignoraría al leer (LyricsMerger), así que no se incrusta.
    [Fact]
    public async Task EmbedWithLayers_SyncedMain_SkipsALayerWithoutTimestamps()
    {
        var writer = new FakeWriter();
        var es = Translation("es", "[00:01.00]Hola traducida");
        var plain = Translation("en", "Hello\nWorld");
        var rom = Pronunciation("Ola\nMundo");

        var report = await NewEditor(writer).EmbedAsync(Song, LrcFile(Main), [es, plain, rom]);

        Assert.Equal([new LyricsLayer(LyricsLayerKind.Translation, "es", "[00:01.00]Hola traducida")],
            Assert.Single(writer.Calls).Layers.Extras);
        Assert.Equal([es], report.Embedded);
        Assert.Equal(
            [new LyricsLayerSkip(plain, "no tiene marcas de tiempo como el original"),
             new LyricsLayerSkip(rom, "no tiene marcas de tiempo como el original")],
            report.Skipped);
        Assert.Equal(
            "Letra de \"T\" incrustada desde song.lrc: original y traducción (es) · saltadas: " +
            "traducción (en), no tiene marcas de tiempo como el original; pronunciación, no tiene marcas de tiempo como el original",
            report.Describe("T", "song.lrc"));
    }

    [Fact]
    public async Task EmbedWithLayers_PlainMain_KeepsPlainLayersAndSkipsSyncedOnes()
    {
        var writer = new FakeWriter();
        var plain = Translation("es", "Hola\nMundo");
        var synced = Translation("en", "[00:01.00]Hello");

        var report = await NewEditor(writer).EmbedAsync(Song, LrcFile("Hello\nWorld"), [plain, synced]);

        Assert.Equal([plain], report.Embedded);
        Assert.Equal([new LyricsLayerSkip(synced, "tiene marcas de tiempo y el original no")], report.Skipped);
        Assert.Equal("Hola\nMundo", Assert.Single(Assert.Single(writer.Calls).Layers.Extras).Text);
    }

    // Sin marcas, LyricsMerger empareja por posición y solo con el mismo número de líneas: si no, la capa nunca se vería.
    [Fact]
    public async Task EmbedWithLayers_PlainMain_SkipsAPlainLayerWithAnotherLineCount()
    {
        var writer = new FakeWriter();
        var same = Translation("es", "Hola\n\nMundo\n");       // las líneas vacías no cuentan, igual que al leer
        var fewer = Translation("en", "Hello");
        var more = Pronunciation("Ola\nMundo\nOtra");

        var report = await NewEditor(writer).EmbedAsync(Song, LrcFile("Hello\nWorld"), [same, fewer, more]);

        Assert.Equal([same], report.Embedded);
        Assert.Equal(
            [new LyricsLayerSkip(fewer, "no tiene tantas líneas como el original"),
             new LyricsLayerSkip(more, "no tiene tantas líneas como el original")],
            report.Skipped);
    }

    // Un principal solo con metadatos se incrusta como hoy, pero sus capas nunca se verían.
    [Fact]
    public async Task EmbedWithLayers_MainWithoutLyrics_SkipsEveryLayer()
    {
        var writer = new FakeWriter();
        var es = Translation("es", "[00:01.00]Hola");

        var report = await NewEditor(writer).EmbedAsync(Song, LrcFile("[ti:Solo cabecera]"), [es]);

        // Alcanzable: la validación del principal (desde M1) no exige que el analizador saque letra, y se escribe.
        var call = Assert.Single(writer.Calls);
        Assert.Equal("[ti:Solo cabecera]", call.Lyrics);
        Assert.Empty(call.Layers.Extras);
        Assert.Equal([new LyricsLayerSkip(es, "el original no tiene letra que leer")], report.Skipped);
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
    public async Task EmbedWithLayers_FormatWithoutLayers_WritesTheMainAndSkipsEveryLayer()
    {
        var writer = new FakeWriter { Layers = false };
        var es = Translation("es", "[00:01.00]Hola traducida");
        var rom = Pronunciation("[00:01.00]Ola");

        var report = await NewEditor(writer).EmbedAsync(Song, LrcFile(Main), [es, rom]);

        var call = Assert.Single(writer.Calls);
        Assert.Equal(Main, call.Lyrics);
        Assert.Empty(call.Layers.Extras);
        Assert.Empty(report.Embedded);
        Assert.Equal([new LyricsLayerSkip(es, "este formato no la guarda"), new LyricsLayerSkip(rom, "este formato no la guarda")],
            report.Skipped);
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