namespace MusicPlayer.Core.Models;

public sealed record TagSuggestOptions(bool AlbumFromFolder = false, bool DetectCompilations = true);

/// <summary>Lo que se propone para una pista: las etiquetas actuales y las nuevas.</summary>
public sealed record TagSuggestion(Track Track, TrackTags Before, TrackTags After)
{
    public bool HasChanges => Before != After;

    /// <summary>Una línea por campo que cambia: "Título: «a» → «b»".</summary>
    public IReadOnlyList<string> Describe()
    {
        var lines = new List<string>();

        void Text(string label, string a, string b)
        {
            if (a != b) lines.Add($"{label}: {Show(a)} → {Show(b)}");
        }

        void Number(string label, uint a, uint b) =>
            Text(label, a == 0 ? "" : a.ToString(), b == 0 ? "" : b.ToString());

        Text("Título", Before.Title, After.Title);
        Text("Artista", Before.Artist, After.Artist);
        Text("Artista del álbum", Before.AlbumArtist, After.AlbumArtist);
        Text("Álbum", Before.Album, After.Album);
        Number("Año", Before.Year, After.Year);
        Number("Pista", Before.TrackNumber, After.TrackNumber);
        Number("Disco", Before.DiscNumber, After.DiscNumber);
        return lines;
    }

    private static string Show(string text) => text.Length == 0 ? "(vacío)" : $"«{text}»";
}