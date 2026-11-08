namespace MusicPlayer.Core.Services;

/// <summary>
/// Desenfoque gaussiano de una imagen pequeña de 4 canales de 8 bits (premultiplicada: BGRA o RGBA, da igual,
/// los canales se tratan igual). Fuera de la imagen se considera transparente, como el efecto de Avalonia, así
/// que los bordes se funden con lo que haya detrás.
/// </summary>
/// <remarks>
/// Sirve para desenfocar la carátula del modo inmersivo una sola vez, a tamaño reducido, en lugar de con un
/// <c>Effect="blur(60)"</c> que se recalcula bajo cada fotograma de la letra progresiva (spec, M3).
/// </remarks>
public static class GaussianBlur
{
    /// <summary>La conversión de radio a sigma que usa Skia (y con ella el BlurEffect de Avalonia).</summary>
    public static double RadiusToSigma(double radius) => radius <= 0 ? 0 : 0.57735 * radius + 0.5;

    /// <summary>Desenfoca <paramref name="pixels"/> en el sitio. Con sigma ≤ 0 no hace nada.</summary>
    public static void Apply(byte[] pixels, int width, int height, double sigma)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (pixels.Length < width * height * 4) throw new ArgumentException("Faltan píxeles.", nameof(pixels));
        if (sigma <= 0) return;

        var kernel = Kernel(sigma);
        var radius = kernel.Length / 2;
        var source = new float[width * height * 4];
        for (int i = 0; i < source.Length; i++) source[i] = pixels[i];
        var temp = new float[source.Length];

        // Horizontal y luego vertical: el gaussiano es separable.
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                Accumulate(source, temp, kernel, radius, (y * width + x) * 4, x, width, step: 4);

        Array.Clear(source);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                Accumulate(temp, source, kernel, radius, (y * width + x) * 4, y, height, step: width * 4);

        for (int i = 0; i < source.Length; i++)
            pixels[i] = (byte)Math.Clamp((int)MathF.Round(source[i]), 0, 255);
    }

    private static void Accumulate(float[] from, float[] to, float[] kernel, int radius, int center, int position,
        int length, int step)
    {
        float b = 0, g = 0, r = 0, a = 0;
        for (int k = -radius; k <= radius; k++)
        {
            var p = position + k;
            if (p < 0 || p >= length) continue; // fuera: transparente
            var w = kernel[k + radius];
            var i = center + k * step;
            b += from[i] * w;
            g += from[i + 1] * w;
            r += from[i + 2] * w;
            a += from[i + 3] * w;
        }
        to[center] = b;
        to[center + 1] = g;
        to[center + 2] = r;
        to[center + 3] = a;
    }

    /// <summary>Núcleo normalizado (suma 1) hasta 3 sigmas.</summary>
    public static float[] Kernel(double sigma)
    {
        var radius = Math.Max(1, (int)Math.Ceiling(3 * sigma));
        var kernel = new float[2 * radius + 1];
        double sum = 0;
        for (int i = -radius; i <= radius; i++)
        {
            var v = Math.Exp(-(i * i) / (2 * sigma * sigma));
            kernel[i + radius] = (float)v;
            sum += v;
        }
        for (int i = 0; i < kernel.Length; i++) kernel[i] = (float)(kernel[i] / sum);
        return kernel;
    }
}
