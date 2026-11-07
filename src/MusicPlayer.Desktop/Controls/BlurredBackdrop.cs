using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using MusicPlayer.Core.Services;

namespace MusicPlayer.Desktop.Controls;

/// <summary>
/// Una imagen a pantalla completa (como <c>Stretch="UniformToFill"</c>) desenfocada una sola vez: se dibuja a
/// 1/<see cref="Downscale"/> del tamaño, se desenfoca en Core y se muestra ampliada. Sustituye a un
/// <c>Image</c> con <c>Effect="blur(…)"</c>, que se recalcula bajo cada fotograma que se repinta encima
/// (la letra progresiva, spec M3). Se regenera solo al cambiar la imagen o el tamaño.
/// </summary>
public sealed class BlurredBackdrop : Control
{
    public static readonly StyledProperty<Bitmap?> SourceProperty =
        AvaloniaProperty.Register<BlurredBackdrop, Bitmap?>(nameof(Source));

    /// <summary>Radio del desenfoque en DIPs de pantalla, el mismo que tendría <c>blur(radio)</c>.</summary>
    public static readonly StyledProperty<double> BlurRadiusProperty =
        AvaloniaProperty.Register<BlurredBackdrop, double>(nameof(BlurRadius), 60);

    /// <summary>Cuánto más pequeña se desenfoca. Con radio 60 el detalle que se pierde no se ve.</summary>
    public const int Downscale = 12;

    private Bitmap? _blurred;
    private Size _blurredFor;
    private Bitmap? _blurredFrom;

    static BlurredBackdrop()
    {
        AffectsRender<BlurredBackdrop>(SourceProperty, BlurRadiusProperty);
    }

    public Bitmap? Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }
    public double BlurRadius { get => GetValue(BlurRadiusProperty); set => SetValue(BlurRadiusProperty, value); }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        if (Source is null || size.Width < 1 || size.Height < 1) return;

        if (_blurred is null || !ReferenceEquals(_blurredFrom, Source) || _blurredFor != size)
            Regenerate(Source, size);

        if (_blurred is not null)
            context.DrawImage(_blurred, new Rect(_blurred.Size), new Rect(size));
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _blurred?.Dispose();
        _blurred = null;
        _blurredFrom = null;
    }

    private void Regenerate(Bitmap source, Size size)
    {
        _blurred?.Dispose();
        _blurred = null;
        _blurredFrom = source;
        _blurredFor = size;

        var pixelSize = new PixelSize(
            Math.Max(1, (int)Math.Ceiling(size.Width / Downscale)),
            Math.Max(1, (int)Math.Ceiling(size.Height / Downscale)));

        // 1. La imagen pequeña, recortada como UniformToFill.
        using var small = new RenderTargetBitmap(pixelSize, new Vector(96, 96));
        using (var ctx = small.CreateDrawingContext())
        {
            var src = source.Size;
            var scale = Math.Max(pixelSize.Width / src.Width, pixelSize.Height / src.Height);
            var visible = new Size(pixelSize.Width / scale, pixelSize.Height / scale);
            var sourceRect = new Rect(
                new Point((src.Width - visible.Width) / 2, (src.Height - visible.Height) / 2), visible);
            using (ctx.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.HighQuality }))
                ctx.DrawImage(source, sourceRect, new Rect(0, 0, pixelSize.Width, pixelSize.Height));
        }

        // 2. Desenfoque en Core, con el radio pasado a la escala reducida.
        var stride = pixelSize.Width * 4;
        var pixels = new byte[stride * pixelSize.Height];
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            var address = handle.AddrOfPinnedObject();
            small.CopyPixels(new PixelRect(pixelSize), address, pixels.Length, stride);
            GaussianBlur.Apply(pixels, pixelSize.Width, pixelSize.Height,
                GaussianBlur.RadiusToSigma(BlurRadius) / Downscale);

            // 3. Mapa de bits con el resultado (copia los píxeles: el búfer se puede soltar después).
            _blurred = new Bitmap(small.Format ?? PixelFormat.Bgra8888, small.AlphaFormat ?? AlphaFormat.Premul,
                address, pixelSize, new Vector(96, 96), stride);
        }
        finally
        {
            handle.Free();
        }
    }
}
