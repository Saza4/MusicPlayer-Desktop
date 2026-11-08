using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;
using MusicPlayer.Infrastructure;
using TagLib;
using TagLib.Id3v2;
using TagLib.Mpeg4;
using TagLib.Ogg;
using File = System.IO.File;

namespace MusicPlayer.Infrastructure.Tests;

/// <summary>Escritura y lectura de las capas en MP3, FLAC y M4A (docs/specs/lyrics-layers.md, M2).</summary>
public class TagLibLyricsLayersWriterTests : IDisposable
{
    private const string Main = "[00:01.00]Principal inventada\r\n[00:03.00]Segunda línea";
    private const string ITunes = "com.apple.iTunes";

    private readonly string _dir = Directory.CreateTempSubdirectory("layers-writer").FullName;
    private readonly TagLibLyricsWriter _writer = new();

    public static TheoryData<string> Formats => new() { "mp3", "flac", "m4a" };

    private string NewFile(string format) => format switch
    {
        "mp3" => TestAudio.CreateMp3(_dir),
        "flac" => TestAudio.CreateFlac(_dir),
        _ => TestAudio.CopyM4a(_dir)
    };

    private static LyricsLayer Tr(string code, string text) => new(LyricsLayerKind.Translation, code, text);
    private static LyricsLayer Rom(string text) => new(LyricsLayerKind.Pronunciation, null, text);

    private static IReadOnlyList<LyricsLayer> ReadLayers(string path)
    {
        using var file = TagLib.File.Create(path);
        return TagLibLyricsLayerFields.Read(file).OrderBy(l => l.Kind == LyricsLayerKind.Translation).ThenBy(l => l.Language, StringComparer.Ordinal).ToList();
    }

    // PNG de 1×1 píxel.
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private static Task<byte[]?> ReadCover(string path) =>
        new EmbeddedCoverArtProvider().GetCoverAsync(new Track { FilePath = path, Title = "x" });

    private static string? ReadMain(string path)
    {
        using var file = TagLib.File.Create(path);
        return file.Tag.Lyrics;
    }

    // Nombres de campo tal como quedan en el archivo, leídos con TagLib directamente (sin el código de la app).
    private static List<string> RawNames(string path)
    {
        using var file = TagLib.File.Create(path);
        return file switch
        {
            TagLib.Mpeg.AudioFile => ((TagLib.Id3v2.Tag)file.GetTag(TagTypes.Id3v2, false))
                .GetFrames<UserTextInformationFrame>().Select(f => f.Description).ToList(),
            TagLib.Flac.File => ((XiphComment)file.GetTag(TagTypes.Xiph, false)).Where(n => n.StartsWith("LYRICS_")).ToList(),
            _ => ((AppleTag)file.GetTag(TagTypes.Apple, false)).Where(b => b.BoxType == "----")
                .Select(b => b.Children.OfType<AppleAdditionalInfoBox>().First(c => c.BoxType == "name").Text).ToList()
        };
    }

    // Escribe un campo con TagLib directamente, como lo haría otro programa.
    private static void WriteRaw(string path, string name, string text)
    {
        using var file = TagLib.File.Create(path);
        switch (file)
        {
            case TagLib.Mpeg.AudioFile:
                UserTextInformationFrame.Get((TagLib.Id3v2.Tag)file.GetTag(TagTypes.Id3v2, true), name, StringType.UTF16, true).Text = [text];
                break;
            case TagLib.Flac.File:
                ((XiphComment)file.GetTag(TagTypes.Xiph, true)).SetField(name, text);
                break;
            default:
                ((AppleTag)file.GetTag(TagTypes.Apple, true)).SetDashBox(ITunes, name, text);
                break;
        }
        file.Save();
    }

    private static byte[] AudioBytes(string path)
    {
        // En FLAC, TagLib da todo el archivo como «invariante»: se comparan el STREAMINFO (siempre el primer bloque) y
        // el marco de audio del final (TestAudio.CreateFlac).
        if (path.EndsWith(".flac"))
        {
            var bytes = File.ReadAllBytes(path);
            return [.. bytes[8..42], .. bytes[^TestAudio.FlacFrameLength..]];
        }

        long start, end;
        using (var file = TagLib.File.Create(path))
        {
            start = file.InvariantStartPosition;
            end = file.InvariantEndPosition;
        }
        return File.ReadAllBytes(path)[(int)start..(int)end];
    }

    [Theory, MemberData(nameof(Formats))]
    public void Write_MainAndLayers_RoundTrip(string format)
    {
        var path = NewFile(format);
        var es = Tr("es", "[00:01.00]Traducción inventada / con barra; ñ\r\n[00:03.00]Otra");
        var rom = Rom("[00:01.00]夜に駆ける\r\n[00:03.00]Yoru ni");

        _writer.Write(path, new LyricsLayers(Main, [es, rom]));

        Assert.Equal(Main, ReadMain(path));
        Assert.Equal([rom, es], ReadLayers(path));
        Assert.Equal(["LYRICS_TRANSLATION_ES", "LYRICS_PRONUNCIATION"], RawNames(path).Order(StringComparer.Ordinal).Reverse());
    }

    [Theory, MemberData(nameof(Formats))]
    public void Write_HyphenatedCodes_AreStoredUppercase_AndReadBackNormalized(string format)
    {
        var path = NewFile(format);

        _writer.Write(path, new LyricsLayers(Main, [Tr("pt-BR", "[00:01.00]Olá"), Tr("zh-Hans", "[00:01.00]你好"), Tr("es-419", "[00:01.00]Órale")]));

        Assert.Equal(["LYRICS_TRANSLATION_ES-419", "LYRICS_TRANSLATION_PT-BR", "LYRICS_TRANSLATION_ZH-HANS"],
            RawNames(path).Order(StringComparer.Ordinal));
        Assert.Equal([Tr("es-419", "[00:01.00]Órale"), Tr("pt-BR", "[00:01.00]Olá"), Tr("zh-Hans", "[00:01.00]你好")], ReadLayers(path));
    }

    [Theory, MemberData(nameof(Formats))]
    public void Write_FieldsWrittenByAnotherProgramInLowercase_AreReadAndReplacedWithoutDuplicates(string format)
    {
        var path = NewFile(format);
        WriteRaw(path, "lyrics_translation_pt-br", "[00:01.00]Antes");

        Assert.Equal([Tr("pt-BR", "[00:01.00]Antes")], ReadLayers(path)); // se lee sin distinguir mayúsculas

        _writer.Write(path, new LyricsLayers(Main, [Tr("pt-BR", "[00:01.00]Después")]));

        Assert.Equal(["LYRICS_TRANSLATION_PT-BR"], RawNames(path));
        Assert.Equal([Tr("pt-BR", "[00:01.00]Después")], ReadLayers(path));
    }

    [Theory, MemberData(nameof(Formats))]
    public void Rewrite_ReplacesOnlyTheLayersWithASource_AndNeverDuplicates(string format)
    {
        var path = NewFile(format);
        _writer.Write(path, new LyricsLayers("[00:01.00]Primera", [Tr("es", "[00:01.00]Uno"), Rom("[00:01.00]Rom uno")]));

        _writer.Write(path, new LyricsLayers("[00:01.00]Segunda", [Tr("es", "[00:01.00]Dos")]));
        _writer.Write(path, new LyricsLayers("[00:01.00]Segunda", [Tr("es", "[00:01.00]Dos")]));

        Assert.Equal("[00:01.00]Segunda", ReadMain(path));
        Assert.Equal([Rom("[00:01.00]Rom uno"), Tr("es", "[00:01.00]Dos")], ReadLayers(path)); // la pronunciación sin fuente se conserva
        Assert.Equal(2, RawNames(path).Count);
    }

    [Theory, MemberData(nameof(Formats))]
    public void Write_MainOnly_KeepsTheLayersAlreadyEmbedded(string format)
    {
        var path = NewFile(format);
        _writer.Write(path, new LyricsLayers(Main, [Tr("es", "[00:01.00]Uno")]));

        _writer.Write(path, LyricsLayers.MainOnly("[00:01.00]Nueva"));

        Assert.Equal([Tr("es", "[00:01.00]Uno")], ReadLayers(path));
    }

    // Pedido del autor (a): borrar la letra principal no toca las capas.
    [Theory, MemberData(nameof(Formats))]
    public void ClearingTheMainLyrics_KeepsTheLayers(string format)
    {
        var path = NewFile(format);
        _writer.Write(path, new LyricsLayers(Main, [Tr("es", "[00:01.00]Uno"), Rom("[00:01.00]Rom")]));

        using (var file = TagLib.File.Create(path))
        {
            file.Tag.Lyrics = null;
            file.Save();
        }

        Assert.True(string.IsNullOrEmpty(ReadMain(path)));
        Assert.Equal([Rom("[00:01.00]Rom"), Tr("es", "[00:01.00]Uno")], ReadLayers(path));
    }

    // Pedido del autor (b): el editor de etiquetas y «Limpiar etiquetas» escriben con TagLibTrackTagWriter (vía TagEditor).
    [Theory, MemberData(nameof(Formats))]
    public void TheTagEditor_KeepsTheLayers(string format)
    {
        var path = NewFile(format);
        _writer.Write(path, new LyricsLayers(Main, [Tr("pt-BR", "[00:01.00]Olá"), Rom("[00:01.00]Rom")]));

        new TagLibTrackTagWriter().Write(path, new TrackTags("Título", "Artista", "", "Álbum", 2020, 3, 0));
        new TagLibTrackTagWriter().Write(path, new TrackTags("Título limpio", "Artista", "", "", 0, 0, 0));

        Assert.Equal(Main, ReadMain(path));
        Assert.Equal([Rom("[00:01.00]Rom"), Tr("pt-BR", "[00:01.00]Olá")], ReadLayers(path));
    }

    // Pedido del autor (M3): poner y quitar la carátula (TagLibCoverArtWriter, vía CoverEditor) conserva las capas.
    [Theory, MemberData(nameof(Formats))]
    public async Task SettingAndRemovingTheCover_KeepsTheLayers(string format)
    {
        var path = NewFile(format);
        _writer.Write(path, new LyricsLayers(Main, [Tr("pt-BR", "[00:01.00]Olá"), Rom("[00:01.00]Rom")]));
        var cover = new TagLibCoverArtWriter();

        cover.Write(path, Png);
        Assert.Equal(Png, await ReadCover(path));
        Assert.Equal(Main, ReadMain(path));
        Assert.Equal([Rom("[00:01.00]Rom"), Tr("pt-BR", "[00:01.00]Olá")], ReadLayers(path));

        cover.Write(path, null);
        Assert.Null(await ReadCover(path));
        Assert.Equal(Main, ReadMain(path));
        Assert.Equal([Rom("[00:01.00]Rom"), Tr("pt-BR", "[00:01.00]Olá")], ReadLayers(path));
    }

    // Pedido del autor (M3): aplicar una carátula a todo un álbum (CoverEditor.SetManyAsync) conserva las capas de cada pista.
    [Fact]
    public async Task ApplyingACoverToAWholeAlbum_KeepsTheLayersOfEveryTrack()
    {
        var paths = Formats.Select(f => NewFile((string)f[0])).ToList();
        foreach (var p in paths)
            _writer.Write(p, new LyricsLayers(Main, [Tr("es", "[00:01.00]Hola " + Path.GetExtension(p)), Rom("[00:01.00]Rom")]));
        var library = new LibraryService(
            new LibraryScanner(new TagLibTrackMetadataReader()),
            new JsonLibraryRepository(Path.Combine(_dir, "sin-usar", "library.json")));
        var tracks = paths.Select(p => new Track { FilePath = p, Title = Path.GetFileName(p), Album = "Álbum" }).ToList();

        var result = await new CoverEditor(new TagLibCoverArtWriter(), library).SetManyAsync(tracks, Png);

        Assert.Equal(3, result.Written);
        Assert.Empty(result.Errors);
        foreach (var p in paths)
        {
            Assert.Equal(Png, await ReadCover(p));
            Assert.Equal(Main, ReadMain(p));
            Assert.Equal([Rom("[00:01.00]Rom"), Tr("es", "[00:01.00]Hola " + Path.GetExtension(p))], ReadLayers(p));
        }
    }

    // Pedido del autor (c): una capa de exactamente 1 MB (UTF-8, el límite) se escribe y se relee.
    [Theory, MemberData(nameof(Formats))]
    public void ALayerOfExactly1MB_IsWrittenAndReadBack(string format)
    {
        var path = NewFile(format);
        var text = "[00:01.00]" + new string('a', (int)LyricsEditor.MaxBytes - 10);
        Assert.Equal(LyricsEditor.MaxBytes, System.Text.Encoding.UTF8.GetByteCount(text));

        _writer.Write(path, new LyricsLayers(Main, [Tr("es", text)]));

        Assert.Equal(text, Assert.Single(ReadLayers(path)).Text);
    }

    [Theory, MemberData(nameof(Formats))]
    public void Write_KeepsTheTextTagsAndTheAudio_AndLeavesNoTemporaryFiles(string format)
    {
        var path = NewFile(format);
        new TagLibTrackTagWriter().Write(path, new TrackTags("Título", "Artista", "", "Álbum", 2020, 3, 0));
        var audio = AudioBytes(path);

        _writer.Write(path, new LyricsLayers(Main, [Tr("es", "[00:01.00]Uno"), Rom("[00:01.00]Rom")]));

        var track = new TagLibTrackMetadataReader().Read(path);
        Assert.NotNull(track);
        Assert.Equal("Título", track.Title);
        Assert.Equal(3u, track.TrackNumber);
        Assert.Equal(audio, AudioBytes(path));
        Assert.Single(Directory.GetFiles(_dir));
    }

    [Theory]
    [InlineData("song.flac")]
    [InlineData("song.m4a")]
    [InlineData("song.mp3")]
    public void Write_CorruptFile_FailsWithoutTouchingTheOriginal(string name)
    {
        var path = Path.Combine(_dir, name);
        var garbage = Enumerable.Range(0, 64).Select(i => (byte)(i * 7)).ToArray();
        File.WriteAllBytes(path, garbage);

        Assert.ThrowsAny<Exception>(() => _writer.Write(path, new LyricsLayers(Main, [Tr("es", "[00:01.00]Uno")])));

        Assert.Equal(garbage, File.ReadAllBytes(path));
        Assert.Single(Directory.GetFiles(_dir));
    }

    [Theory]
    [InlineData("song.mp3", true)]
    [InlineData("song.FLAC", true)]
    [InlineData("song.m4a", true)]
    [InlineData("song.wav", false)]
    [InlineData("song.ogg", false)]
    public void SupportsLayers_OnlyMp3FlacAndM4a(string name, bool expected) =>
        Assert.Equal(expected, _writer.SupportsLayers(name));

    [Fact]
    public void Write_LayersOnAnUnsupportedFormat_Throws() =>
        Assert.Throws<NotSupportedException>(() =>
            _writer.Write(Path.Combine(_dir, "song.wav"), new LyricsLayers(Main, [Tr("es", "[00:01.00]Uno")])));

    public void Dispose() => Directory.Delete(_dir, recursive: true);
}
