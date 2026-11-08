using MusicPlayer.Core.Interfaces;
using MusicPlayer.Core.Models;
using MusicPlayer.Core.Services;

namespace MusicPlayer.Infrastructure;

public sealed class TagLibLyricsWriter : ILyricsWriter
{
    public bool SupportsLayers(string filePath) => TagLibLyricsLayerFields.Supports(filePath);

    public void Write(string filePath, LyricsLayers layers)
    {
        if (layers.Extras.Count > 0 && !SupportsLayers(filePath))
            throw new NotSupportedException("Este formato no guarda traducción ni pronunciación incrustadas.");

        // Una sola escritura: la letra principal y todas las capas van en la misma copia temporal.
        SafeFileEdit.Run(filePath, temp =>
        {
            Apply(temp, layers.Main, layers.Extras);
            Verify(temp, layers.Main, layers.Extras);
        });
    }

    public void WriteLayers(string filePath, IReadOnlyList<LyricsLayer> layers)
    {
        if (!SupportsLayers(filePath))
            throw new NotSupportedException("Este formato no guarda traducción ni pronunciación incrustadas.");

        // Solo capas: sin letra principal, así que no se toca el campo estándar (USLT, LYRICS, ©lyr).
        SafeFileEdit.Run(filePath, temp =>
        {
            Apply(temp, null, layers);
            Verify(temp, null, layers);
        });
    }

    private static void Apply(string path, string? main, IReadOnlyList<LyricsLayer> extras)
    {
        using var file = TagLib.File.Create(path);

        if (main is not null)
        {
            file.Tag.Lyrics = null;   // quita letras anteriores, sea cual sea su idioma o descripción (las capas no)
            file.Tag.Lyrics = main;
        }

        // Solo las capas con fuente: las demás que ya hubiera en el archivo se conservan (criterio 4).
        foreach (var layer in extras)
            TagLibLyricsLayerFields.Set(file, LyricsLayerNames.FieldName(layer), layer.Text);

        file.Save();
    }

    // Reabre la copia y comprueba que lo escrito es lo pedido, capa por capa. Trim: TagLib puede recortar los bordes.
    private static void Verify(string path, string? main, IReadOnlyList<LyricsLayer> extras)
    {
        using var file = TagLib.File.Create(path);
        var ok = (main is null || (file.Tag.Lyrics ?? "").Trim() == main.Trim()) &&
                 extras.All(l =>
                     (TagLibLyricsLayerFields.Get(file, LyricsLayerNames.FieldName(l)) ?? "").Trim() == l.Text.Trim());
        if (!ok)
            throw new IOException("La verificación tras escribir falló. El archivo original no se modificó.");
    }
}
