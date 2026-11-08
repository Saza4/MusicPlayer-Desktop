using TagLib;
using TagLib.Id3v2;
using TagLib.Mpeg4;
using TagLib.Ogg;
using File = System.IO.File;
using Tag = TagLib.Id3v2.Tag;

namespace MusicPlayer.Infrastructure.Tests;

/// <summary>
/// Spike de M0 (docs/specs/lyrics-layers.md): fija lo que TagLibSharp 2.3.0 permite para guardar la traducción y la
/// pronunciación en campos propios, sin pasar aún por el código de la app (eso es M2).
/// </summary>
public class TagLibLyricsLayersSpikeTests : IDisposable
{
    private const string Main = "[00:01.00]Principal inventada";
    private const string Field = "LYRICS_TRANSLATION_ES";
    private const string ITunes = "com.apple.iTunes";

    private readonly string _dir = Directory.CreateTempSubdirectory("layers-spike").FullName;

    // Multilínea, con los separadores que ID3v2.3 podría partir ("/") y otras escrituras.
    private static readonly string Layer = string.Join("\r\n", Enumerable.Range(0, 5).Select(i =>
        $"[00:{i:00}.50]Línea {i} inventada / con barra; y más — 夜に駆ける ñ")) + "\r\n";

    // Cerca del límite de 1 MB por capa (UTF-8).
    private static readonly string Big = string.Join("\r\n", Enumerable.Range(0, 9000).Select(i =>
        $"[{i / 60:00}:{i % 60:00}.00]Línea {i} inventada, bastante larga — ñ"));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Mp3_Txxx_RoundTripsMultilineTextAndKeepsTheMainLyrics(bool big)
    {
        var path = TestAudio.CreateMp3(_dir);
        var text = big ? Big : Layer;

        using (var file = TagLib.File.Create(path))
        {
            file.Tag.Lyrics = Main;
            var tag = (Tag)file.GetTag(TagTypes.Id3v2, true);
            UserTextInformationFrame.Get(tag, Field, StringType.UTF16, true).Text = [text];
            file.Save();
        }

        using var reopened = TagLib.File.Create(path);
        var frame = Assert.Single(Txxx(reopened));
        Assert.Equal(text, Assert.Single(frame.Text)); // una sola parte: no se parte por "/"
        Assert.Equal(Main, reopened.Tag.Lyrics);
    }

    [Fact]
    public void Mp3_Txxx_GetIsCaseSensitive_SoReadingMustEnumerate()
    {
        var path = TestAudio.CreateMp3(_dir);
        using (var file = TagLib.File.Create(path))
        {
            UserTextInformationFrame.Get((Tag)file.GetTag(TagTypes.Id3v2, true), Field, StringType.UTF16, true).Text = [Layer];
            file.Save();
        }

        using var reopened = TagLib.File.Create(path);
        var tag = (Tag)reopened.GetTag(TagTypes.Id3v2, false);
        Assert.Null(UserTextInformationFrame.Get(tag, Field.ToLowerInvariant(), false));
        Assert.Single(Txxx(reopened), f => string.Equals(f.Description, Field.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Mp3_ClearingTheLyricsKeepsTheTxxx_AndRewritingDoesNotDuplicate()
    {
        var path = TestAudio.CreateMp3(_dir);
        for (int pass = 0; pass < 2; pass++)
        {
            using var file = TagLib.File.Create(path);
            file.Tag.Lyrics = null;
            file.Tag.Lyrics = Main;
            UserTextInformationFrame.Get((Tag)file.GetTag(TagTypes.Id3v2, true), Field, StringType.UTF16, true).Text = [$"[00:01.00]Pasada {pass}"];
            file.Save();
        }

        using var reopened = TagLib.File.Create(path);
        Assert.Equal("[00:01.00]Pasada 1", Assert.Single(Assert.Single(Txxx(reopened)).Text));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Flac_VorbisField_RoundTrips_IsCaseInsensitive_AndDoesNotDuplicate(bool big)
    {
        var path = TestAudio.CreateFlac(_dir);
        var text = big ? Big : Layer;

        using (var file = TagLib.File.Create(path))
        {
            file.Tag.Lyrics = Main;
            ((XiphComment)file.GetTag(TagTypes.Xiph, true)).SetField(Field, text);
            file.Save();
        }
        using (var file = TagLib.File.Create(path))
        {
            ((XiphComment)file.GetTag(TagTypes.Xiph, true)).SetField(Field.ToLowerInvariant(), text);
            file.Save();
        }

        using var reopened = TagLib.File.Create(path);
        var xiph = (XiphComment)reopened.GetTag(TagTypes.Xiph, false);
        Assert.Equal(text, Assert.Single(xiph.GetField(Field.ToLowerInvariant())));
        Assert.Contains(Field, xiph); // los nombres se enumeran (y se guardan en mayúsculas)
        Assert.Equal(Main, reopened.Tag.Lyrics);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void M4a_FreeformAtom_RoundTrips_IsCaseInsensitive_AndDoesNotDuplicate(bool big)
    {
        var path = TestAudio.CopyM4a(_dir);
        var text = big ? Big : Layer;

        for (int pass = 0; pass < 2; pass++)
        {
            using var file = TagLib.File.Create(path);
            file.Tag.Lyrics = Main;
            ((AppleTag)file.GetTag(TagTypes.Apple, true)).SetDashBox(ITunes, Field, text);
            file.Save();
        }

        using var reopened = TagLib.File.Create(path);
        var apple = (AppleTag)reopened.GetTag(TagTypes.Apple, false);
        Assert.Equal(text, apple.GetDashBox(ITunes, Field.ToLowerInvariant()));
        Assert.Equal([Field], FreeformNames(apple)); // los nombres se enumeran con las cajas "----"
        Assert.Equal(Main, reopened.Tag.Lyrics);
        Assert.True(reopened.Properties.Duration > TimeSpan.Zero); // el audio sigue legible
    }

    private static List<UserTextInformationFrame> Txxx(TagLib.File file) =>
        ((Tag)file.GetTag(TagTypes.Id3v2, false)).GetFrames<UserTextInformationFrame>().ToList();

    private static List<string?> FreeformNames(AppleTag apple) =>
        apple.Where(b => b.BoxType == "----")
             .Select(b => b.Children.OfType<AppleAdditionalInfoBox>().FirstOrDefault(c => c.BoxType == "name")?.Text)
             .ToList();

    public void Dispose() => Directory.Delete(_dir, recursive: true);
}
