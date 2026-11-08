using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using MusicPlayer.Core.Services;

namespace MusicPlayer.Desktop.Controls;

/// <summary>
/// Una imagen a pantalla completa (como <c>Stretch="UniformToFill"</c>) desenfocada una sola vez: se reduce a
/// 1/<see cref="Downscale"/> del tamaño, se desenfoca en Core y se muestra ampliada. Sustituye a un
/// <c>Image</c> con <c>Effect="blur(…)"</c>, que se recalcula bajo cada fotograma que se repinta encima
/// (la letra progresiva, spec M3).
/// </summary>
/// <remarks>
/// Reducir, recortar y desenfocar ocurre fuera del hilo de la interfaz (spec M4). Se regenera solo cuando cambia la
/// imagen (otra pista, o la misma tras editar su carátula, que llega como un <see cref="Bitmap"/> nuevo) o el
/// tamaño reducido. Mientras tanto se sigue viendo el resultado anterior, ampliado.
/// </remarks>
public sealed class BlurredBackdrop : Control
{
    public static readonly StyledProperty<Bitmap?> SourceProperty =
        AvaloniaProperty.Register<BlurredBackdrop, Bitmap?>(nameof(Source));

    /// <summary>Radio del desenfoque en DIPs de pantalla, el mismo que tendría <c>blur(radio)</c>.</summary>
    public static readonly StyledProperty<double> BlurRadiusProperty =
        AvaloniaProperty.Register<BlurredBackdrop, double>(nameof(BlurRadius), 60);

    /// <summary>Cuánto más pequeña se desenfoca. Con radio 60 el detalle que se pierde no se ve.</summary>
    public const int Downscale = 12;

    private static readonly Vector Dpi = new(96, 96);

    private Bitmap? _blurred;
    private Bitmap? _requestedFrom;
    private PixelSize _requestedSize;
    private double _requestedRadius;
    private int _version; // solo hilo de UI: descarta resultados de peticiones ya superadas

    public Bitmap? Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }
    public double BlurRadius { get => GetValue(BlurRadiusProperty); set => SetValue(BlurRadiusProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourceProperty || change.Property == BlurRadiusProperty)
            Request();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        Request();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _version++;
        Replace(null);
        _requestedFrom = null;
    }

    public override void Render(DrawingContext context)
    {
        if (_blurred is not null && Bounds.Width >= 1 && Bounds.Height >= 1)
            context.DrawImage(_blurred, new Rect(_blurred.Size), new Rect(Bounds.Size));
    }

    private void Request()
    {
        var source = Source;
        if (source is null)
        {
            _version++;
            _requestedFrom = null;
            Replace(null); // sin carátula: sin fondo, como el Image de antes
            return;
        }

        var size = Bounds.Size;
        if (size.Width < 1 || size.Height < 1) return; // se pedirá al recibir tamaño

        var target = new PixelSize(
            Math.Max(1, (int)Math.Ceiling(size.Width / Downscale)),
            Math.Max(1, (int)Math.Ceiling(size.Height / Downscale)));
        var radius = BlurRadius;
        if (ReferenceEquals(source, _requestedFrom) && target == _requestedSize && radius == _requestedRadius)
            return; // ya está hecho o en marcha

        _requestedFrom = source;
        _requestedSize = target;
        _requestedRadius = radius;
        var version = ++_version;
        var sigma = GaussianBlur.RadiusToSigma(radius) / Downscale;

        _ = Task.Run(() => Build(source, target, sigma)).ContinueWith(task =>
        {
            var result = task.IsCompletedSuccessfully ? task.Result : null;
            Dispatcher.UIThread.Post(() =>
            {
                if (version == _version) Replace(result);
                else result?.Dispose(); // llegó tarde: ya hay otra petición
            });
        }, TaskScheduler.Default);
    }

    private void Replace(Bitmap? blurred)
    {
        var old = _blurred;
        _blurred = blurred;
        InvalidateVisual();
        old?.Dispose();
    }

    /// <summary>En segundo plano: reduce como UniformToFill, recorta el centro y desenfoca.</summary>
    private static Bitmap? Build(Bitmap source, PixelSize target, double sigma)
    {
        var src = source.PixelSize;
        if (src.Width <= 0 || src.Height <= 0) return null;

        var scale = Math.Max((double)target.Width / src.Width, (double)target.Height / src.Height);
        var scaledSize = new PixelSize(
            Math.Max(target.Width, (int)Math.Ceiling(src.Width * scale)),
            Math.Max(target.Height, (int)Math.Ceiling(src.Height * scale)));

        using var scaled = source.CreateScaledBitmap(scaledSize, BitmapInterpolationMode.HighQuality);
        var crop = new PixelRect(
            (scaledSize.Width - target.Width) / 2, (scaledSize.Height - target.Height) / 2,
            target.Width, target.Height);

        var stride = target.Width * 4;
        var pixels = new byte[stride * target.Height];
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            var address = handle.AddrOfPinnedObject();
            scaled.CopyPixels(crop, address, pixels.Length, stride);
            GaussianBlur.Apply(pixels, target.Width, target.Height, sigma);

            // Copia los píxeles: el búfer se puede soltar después.
            return new Bitmap(scaled.Format ?? PixelFormat.Bgra8888, scaled.AlphaFormat ?? AlphaFormat.Premul,
                address, target, Dpi, stride);
        }
        finally
        {
            handle.Free();
        }
    }
}
