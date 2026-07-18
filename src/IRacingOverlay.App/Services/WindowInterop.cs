using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace IRacingOverlay.App.Services;

/// <summary>
/// Win32 window-style plumbing WPF doesn't expose. Currently just click-through:
/// there is no WPF property for it, because it's a window-manager behaviour
/// rather than a rendering one - the extended style tells Windows to route hit
/// tests past the window entirely, so clicks land on the sim behind it.
/// </summary>
public static class WindowInterop
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    /// <summary>
    /// Makes a window transparent to the mouse, or restores it.
    ///
    /// A click-through widget cannot be dragged, so the settings surface is the
    /// only way back - which is why this is opt-in per widget rather than a
    /// global toggle, and why the settings window itself is never click-through.
    ///
    /// No-ops if the window has no handle yet (not shown): callers apply this
    /// after <c>Show()</c>, and re-apply on a settings change.
    /// </summary>
    public static void SetClickThrough(Window window, bool clickThrough)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var style = (long)GetWindowLongPtr(handle, GwlExStyle);
        var updated = clickThrough
            ? style | WsExTransparent
            : style & ~WsExTransparent;

        if (updated != style)
        {
            SetWindowLongPtr(handle, GwlExStyle, (IntPtr)updated);
        }
    }
}
