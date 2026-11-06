using System.Globalization;

namespace MusicPlayer.Core.Models;

/// <summary>Color sólido opaco. En los temas se escribe siempre como <c>#RRGGBB</c>.</summary>
public readonly record struct ThemeColor(byte R, byte G, byte B)
{
    /// <summary>Acepta solo <c>#RRGGBB</c> (sin distinguir mayúsculas y sin contar espacios alrededor).</summary>
    public static bool TryParse(string? text, out ThemeColor color)
    {
        color = default;
        var s = text?.Trim();
        if (s is not { Length: 7 } || s[0] != '#')
            return false;

        if (!int.TryParse(s.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value))
            return false;

        color = new ThemeColor((byte)(value >> 16), (byte)(value >> 8), (byte)value);
        return true;
    }

    public static ThemeColor Parse(string text) =>
        TryParse(text, out var color) ? color : throw new FormatException($"Color no válido: '{text}'.");

    public string ToHex() => $"#{R:X2}{G:X2}{B:X2}";

    public override string ToString() => ToHex();
}
