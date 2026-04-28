using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using Muu.Interop;

namespace Muu.Infrastructure;

/// <summary>
/// Detects the current Windows light/dark theme and applies matching
/// background, foreground, and control brushes to a Window.
/// </summary>
public static class ThemeHelper
{
    public static bool IsLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var v = key?.GetValue("AppsUseLightTheme");
            return v is int i && i == 1;
        }
        catch
        {
            return true;
        }
    }

    /// <summary>Apply theme-aware brushes to a window's resources.</summary>
    public static void Apply(Window window)
    {
        bool light = IsLightTheme();

        // Backgrounds
        var winBg     = Color(light ? "#F3F3F3" : "#1E1E1E");
        var ctrlBg    = Color(light ? "#FFFFFF" : "#2D2D2D");
        var ctrlBgAlt = Color(light ? "#F8F8F8" : "#262626");
        var border    = Color(light ? "#D0D0D0" : "#444444");

        // Foregrounds
        var primaryFg   = Color(light ? "#1F1F1F" : "#E4E4E4");
        var secondaryFg = Color(light ? "#666666" : "#888888");

        // Buttons
        var btnBg      = Color(light ? "#FAFAFA" : "#3A3A3A");
        var btnBorder  = Color(light ? "#C0C0C0" : "#555555");

        window.Resources["WindowBg"]    = Brush(winBg);
        window.Resources["ControlBg"]   = Brush(ctrlBg);
        window.Resources["ListBg"]      = Brush(ctrlBgAlt);
        window.Resources["BorderBrush"] = Brush(border);
        window.Resources["PrimaryFg"]   = Brush(primaryFg);
        window.Resources["SecondaryFg"] = Brush(secondaryFg);
        window.Resources["ButtonBg"]    = Brush(btnBg);
        window.Resources["ButtonBorder"]= Brush(btnBorder);

        window.Background = Brush(winBg);

        // Apply DWM dark mode to the title bar so the system chrome matches
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd != IntPtr.Zero)
        {
            int useDark = light ? 0 : 1;
            NativeMethods.DwmSetWindowAttribute(hwnd,
                NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE,
                ref useDark, sizeof(int));
        }
    }

    private static Color Color(string hex)
        => (Color)ColorConverter.ConvertFromString(hex)!;

    private static SolidColorBrush Brush(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }
}
