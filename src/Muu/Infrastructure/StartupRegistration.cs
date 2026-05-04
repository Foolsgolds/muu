using System.Diagnostics;
using Microsoft.Win32;

namespace Muu.Infrastructure;

/// <summary>
/// Registers Muu in the Windows "Run on logon" key so the user can
/// toggle auto-start from Settings → Apps → Startup. Uses the
/// per-user Run key (HKCU) so no admin elevation is needed.
/// </summary>
public static class StartupRegistration
{
    private const string RegPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Muu";

    public static bool IsRegistered()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegPath, writable: false);
            return key?.GetValue(ValueName) is not null;
        }
        catch
        {
            return false;
        }
    }

    public static void Register()
    {
        try
        {
            string? exe = GetExecutablePath();
            if (string.IsNullOrEmpty(exe)) return;

            using var key = Registry.CurrentUser.CreateSubKey(RegPath, writable: true);
            // Quote the path so spaces in the install location are handled.
            key?.SetValue(ValueName, "\"" + exe + "\"");
        }
        catch
        {
            // Best-effort; the user can always edit Run entries themselves.
        }
    }

    public static void Unregister()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegPath, writable: true);
            key?.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        catch { }
    }

    private static string? GetExecutablePath()
    {
        try
        {
            return Process.GetCurrentProcess().MainModule?.FileName;
        }
        catch
        {
            return null;
        }
    }
}
