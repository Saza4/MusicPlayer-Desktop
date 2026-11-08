using MusicPlayer.Core.Models;

namespace MusicPlayer.Core.Tests;

public class LyricsEmbedReportTests
{
    private static readonly LyricsLayerSource Es = new(LyricsLayerKind.Translation, "es", "song.es.lrc");
    private static readonly LyricsLayerSource En = new(LyricsLayerKind.Translation, "en", "song.en.lrc");
    private static readonly LyricsLayerSource Rom = new(LyricsLayerKind.Pronunciation, null, "song.rom.lrc");

    [Fact]
    public void WithoutLayers_SaysWhatItSaidBefore() =>
        Assert.Equal("Letra de \"X\" incrustada desde song.lrc",
            new LyricsEmbedReport([], []).Describe("X", "song.lrc"));

    [Fact]
    public void ListsWhatWasEmbedded() =>
        Assert.Equal("Letra de \"X\" incrustada desde song.lrc: original, traducción (es) y pronunciación",
            new LyricsEmbedReport([Es, Rom], []).Describe("X", "song.lrc"));

    // Los hermanos llegan con la pronunciación delante (LyricsLayerNames.FindSiblings); el texto sigue el criterio 7.
    [Fact]
    public void TheOrderDoesNotDependOnHowTheyArrive() =>
        Assert.Equal("Letra de \"X\" incrustada desde song.lrc: original, traducción (en), traducción (es) y pronunciación" +
                     " · saltadas: traducción (en), está vacía; pronunciación, pesa más de 1 MB",
            new LyricsEmbedReport([Rom, Es, En], [new LyricsLayerSkip(Rom, "pesa más de 1 MB"), new LyricsLayerSkip(En, "está vacía")])
                .Describe("X", "song.lrc"));

    [Fact]
    public void ListsWhatWasSkippedAndWhy() =>
        Assert.Equal("Letra de \"X\" incrustada desde song.lrc: original y traducción (es) · saltada: traducción (en), no está en UTF-8",
            new LyricsEmbedReport([Es], [new LyricsLayerSkip(En, "no está en UTF-8")]).Describe("X", "song.lrc"));

    [Fact]
    public void SeveralSkipped() =>
        Assert.Equal("Letra de \"X\" incrustada desde song.lrc · saltadas: traducción (en), está vacía; pronunciación, pesa más de 1 MB",
            new LyricsEmbedReport([], [new LyricsLayerSkip(En, "está vacía"), new LyricsLayerSkip(Rom, "pesa más de 1 MB")])
                .Describe("X", "song.lrc"));
}
