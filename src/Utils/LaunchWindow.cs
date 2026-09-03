using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace TFROnlineMenu.Utils;

/// <summary>
/// Applies <c>--tfr-res</c> / <c>--tfr-pos</c> (client size; window top-left from the primary display).
/// </summary>
internal static class LaunchWindow
{
    private const string UnityWindowClass = "UnityWndClass";
    private const uint SwpNosize = 0x0001;
    private const uint SwpNozorder = 0x0004;

    public static void Initialize()
    {
        LaunchArgs.EnsureParsed();
        if (!LaunchArgs.HasWindowOverride) return;

        Apply();
    }

    private static void Apply()
    {
        if (LaunchArgs.Width is not int width || LaunchArgs.Height is not int height)
        {
            Screen.fullScreen = false;
            Screen.fullScreenMode = FullScreenMode.Windowed;
            return;
        }

        Screen.fullScreen = false;
        Screen.SetResolution(width, height, FullScreenMode.Windowed);
        if (LaunchArgs.PosX is not int x || LaunchArgs.PosY is not int y)
        {
            return;
        }

        var hwnd = FindUnityWindow();
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0, SwpNosize | SwpNozorder);
    }

    private static IntPtr FindUnityWindow()
    {
        var pid = (uint)Environment.ProcessId;
        var found = IntPtr.Zero;
        EnumWindows((hWnd, _) =>
        {
            GetWindowThreadProcessId(hWnd, out var windowPid);
            if (windowPid != pid || !IsWindowVisible(hWnd))
            {
                return true;
            }

            var className = new StringBuilder(256);
            GetClassName(hWnd, className, className.Capacity);
            if (!className.ToString().Equals(UnityWindowClass, StringComparison.Ordinal))
            {
                return true;
            }

            found = hWnd;
            return false;
        }, IntPtr.Zero);

        return found;
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
}
