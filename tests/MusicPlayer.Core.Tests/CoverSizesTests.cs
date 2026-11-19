using MusicPlayer.Core.Services;

namespace MusicPlayer.Core.Tests;

// docs/specs/redesign-mockups.md, tercera pasada: las tarjetas de la página de artista se veían pixeladas porque usaban
// la miniatura de la lista (96 px) a 156 de tamaño en pantalla.
public class CoverSizesTests
{
    [Fact]
    public void Card_IsDecodedForAt300PercentScaling()
    {
        Assert.True(CoverSizes.CardDecodeWidth >= 3 * CoverSizes.CardDisplaySize);
        Assert.True(CoverSizes.CardDecodeWidth >= 400);
    }

    [Fact]
    public void Card_IsNeverBiggerThanTheImmersiveCover()
    {
        Assert.True(CoverSizes.CardDecodeWidth <= CoverSizes.LargeWidth);
    }

    [Fact]
    public void TheSongListKeepsItsSmallThumbnail()
    {
        Assert.Equal(96, CoverSizes.ThumbnailWidth);
    }
}
