using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;

namespace MusicPlayer.Desktop.Controls;

/// <summary>
/// Texto que se rellena de izquierda a derecha: se dibuja tenue y, encima, brillante hasta <see cref="FillX"/>,
/// con el borde del relleno suavizado. Si el texto ocupa varios renglones, el relleno los recorre en orden.
/// </summary>
/// <remarks>
/// <see cref="FillX"/> es una distancia en DIPs medida a lo largo del texto: el ancho de los renglones anteriores
/// más lo avanzado en el actual (0 = nada, <see cref="FillLength"/> = todo). Es una posición y no una fracción
/// para que, más adelante, el relleno pueda seguir el ancho de cada palabra (spec, RNF-5).
/// </remarks>
public sealed class FillTextBlock : Control
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<FillTextBlock, string?>(nameof(Text));

    public static readonly StyledProperty<double> FontSizeProperty =
        TextElement.FontSizeProperty.AddOwner<FillTextBlock>();

    public static readonly StyledProperty<FontWeight> FontWeightProperty =
        TextElement.FontWeightProperty.AddOwner<FillTextBlock>();

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        TextElement.FontFamilyProperty.AddOwner<FillTextBlock>();

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<FillTextBlock>();

    public static readonly StyledProperty<IBrush?> DimForegroundProperty =
        AvaloniaProperty.Register<FillTextBlock, IBrush?>(nameof(DimForeground));

    public static readonly StyledProperty<double> FillXProperty =
        AvaloniaProperty.Register<FillTextBlock, double>(nameof(FillX));

    /// <summary>Ancho del borde suavizado del relleno, en DIPs.</summary>
    public static readonly StyledProperty<double> SoftEdgeProperty =
        AvaloniaProperty.Register<FillTextBlock, double>(nameof(SoftEdge), 24);

    private TextLayout? _dim;
    private TextLayout? _bright;
    private Typeface _typeface;
    private double _maxWidth = double.PositiveInfinity;

    static FillTextBlock()
    {
        AffectsMeasure<FillTextBlock>(TextProperty, FontSizeProperty, FontWeightProperty, FontFamilyProperty,
            ForegroundProperty, DimForegroundProperty);
        AffectsRender<FillTextBlock>(FillXProperty, SoftEdgeProperty);
    }

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public IBrush? DimForeground { get => GetValue(DimForegroundProperty); set => SetValue(DimForegroundProperty, value); }
    public double FillX { get => GetValue(FillXProperty); set => SetValue(FillXProperty, value); }
    public double SoftEdge { get => GetValue(SoftEdgeProperty); set => SetValue(SoftEdgeProperty, value); }

    /// <summary>Longitud total que recorre el relleno: la suma del ancho de los renglones (sin espacios finales).</summary>
    public double FillLength { get; private set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        var text = Text ?? string.Empty;
        var typeface = new Typeface(FontFamily, FontStyle.Normal, FontWeight);
        var maxWidth = double.IsInfinity(availableSize.Width) ? double.PositiveInfinity : availableSize.Width;

        _dim = new TextLayout(text, typeface, FontSize, DimForeground,
            textWrapping: TextWrapping.Wrap, maxWidth: maxWidth);
        _bright = new TextLayout(text, typeface, FontSize, Foreground,
            textWrapping: TextWrapping.Wrap, maxWidth: maxWidth);

        double length = 0;
        foreach (var line in _bright.TextLines)
            length += line.Width;
        FillLength = length;

        _typeface = typeface;
        _maxWidth = maxWidth;
        return new Size(_dim.WidthIncludingTrailingWhitespace, _dim.Height);
    }

    /// <summary>
    /// El texto con un degradado horizontal: color pleno hasta <paramref name="from"/> y transparente desde
    /// <paramref name="to"/>. Las coordenadas son las del control (comprobado con capturas en M2, también en el
    /// segundo renglón). Con varias tiradas en un renglón (mezcla de alfabetos con otra fuente) no se ha probado.
    /// </summary>
    private TextLayout EdgeLayout(double from, double to)
    {
        var color = (Foreground as ISolidColorBrush)?.Color ?? Colors.White;
        var brush = new ImmutableLinearGradientBrush(
            [new ImmutableGradientStop(0, color), new ImmutableGradientStop(1, Color.FromArgb(0, color.R, color.G, color.B))],
            opacity: Foreground?.Opacity ?? 1,
            startPoint: new RelativePoint(from, 0, RelativeUnit.Absolute),
            endPoint: new RelativePoint(to, 0, RelativeUnit.Absolute));

        return new TextLayout(Text ?? string.Empty, _typeface, FontSize, brush,
            textWrapping: TextWrapping.Wrap, maxWidth: _maxWidth);
    }

    public override void Render(DrawingContext context)
    {
        if (_dim is null || _bright is null) return;

        _dim.Draw(context, default);

        var fill = FillX;
        if (fill <= 0) return;

        var soft = Math.Max(0, SoftEdge);
        double before = 0; // ancho de los renglones ya recorridos
        double y = 0;

        foreach (var line in _bright.TextLines)
        {
            var width = line.Width;
            var advanced = Math.Clamp(fill - before, 0, width);
            if (advanced <= 0) break;

            var x0 = line.Start;
            var rect = new Rect(x0, y, width + soft, line.Height);

            if (fill - before >= width)
            {
                // Renglón completo: color pleno.
                using (context.PushClip(rect))
                    _bright.Draw(context, default);
            }
            else
            {
                // El borde recorre width + soft para que, al llegar al final, el degradado ya haya salido del texto
                // y no haya un salto al pasar de «parcial» a «completo».
                var edge = x0 + (width <= 0 ? 0 : advanced / width * (width + soft));

                // El degradado va en el propio texto (de brillante a transparente), no en una máscara de opacidad:
                // PushOpacityMask abre una capa en cada fotograma (M2). TextLayout fija su pincel al crearse, así
                // que el renglón parcial se dibuja con uno nuevo; el recorte deja fuera los demás renglones.
                using (context.PushClip(rect))
                    EdgeLayout(edge - soft, edge).Draw(context, default);
            }

            before += width;
            y += line.Height;
        }
    }
}
