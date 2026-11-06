using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia.Controls;

namespace MusicPlayer.Desktop.Services;

/// <summary>
/// Hace que una ventana no se active al pulsarla (WS_EX_NOACTIVATE) y no aparezca en la barra de tareas
/// ni en Alt+Tab (WS_EX_TOOLWINDOW). Solo Windows: en el resto no hace nada.
/// </summary>
public static class NonActivatingWindow
{
    private const int GwlExStyle = -20;
    private const long WsExToolWindow = 0x00000080L;
    private const long WsExNoActivate = 0x08000000L;

    public static void Apply(Window window)
    {
        if (!OperatingSystem.IsWindows()) return;
        if (window.TryGetPlatformHandle()?.Handle is not { } hwnd || hwnd == IntPtr.Zero) return;
        ApplyOnWindows(hwnd);
    }

    [SupportedOSPlatform("windows")]
    private static void ApplyOnWindows(IntPtr hwnd)
    {
        var style = GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();
        SetWindowLongPtr(hwnd, GwlExStyle, new IntPtr(style | WsExNoActivate | WsExToolWindow));
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
}
