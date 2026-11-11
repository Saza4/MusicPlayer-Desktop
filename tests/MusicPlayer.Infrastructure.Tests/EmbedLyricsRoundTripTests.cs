using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;
using MusicPlayer.Infrastructure;

namespace MusicPlayer.Infrastructure.Tests;

/// <summary>
/// Lo que hace «Incrustar letra (.lrc)…» (EmbedLyricsViewModel, M4) sin la interfaz: buscar los hermanos junto al
/// principal, incrustar, quitar los hermanos y leer la canción con la cadena de CompositionRoot.
/// </summary>
public class EmbedLyricsRoundTripTests : IDisposable
{
    private const string Main = "[00:01.00]Hello\n[00:03.00]World";

    private readonly string _dir = Directory.CreateTempSubdirectory("embed-roundtrip").FullName;
    private readonly LrcLyricParser _parser = new();
    private readonly LyricsSiblingFinder _finder = new("es");

    public static TheoryData<string> Formats => new() { "mp3", "flac", "m4a" };

    private string NewFile(string format) => format switch
    {
        "mp3" => TestAudio.CreateMp3(_dir),
        "flac" => TestAudio.CreateFlac(_dir),
        _ => TestAudio.CopyM4a(_dir)
    };

    private string Write(string name, string content)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, content);
        return path;
    }

    private async Task<LyricsEmbedReport> EmbedAsync(string audio, string main)
    {
        var track = new Track { FilePath = audio, Title = "Canción" };
        return await new LyricsEditor(new TagLibLyricsWriter(), _parser).EmbedAsync(track, main, _finder.Find(main));
    }

    private Task<LyricBlock> ReadAsync(string audio) =>
        new CompanionLyricsProvider(
                new CompositeLyricsProvider([new LrcFileLyricsProvider(_parser), new EmbeddedLyricsProvider(_parser)]),
                _parser, new TagLibLyricsLayerReader(), "es", "es-ES")
            .GetLyricsAsync(new Track { FilePath = audio, Title = "Canción" });

    // Prueba a mano 1 del autor: incrustar, mover los hermanos y abrir la canción.
    [Theory, MemberData(nameof(Formats))]
    public async Task EmbedThenRemoveTheSiblings_TheLayersComeFromTheAudio(string format)
    {
        var audio = NewFile(format);
        var main = Write("song.lrc", Main);
        Write("song.es.lrc", "[00:01.00]Hola\n[00:03.00]Mundo");
        Write("song.rom.lrc", "[00:01.00]Helo\n[00:03.00]Wurudo");

        var report = await EmbedAsync(audio, main);
        Assert.Equal(
            "Letra de \"Canción\" incrustada desde song.lrc: original, traducción (es) y pronunciación",
            report.Describe("Canción", "song.lrc"));

        foreach (var lrc in Directory.GetFiles(_dir, "*.lrc"))
            File.Delete(lrc);
        var block = await ReadAsync(audio);

        Assert.Equal("Hello", block.Lines[0].Text);
        Assert.Equal("Hola", block.Lines[0].Translation);
        Assert.Equal("Wurudo", block.Lines[1].Romanization);
    }

    // docs/specs/lrc-enhanced.md, criterios 12 y 13: las marcas <mm:ss.xx> viajan dentro del audio y se leen igual
    // (principal y traducción), sin que quede ningún «<» en el texto.
    [Theory, MemberData(nameof(Formats))]
    public async Task EnhancedLyrics_SurviveEmbedding(string format)
    {
        var audio = NewFile(format);
        var main = Write("song.lrc", "[00:01.00]<00:01.00>Hel<00:01.30>lo <00:01.60>mundo <00:02.20>\n[00:03.00]Uno <00:03.40>dos");
        Write("song.es.lrc", "[00:01.00]<00:01.00>Hola <00:01.60>mundo\n[00:03.00]Uno <00:03.40>dos");

        await EmbedAsync(audio, main);
        foreach (var lrc in Directory.GetFiles(_dir, "*.lrc"))
            File.Delete(lrc);
        var block = await ReadAsync(audio);

        var first = block.Lines[0];
        Assert.Equal("Hello mundo", first.Text);
        Assert.Equal(new[] { "Hel", "lo", "mundo" }, first.Words!.Select(w => w.Text));
        Assert.Equal(TimeSpan.FromSeconds(2.2), first.End);
        Assert.Equal("Hola mundo", first.Translation);
        Assert.DoesNotContain(block.Lines, l => l.Text.Contains('<') || l.Translation?.Contains('<') == true);
    }

    // Prueba a mano 3 del autor: un hermano inválido se salta y la línea de estado lo dice.
    [Theory, MemberData(nameof(Formats))]
    public async Task AnInvalidSibling_IsSkippedAndReported(string format)
    {
        var audio = NewFile(format);
        var main = Write("song.lrc", Main);
        Write("song.es.lrc", "[00:01.00]Hola");
        File.WriteAllBytes(Path.Combine(_dir, "song.en.lrc"), [0x5B, 0x30, 0x30, 0x5D, 0xFF, 0xFE, 0x41]); // no es UTF-8
        Write("song.rom.lrc", "");

        var report = await EmbedAsync(audio, main);

        Assert.Equal(
            "Letra de \"Canción\" incrustada desde song.lrc: original y traducción (es) · saltadas: " +
            "traducción (en), no está en UTF-8; pronunciación, está vacía",
            report.Describe("Canción", "song.lrc"));
    }

    // Criterio 6: con el selector, las capas se toman junto al archivo elegido, no junto al audio.
    [Fact]
    public async Task APickedMainFromAnotherFolder_TakesTheLayersNextToIt()
    {
        var audio = NewFile("mp3");
        Write("song.es.lrc", "[00:01.00]Hola junto al audio");
        var other = Directory.CreateDirectory(Path.Combine(_dir, "otra")).FullName;
        var picked = Path.Combine(other, "elegida.lrc");
        File.WriteAllText(picked, Main);
        File.WriteAllText(Path.Combine(other, "elegida.fr.lrc"), "[00:01.00]Salut");

        var report = await EmbedAsync(audio, picked);

        Assert.Equal("fr", Assert.Single(report.Embedded).Language);
        File.Delete(Path.Combine(_dir, "song.es.lrc"));
        Assert.Equal("Salut", (await ReadAsync(audio)).Lines[0].Translation);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);
}
