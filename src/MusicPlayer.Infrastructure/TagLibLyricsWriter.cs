using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;

namespace MusicPlayer.Infrastructure;

public sealed class TagLibLyricsWriter : ILyricsWriter
{
    public void Write(string filePath, LyricsLayers layers)
    {
        // Las capas llegan en M2 (docs/specs/lyrics-layers.md); hasta entonces, mejor fallar que perderlas sin avisar.
        if (layers.Extras.Count > 0)
            throw new NotSupportedException("Incrustar traducción y pronunciación aún no está disponible.");

        SafeFileEdit.Run(filePath, temp =>
        {
            Apply(temp, layers.Main);
            Verify(temp, layers.Main);
        });
    }

    private static void Apply(string path, string lyrics)
    {
        using var file = TagLib.File.Create(path);

        file.Tag.Lyrics = null;   // quita letras anteriores, sea cual sea su idioma o descripción
        file.Tag.Lyrics = lyrics;

        file.Save();
    }

    // Reabre la copia y comprueba que lo escrito es lo pedido. Trim: TagLib puede recortar los bordes.
    private static void Verify(string path, string lyrics)
    {
        using var file = TagLib.File.Create(path);
        if ((file.Tag.Lyrics ?? "").Trim() != lyrics.Trim())
            throw new IOException("La verificación tras escribir falló. El archivo original no se modificó.");
    }
}