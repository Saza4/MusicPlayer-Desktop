using MusicPlayer.Core.Interfaces;

namespace MusicPlayer.Infrastructure;

public sealed class TagLibLyricsWriter : ILyricsWriter
{
    public void Write(string filePath, string lyrics) =>
        SafeFileEdit.Run(filePath, temp =>
        {
            Apply(temp, lyrics);
            Verify(temp, lyrics);
        });

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