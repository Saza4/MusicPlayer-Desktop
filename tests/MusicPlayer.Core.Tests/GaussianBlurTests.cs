using MusicPlayer.Core.Services;

namespace MusicPlayer.Core.Tests;

public class GaussianBlurTests
{
    private static byte[] Solid(int w, int h, byte value)
    {
        var p = new byte[w * h * 4];
        Array.Fill(p, value);
        return p;
    }

    private static int At(int w, int x, int y, int channel = 3) => (y * w + x) * 4 + channel;

    [Fact]
    public void RadiusToSigma_MatchesSkia()
    {
        Assert.Equal(0.57735 * 60 + 0.5, GaussianBlur.RadiusToSigma(60), 9);
        Assert.Equal(0, GaussianBlur.RadiusToSigma(0));
    }

    [Fact]
    public void Kernel_IsNormalizedAndSymmetric()
    {
        var kernel = GaussianBlur.Kernel(2.5);

        Assert.Equal(1, kernel.Sum(), 4);
        for (int i = 0; i < kernel.Length / 2; i++)
            Assert.Equal(kernel[i], kernel[^(i + 1)], 6);
        Assert.Equal(kernel.Max(), kernel[kernel.Length / 2]);
    }

    [Fact]
    public void ZeroSigma_ChangesNothing()
    {
        var pixels = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

        GaussianBlur.Apply(pixels, 2, 1, 0);

        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, pixels);
    }

    [Fact]
    public void SolidImage_StaysSolidInside_AndFadesAtTheEdges()
    {
        const int w = 40, h = 30;
        var pixels = Solid(w, h, 200);

        GaussianBlur.Apply(pixels, w, h, 2);

        Assert.Equal(200, pixels[At(w, 20, 15)]);                  // lejos del borde: igual
        Assert.InRange(pixels[At(w, 0, 15)], 90, 130);             // en el borde: ~la mitad (fuera es transparente)
        Assert.True(pixels[At(w, 0, 0)] < pixels[At(w, 0, 15)]);   // en la esquina, aún menos
    }

    [Fact]
    public void SinglePixel_SpreadsSymmetrically_AndKeepsItsEnergy()
    {
        const int w = 21, h = 21;
        var pixels = new byte[w * h * 4];
        for (int c = 0; c < 4; c++) pixels[At(w, 10, 10, c)] = 255;

        GaussianBlur.Apply(pixels, w, h, 1.5);

        Assert.Equal(pixels[At(w, 9, 10)], pixels[At(w, 11, 10)]);
        Assert.Equal(pixels[At(w, 10, 9)], pixels[At(w, 10, 11)]);
        Assert.Equal(pixels[At(w, 9, 9)], pixels[At(w, 11, 11)]);
        Assert.True(pixels[At(w, 10, 10)] > pixels[At(w, 11, 10)]);
        var total = 0;
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) total += pixels[At(w, x, y)];
        Assert.InRange(total, 255 - 25, 255 + 25); // redondeo a 8 bits
    }

    [Fact]
    public void Channels_AreBlurredIndependently()
    {
        const int w = 9, h = 1;
        var pixels = new byte[w * h * 4];
        pixels[At(w, 4, 0, 0)] = 255; // solo el primer canal

        GaussianBlur.Apply(pixels, w, h, 1);

        Assert.True(pixels[At(w, 3, 0, 0)] > 0);
        for (int x = 0; x < w; x++)
            for (int c = 1; c < 4; c++)
                Assert.Equal(0, pixels[At(w, x, 0, c)]);
    }

    [Fact]
    public void InvalidArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => GaussianBlur.Apply(null!, 1, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => GaussianBlur.Apply(new byte[4], 0, 1, 1));
        Assert.Throws<ArgumentException>(() => GaussianBlur.Apply(new byte[4], 2, 1, 1));
    }
}
