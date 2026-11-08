using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;
using MusicPlayer.Infrastructure;

namespace MusicPlayer.Infrastructure.Tests;

/// <summary>
/// Lectura de las capas incrustadas y prioridad frente a los hermanos (docs/specs/lyrics-layers.md, criterios 8-10, M3).
/// Los archivos se escriben con <see cref="TagLibLyricsWriter"/>, como lo hace «Incrustar letra (.lrc)…».
/// </summary>
public class EmbeddedLyricsLayersReadTests : IDisposable
{
    private const string Main = "[00:01.00]Hello\n[00:03.00]World";

    private readonly string _dir = Directory.CreateTempSubdirectory("layers-read").FullName;
    private readonly LrcLyricParser _parser = new();

    public static TheoryData<string> Formats => new() { "mp3", "flac", "m4a" };

    private string NewFile(string format) => format switch
    {
        "mp3" => TestAudio.CreateMp3(_dir),
        "flac" => TestAudio.CreateFlac(_dir),
        _ => TestAudio.CopyM4a(_dir)
    };

    private static LyricsLayer Tr(string code, string text) => new(LyricsLayerKind.Translation, code, text);
    private static LyricsLayer Rom(string text) => new(LyricsLayerKind.Pronunciation, null, text);

    private static void Embed(string path, params LyricsLayer[] layers) =>
        new TagLibLyricsWriter().Write(path, new LyricsLayers(Main, layers));

    private void Sibling(string name, string content) => File.WriteAllText(Path.Combine(_dir, name), content);

    // La misma cadena que CompositionRoot: «cancion.lrc», si no la incrustada, y encima las capas.
    private Task<LyricBlock> ReadAsync(string path, string culture = "es-ES")
    {
        var provider = new CompanionLyricsProvider(
            new CompositeLyricsProvider([new LrcFileLyricsProvider(_parser), new EmbeddedLyricsProvider(_parser)]),
            _parser, new TagLibLyricsLayerReader(), culture.Split('-')[0], culture);
        return provider.GetLyricsAsync(new Track { FilePath = path, Title = "song" });
    }

    // Criterio 8: sin hermanos, se muestran las capas incrustadas (y la principal incrustada).
    [Theory, MemberData(nameof(Formats))]
    public async Task OnlyEmbedded_ShowsTranslationAndPronunciation(string format)
    {
        var path = NewFile(format);
        Embed(path, Tr("es", "[00:01.00]Hola\n[00:03.00]Mundo"), Rom("[00:01.00]Helo\n[00:03.00]Wurudo"));

        var block = await ReadAsync(path);

        Assert.Equal("Hello", block.Lines[0].Text);
        Assert.Equal("Hola", block.Lines[0].Translation);
        Assert.Equal("Wurudo", block.Lines[1].Romanization);
    }

    [Theory, MemberData(nameof(Formats))]
    public async Task OnlySiblings_StillWork(string format)
    {
        var path = NewFile(format);
        Embed(path);
        Sibling("song.es.lrc", "[00:01.00]Hola");
        Sibling("song.rom.lrc", "[00:01.00]Helo");

        var block = await ReadAsync(path);

        Assert.Equal("Hola", block.Lines[0].Translation);
        Assert.Equal("Helo", block.Lines[0].Romanization);
    }

    // Criterios 9 y 10: con el mismo tipo y código, el hermano gana a la incrustada.
    [Theory, MemberData(nameof(Formats))]
    public async Task Both_TheSiblingWins(string format)
    {
        var path = NewFile(format);
        Embed(path, Tr("es", "[00:01.00]Hola incrustada"), Rom("[00:01.00]Rom incrustada"));
        Sibling("song.es.lrc", "[00:01.00]Hola hermana");
        Sibling("song.rom.lrc", "[00:01.00]Rom hermana");

        var block = await ReadAsync(path);

        Assert.Equal("Hola hermana", block.Lines[0].Translation);
        Assert.Equal("Rom hermana", block.Lines[0].Romanization);
    }

    // Cada capa por su lado: la traducción del hermano y la pronunciación incrustada, o al revés.
    [Theory, MemberData(nameof(Formats))]
    public async Task Both_EachLayerComesFromWhereItIs(string format)
    {
        var path = NewFile(format);
        Embed(path, Rom("[00:01.00]Rom incrustada"));
        Sibling("song.es.lrc", "[00:01.00]Hola hermana");

        var block = await ReadAsync(path);

        Assert.Equal("Hola hermana", block.Lines[0].Translation);
        Assert.Equal("Rom incrustada", block.Lines[0].Romanization);
    }

    // Criterio 10: se elige primero por idioma, entre hermanas e incrustadas juntas.
    [Fact]
    public async Task TheLanguageIsChosenFirst_AnEmbeddedSystemLanguageBeatsASiblingInAnother()
    {
        var path = NewFile("mp3");
        Embed(path, Tr("es", "[00:01.00]Hola incrustada"));
        Sibling("song.en.lrc", "[00:01.00]Hi sibling");

        Assert.Equal("Hola incrustada", (await ReadAsync(path, "es-ES")).Lines[0].Translation);
        Assert.Equal("Hi sibling", (await ReadAsync(path, "en-US")).Lines[0].Translation);
    }

    [Theory, MemberData(nameof(Formats))]
    public async Task SeveralEmbeddedTranslations_PicksBySystemLanguage_ThenAlphabetically(string format)
    {
        var path = NewFile(format);
        Embed(path, Tr("fr", "[00:01.00]Salut"), Tr("pt-BR", "[00:01.00]Olá"), Tr("en", "[00:01.00]Hi"), Tr("pt-PT", "[00:01.00]Olá PT"));

        Assert.Equal("Salut", (await ReadAsync(path, "fr-FR")).Lines[0].Translation);
        Assert.Equal("Olá", (await ReadAsync(path, "pt-BR")).Lines[0].Translation);
        Assert.Equal("Olá", (await ReadAsync(path, "pt-AO")).Lines[0].Translation);   // mismo idioma: pt-BR antes que pt-PT
        Assert.Equal("Hi", (await ReadAsync(path, "de-DE")).Lines[0].Translation);    // primera alfabética
    }

    [Fact]
    public async Task SeveralTranslations_SiblingsAndEmbeddedMixed()
    {
        var path = NewFile("flac");
        Embed(path, Tr("fr", "[00:01.00]Salut incrustada"), Tr("es", "[00:01.00]Hola incrustada"));
        Sibling("song.de.lrc", "[00:01.00]Hallo hermana");
        Sibling("song.es.lrc", "[00:01.00]Hola hermana");

        Assert.Equal("Hola hermana", (await ReadAsync(path, "es-MX")).Lines[0].Translation);
        Assert.Equal("Salut incrustada", (await ReadAsync(path, "fr-CA")).Lines[0].Translation);
        Assert.Equal("Hallo hermana", (await ReadAsync(path, "ja-JP")).Lines[0].Translation); // «de» es la primera alfabética
    }

    // Un hermano vacío o mal formado no da letra: se usa la incrustada del mismo código.
    [Theory, MemberData(nameof(Formats))]
    public async Task ASiblingWithoutLyrics_FallsBackToTheEmbeddedOne(string format)
    {
        var path = NewFile(format);
        Embed(path, Tr("es", "[00:01.00]Hola incrustada"), Rom("[00:01.00]Rom incrustada"));
        Sibling("song.es.lrc", "");
        Sibling("song.rom.lrc", "   \n");

        var block = await ReadAsync(path);

        Assert.Equal("Hola incrustada", block.Lines[0].Translation);
        Assert.Equal("Rom incrustada", block.Lines[0].Romanization);
    }

    // Texto sin marcas con un original sincronizado: LyricsMerger lo descartaría, así que tampoco gana a la incrustada.
    [Theory, MemberData(nameof(Formats))]
    public async Task ASiblingOfAnotherKind_FallsBackToTheEmbeddedOne(string format)
    {
        var path = NewFile(format);
        Embed(path, Tr("es", "[00:01.00]Hola incrustada"));
        Sibling("song.es.lrc", "texto sin marcas de tiempo");

        Assert.Equal("Hola incrustada", (await ReadAsync(path)).Lines[0].Translation);
    }

    // Coherencia con la principal: un «song.lrc» vacío o solo con metadatos cae a la letra incrustada (CompositeLyricsProvider).
    [Theory]
    [InlineData("")]
    [InlineData("[ar:Artista]\n[ti:Título]")]
    public async Task AMainLrcWithoutLyrics_FallsBackToTheEmbeddedMain(string content)
    {
        var path = NewFile("mp3");
        Embed(path, Tr("es", "[00:01.00]Hola"));
        Sibling("song.lrc", content);

        var block = await ReadAsync(path);

        Assert.Equal("Hello", block.Lines[0].Text);
        Assert.Equal("Hola", block.Lines[0].Translation);
    }

    // La principal del hermano «song.lrc» y las capas incrustadas se combinan igual.
    [Theory, MemberData(nameof(Formats))]
    public async Task MainFromTheLrcFile_LayersFromTheAudio(string format)
    {
        var path = NewFile(format);
        Embed(path, Tr("es", "[00:01.00]Hola"));
        Sibling("song.lrc", "[00:01.00]Hello from lrc");

        var block = await ReadAsync(path);

        Assert.Equal("Hello from lrc", block.Lines[0].Text);
        Assert.Equal("Hola", block.Lines[0].Translation);
    }

    [Theory, MemberData(nameof(Formats))]
    public async Task NoLayersAnywhere_ReturnsTheOriginalAsIs(string format)
    {
        var path = NewFile(format);
        Embed(path);

        var block = await ReadAsync(path);

        Assert.Equal(["Hello", "World"], block.Lines.Select(l => l.Text));
        Assert.All(block.Lines, l => { Assert.Null(l.Translation); Assert.Null(l.Romanization); });
    }

    [Fact]
    public void Reader_UnsupportedFormatOrMissingOrDamagedFile_ReturnsEmpty()
    {
        var reader = new TagLibLyricsLayerReader();
        var damaged = Path.Combine(_dir, "roto.flac");
        File.WriteAllBytes(damaged, [1, 2, 3, 4, 5]);
        var wav = Path.Combine(_dir, "song.wav");
        File.WriteAllBytes(wav, [1, 2, 3]);

        Assert.Empty(reader.Read(wav));
        Assert.Empty(reader.Read(Path.Combine(_dir, "no-existe.mp3")));
        Assert.Empty(reader.Read(damaged));
    }

    [Fact]
    public async Task ADamagedFile_KeepsTheSiblingLayers()
    {
        var path = Path.Combine(_dir, "song.mp3");
        File.WriteAllBytes(path, [1, 2, 3, 4, 5]);
        Sibling("song.lrc", Main);
        Sibling("song.es.lrc", "[00:01.00]Hola");

        var block = await ReadAsync(path);

        Assert.Equal("Hola", block.Lines[0].Translation);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);
}
