namespace Muu.Interop;

public static class DwmHelper
{
    public static void EnableMica(IntPtr hwnd)
    {
        int darkMode = 1;
        NativeMethods.DwmSetWindowAttribute(hwnd,
            NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE,
            ref darkMode, sizeof(int));

        int backdropType = NativeMethods.DWMSBT_MAINWINDOW; // Mica
        NativeMethods.DwmSetWindowAttribute(hwnd,
            NativeMethods.DWMWA_SYSTEMBACKDROP_TYPE,
            ref backdropType, sizeof(int));
    }

    public static void EnableAcrylic(IntPtr hwnd)
    {
        int darkMode = 1; // Dark mode
        NativeMethods.DwmSetWindowAttribute(hwnd,
            NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE,
            ref darkMode, sizeof(int));

        int backdropType = NativeMethods.DWMSBT_TRANSIENTWINDOW; // Acrylic
        NativeMethods.DwmSetWindowAttribute(hwnd,
            NativeMethods.DWMWA_SYSTEMBACKDROP_TYPE,
            ref backdropType, sizeof(int));
    }
}
