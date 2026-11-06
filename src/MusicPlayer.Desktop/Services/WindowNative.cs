using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Controls;

namespace MusicPlayer.Desktop.Services;

/// <summary>
/// Lo que la isla necesita de Win32 y Avalonia no ofrece:
/// <list type="bullet">
/// <item>La región de la ventana (<c>SetWindowRgn</c>): fuera de ella los clics pasan a lo que hay debajo.
/// La isla no cambia de tamaño ni de sitio al expandirse: cambiar la ventana hace que Avalonia pinte el
/// contenido un fotograma tarde y la pastilla salte (medido en M5); cambiar la región, no.</item>
/// <item>El cursor real: el hover de Avalonia no se actualiza si lo que hay bajo un cursor quieto cambia.</item>
/// </list>
/// Fuera de Windows no hace nada (la isla no se muestra allí).
/// </summary>
public static class WindowNative
{
    /// <summary>
    /// Limita los clics de la ventana a un rectángulo redondeado, en píxeles físicos relativos a su esquina.
    /// </summary>
    public static void SetHitRegion(Window window, PixelRect area, int cornerRadius)
    {
        if (!OperatingSystem.IsWindows()) return;
        if (window.TryGetPlatformHandle()?.Handle is not { } hwnd || hwnd == IntPtr.Zero) return;
        SetRegionOnWindows(hwnd, area, cornerRadius);
    }

    /// <summary>Posición del cursor en píxeles físicos, o null si no se puede saber.</summary>
    public static PixelPoint? CursorPosition()
    {
        if (!OperatingSystem.IsWindows()) return null;
        return CursorOnWindows();
    }

    [SupportedOSPlatform("windows")]
    private static void SetRegionOnWindows(IntPtr hwnd, PixelRect a, int radius)
    {
        // CreateRoundRectRgn excluye el borde derecho e inferior: +1 para cubrir el área entera.
        var region = CreateRoundRectRgn(a.X, a.Y, a.Right + 1, a.Bottom + 1, 2 * radius, 2 * radius);
        if (region == IntPtr.Zero) return;
        // Si SetWindowRgn tiene éxito, la región pasa a ser del sistema y no se libera.
        if (SetWindowRgn(hwnd, region, true) == 0)
            DeleteObject(region);
    }

    [SupportedOSPlatform("windows")]
    private static PixelPoint? CursorOnWindows() =>
        GetCursorPos(out var p) ? new PixelPoint(p.X, p.Y) : null;

    [StructLayout(LayoutKind.Sequential)]
    private struct Point32
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, [MarshalAs(UnmanagedType.Bool)] bool redraw);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int w, int h);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr obj);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out Point32 point);
}
