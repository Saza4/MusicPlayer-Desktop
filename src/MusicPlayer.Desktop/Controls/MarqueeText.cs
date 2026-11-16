using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using MusicPlayer.Core.Services;

namespace MusicPlayer.Desktop.Controls;

/// <summary>
/// Un renglón de texto que, si no cabe, se dibuja corrido <see cref="Offset"/> hacia la izquierda, seguido de una
/// segunda copia tras <see cref="Gap"/> (así el bucle no tiene salto). Cuánto se corre lo decide quien lo usa con
/// <see cref="Marquee.Offset"/>; este control solo dibuja (docs/specs/island-title-and-close.md, criterio 6).
/// </summary>
/// <remarks>
/// El texto se compone al medir (cambio de texto, de fuente o de color), no en cada fotograma. Con
/// <see cref="IsScrolling"/> apagado y un texto que no cabe, se muestra el principio recortado con «…»; esa
/// versión se compone una vez por ancho.
/// </remarks>
public sealed class MarqueeText : Control
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<MarqueeText, string?>(nameof(Text));

    public static readonly StyledProperty<double> FontSizeProperty =
        TextElement.FontSizeProperty.AddOwner<MarqueeText>();

    public static readonly StyledProperty<FontWeight> FontWeightProperty =
        TextElement.FontWeightProperty.AddOwner<MarqueeText>();

    public static readonly StyledProperty<FontStyle> FontStyleProperty =
        TextElement.FontStyleProperty.AddOwner<MarqueeText>();

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        TextElement.FontFamilyProperty.AddOwner<MarqueeText>();

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<MarqueeText>();

    public static readonly StyledProperty<double> OffsetProperty =
        AvaloniaProperty.Register<MarqueeText, double>(nameof(Offset));

    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<MarqueeText, double>(nameof(Gap), 32);

    public static readonly StyledProperty<bool> IsScrollingProperty =
        AvaloniaProperty.Register<MarqueeText, bool>(nameof(IsScrolling));

    private TextLayout? _full;
    private TextLayout? _trimmed;
    private double _trimmedWidth = double.NaN;
    private Typeface _typeface;

    static MarqueeText()
    {
        AffectsMeasure<MarqueeText>(TextProperty, FontSizeProperty, FontWeightProperty, FontStyleProperty,
            FontFamilyProperty, ForegroundProperty);
        AffectsRender<MarqueeText>(OffsetProperty, GapProperty, IsScrollingProperty);
        ClipToBoundsProperty.OverrideDefaultValue<MarqueeText>(true);
    }

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }
    public FontStyle FontStyle { get => GetValue(FontStyleProperty); set => SetValue(FontStyleProperty, value); }
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    /// <summary>Cuánto se ha corrido el texto hacia la izquierda, en DIPs.</summary>
    public double Offset { get => GetValue(OffsetProperty); set => SetValue(OffsetProperty, value); }

    /// <summary>Hueco entre el final del texto y su siguiente copia, en DIPs.</summary>
    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    /// <summary>Apagado, un texto que no cabe se queda quieto al principio y recortado con «…».</summary>
    public bool IsScrolling { get => GetValue(IsScrollingProperty); set => SetValue(IsScrollingProperty, value); }

    /// <summary>Ancho del texto entero, en DIPs (0 si aún no se ha medido).</summary>
    public double TextWidth { get; private set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        _typeface = new Typeface(FontFamily, FontStyle, FontWeight);
        _full = new TextLayout(Text ?? string.Empty, _typeface, FontSize, Foreground, maxLines: 1);
        _trimmed = null;
        TextWidth = _full.WidthIncludingTrailingWhitespace;

        var width = double.IsInfinity(availableSize.Width) ? TextWidth : Math.Min(TextWidth, availableSize.Width);
        return new Size(width, _full.Height);
    }

    public override void Render(DrawingContext context)
    {
        if (_full is null) return;

        var width = Bounds.Width;
        if (Marquee.Fits(TextWidth, width))
        {
            _full.Draw(context, default);
            return;
        }

        if (!IsScrolling)
        {
            if (_trimmed is null || _trimmedWidth != width)
            {
                _trimmed = new TextLayout(Text ?? string.Empty, _typeface, FontSize, Foreground,
                    textTrimming: TextTrimming.CharacterEllipsis, maxWidth: width, maxLines: 1);
                _trimmedWidth = width;
            }
            _trimmed.Draw(context, default);
            return;
        }

        // ClipToBounds recorta lo que sobresale. La segunda copia solo se dibuja cuando ya asoma.
        var x = -Offset;
        _full.Draw(context, new Point(x, 0));
        var second = x + TextWidth + Math.Max(0, Gap);
        if (second < width)
            _full.Draw(context, new Point(second, 0));
    }
}
