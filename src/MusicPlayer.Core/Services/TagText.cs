using System.Text.RegularExpressions;

namespace MusicPlayer.Core.Services;

public sealed record ParsedFileName(uint? Number, string? Artist, string Title, string Rest);

public static partial class FileNameParser
{
    // Número de pista solo con separador explícito, y lo que sigue no empieza por dígito ("10.000 Reasons").
    [GeneratedRegex(@"^(?<n>\d{1,3})\s*[-.)]\s*(?<rest>\D.*)$")]
    private static partial Regex Leading();

    public static ParsedFileName Parse(string fileName)
    {
        var name = fileName.Trim();
        if (!name.Contains(' ')) name = name.Replace('_', ' '); // "Artista_-_Titulo" -> con espacios
        name = TitleCleaner.Tidy(name);

        uint? number = null;
        if (Leading().Match(name) is { Success: true } m)
        {
            number = uint.Parse(m.Groups["n"].Value);
            name = m.Groups["rest"].Value.Trim();
        }

        var parts = name.Split(" - ", 2, StringSplitOptions.TrimEntries);
        return parts.Length == 2 && parts[0].Length > 0 && parts[1].Length > 0
            ? new ParsedFileName(number, parts[0], parts[1], name)
            : new ParsedFileName(number, null, name, name);
    }
}

public static partial class TitleCleaner
{
    [GeneratedRegex(@"\s*[\(\[]\s*(official\s+)?(music\s+|lyric\s+)?(video|audio|lyrics?|visuali[sz]er)\s*[\)\]]",
        RegexOptions.IgnoreCase)]
    private static partial Regex Bracketed();

    [GeneratedRegex(@"\s*[\(\[]\s*(hd|hq|4k)\s*[\)\]]", RegexOptions.IgnoreCase)]
    private static partial Regex Quality();

    // Marcas de agua de descargadores: solo al final del título. Amplía la lista a tu gusto.
    [GeneratedRegex(@"[\s_-]*\b(klickaud|y2mate(\.com)?|ytmp3(\.\w+)?|savefrom(\.net)?)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex Watermark();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    /// <summary>Recorta y colapsa espacios.</summary>
    public static string Tidy(string text) => Spaces().Replace(text.Trim(), " ");

    /// <summary>Quita basura típica de descargas. Si no quedara nada, devuelve el texto original.</summary>
    public static string Clean(string title)
    {
        var text = Bracketed().Replace(title, "");
        text = Quality().Replace(text, "");
        text = Watermark().Replace(text, "");
        text = Tidy(text).TrimEnd('-', ' ');
        return text.Length > 0 ? text : Tidy(title);
    }
}