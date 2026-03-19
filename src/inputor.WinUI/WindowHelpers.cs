using System;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace Inputor.WinUI;

internal static class WindowHelpers
{
    private const int SwHide = 0;
    private const int SwRestore = 9;

    public static AppWindow GetAppWindow(Window window)
    {
        var hWnd = WindowNative.GetWindowHandle(window);
        var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
        return AppWindow.GetFromWindowId(windowId);
    }

    public static void RegisterHideOnClose(Window window, Func<bool> shouldHide)
    {
        GetAppWindow(window).Closing += (_, args) =>
        {
            if (!shouldHide())
            {
                return;
            }

            args.Cancel = true;
            HideWindow(window);
        };
    }

    public static void HideWindow(Window window)
    {
        _ = ShowWindow(WindowNative.GetWindowHandle(window), SwHide);
    }

    public static void ShowWindow(Window window)
    {
        var handle = WindowNative.GetWindowHandle(window);
        _ = ShowWindow(handle, SwRestore);
        window.Activate();
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);
}
