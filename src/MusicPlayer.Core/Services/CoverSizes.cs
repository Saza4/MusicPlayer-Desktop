namespace MusicPlayer.Core.Services;

/// <summary>
/// Anchos a los que se decodifican las carátulas, en píxeles, y el tamaño en pantalla de la tarjeta de álbum, en
/// unidades de interfaz (docs/specs/redesign-mockups.md). Una imagen se ve nítida si se decodifica al menos al tamaño
/// en pantalla por la escala del monitor.
/// </summary>
public static class CoverSizes
{
    /// <summary>Miniatura de la lista de canciones y de «Álbumes» (40-56 en pantalla).</summary>
    public const int ThumbnailWidth = 96;

    /// <summary>Lado de la tarjeta de álbum de la página de artista, en unidades de interfaz.</summary>
    public const double CardDisplaySize = 156;

    /// <summary>Tarjeta de álbum: cubre la tarjeta hasta una escala del 300 % (156 × 3 = 468).</summary>
    public const int CardDecodeWidth = 512;

    /// <summary>Barra de reproducción y modo inmersivo (360 en pantalla).</summary>
    public const int LargeWidth = 640;
}
