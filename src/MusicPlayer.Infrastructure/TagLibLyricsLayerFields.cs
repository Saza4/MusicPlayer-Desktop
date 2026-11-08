using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;
using TagLib;
using TagLib.Id3v2;
using TagLib.Mpeg4;
using TagLib.Ogg;

namespace MusicPlayer.Infrastructure;

/// <summary>
/// Campos de las capas de letra en cada formato (docs/specs/lyrics-layers.md, criterio 2 y «Resultado del spike»):
/// TXXX en MP3, comentario Vorbis en FLAC y átomo libre «----:com.apple.iTunes:…» en M4A. Los nombres se escriben en
/// mayúsculas y se buscan sin distinguir mayúsculas.
/// </summary>
public static class TagLibLyricsLayerFields
{
    private const string ITunes = "com.apple.iTunes";

    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase) { ".mp3", ".flac", ".m4a" };

    /// <summary>True si el formato guarda capas. Los demás (WAV…) solo llevan la letra principal.</summary>
    public static bool Supports(string filePath) => Extensions.Contains(Path.GetExtension(filePath));

    /// <summary>Escribe la capa en su campo y quita cualquier otro con el mismo nombre en otras mayúsculas.</summary>
    public static void Set(TagLib.File file, string name, string text)
    {
        switch (file)
        {
            case TagLib.Mpeg.AudioFile:
                var id3 = (TagLib.Id3v2.Tag)file.GetTag(TagTypes.Id3v2, true);
                foreach (var old in Txxx(id3).Where(f => Same(f.Description, name)).ToList())
                    id3.RemoveFrame(old);
                UserTextInformationFrame.Get(id3, name, StringType.UTF16, true).Text = [text];
                break;

            case TagLib.Flac.File:
                var xiph = (XiphComment)file.GetTag(TagTypes.Xiph, true);
                xiph.RemoveField(name);         // Vorbis no distingue mayúsculas
                xiph.SetField(name, text);
                break;

            case TagLib.Mpeg4.File:
                var apple = (AppleTag)file.GetTag(TagTypes.Apple, true);
                foreach (var old in FreeformNames(apple).Where(n => Same(n, name)).ToList())
                    apple.SetDashBox(ITunes, old, null);
                apple.SetDashBox(ITunes, name, text);
                break;

            default:
                throw new NotSupportedException("Este formato no guarda traducción ni pronunciación incrustadas.");
        }
    }

    /// <summary>Texto de un campo de capa, sin distinguir mayúsculas. Null si no está.</summary>
    public static string? Get(TagLib.File file, string name) =>
        Fields(file).Where(f => Same(f.Name, name)).Select(f => f.Text).FirstOrDefault();

    /// <summary>
    /// Todas las capas incrustadas, con el código normalizado («pt-BR»). Un código repetido en otras mayúsculas se toma
    /// una vez. Los campos que no son capas, o con un código que no vale, se ignoran.
    /// </summary>
    public static IReadOnlyList<LyricsLayer> Read(TagLib.File file)
    {
        var layers = new Dictionary<string, LyricsLayer>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, text) in Fields(file))
        {
            if (string.IsNullOrWhiteSpace(text) || !LyricsLayerNames.TryParseField(name, out var kind, out var language))
                continue;
            layers.TryAdd(language ?? "", new LyricsLayer(kind, language, text));
        }
        return layers.Values.ToList();
    }

    private static IEnumerable<(string Name, string Text)> Fields(TagLib.File file)
    {
        switch (file)
        {
            case TagLib.Mpeg.AudioFile when file.GetTag(TagTypes.Id3v2, false) is TagLib.Id3v2.Tag id3:
                return Txxx(id3).Where(f => f.Description is not null)
                                .Select(f => (f.Description, string.Join("\0", f.Text)));

            case TagLib.Flac.File when file.GetTag(TagTypes.Xiph, false) is XiphComment xiph:
                return xiph.Select(n => (n, string.Join("\0", xiph.GetField(n))));

            case TagLib.Mpeg4.File when file.GetTag(TagTypes.Apple, false) is AppleTag apple:
                return FreeformNames(apple).Select(n => (n, apple.GetDashBox(ITunes, n) ?? ""));

            default:
                return [];
        }
    }

    private static IEnumerable<UserTextInformationFrame> Txxx(TagLib.Id3v2.Tag tag) =>
        tag.GetFrames<UserTextInformationFrame>();

    // Nombres de los átomos «----» de iTunes (GetDashBox no permite enumerarlos).
    private static IEnumerable<string> FreeformNames(AppleTag apple) =>
        apple.Where(b => b.BoxType == "----")
             .Select(b => (Mean: Child(b, "mean"), Name: Child(b, "name")))
             .Where(x => x.Mean == ITunes && x.Name is not null)
             .Select(x => x.Name!);

    private static string? Child(Box box, string type) =>
        box.Children.OfType<AppleAdditionalInfoBox>().FirstOrDefault(c => c.BoxType == type)?.Text;

    private static bool Same(string? a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
