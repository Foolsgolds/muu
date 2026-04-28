using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Muu.Interop;
using Muu.Models;

namespace Muu.Services;

/// <summary>
/// Discovers installed applications by scanning Start Menu shortcuts (.lnk).
/// Cached on first access; results are reused by both the search provider
/// and the registration form.
/// </summary>
public static class InstalledAppsService
{
    private static List<AppInfo>? _cache;
    private static readonly object _sync = new();

    public static IReadOnlyList<AppInfo> GetApps()
    {
        lock (_sync)
        {
            return _cache ??= Load();
        }
    }

    public static void Invalidate()
    {
        lock (_sync) _cache = null;
    }

    private static List<AppInfo> Load()
    {
        var result = new List<AppInfo>();

        var dirs = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs"),
        };

        foreach (var dir in dirs)
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var lnk in Directory.EnumerateFiles(dir, "*.lnk", SearchOption.AllDirectories))
            {
                try
                {
                    var entry = ResolveShortcut(lnk);
                    if (entry is not null)
                        result.Add(entry);
                }
                catch
                {
                    // Skip broken shortcuts
                }
            }
        }

        // De-duplicate: same display name + target path
        return result
            .GroupBy(a => (a.Name.ToLowerInvariant(), a.TargetPath.ToLowerInvariant()))
            .Select(g => g.First())
            .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static AppInfo? ResolveShortcut(string lnkPath)
    {
        var shellLink = (IShellLinkW)new ShellLink();
        var persistFile = (IPersistFile)shellLink;
        persistFile.Load(lnkPath, 0);

        var sb = new StringBuilder(260);
        shellLink.GetPath(sb, sb.Capacity, IntPtr.Zero, 0);
        string target = sb.ToString();

        if (string.IsNullOrWhiteSpace(target))
            return null;

        var argsSb = new StringBuilder(1024);
        shellLink.GetArguments(argsSb, argsSb.Capacity);
        string args = argsSb.ToString();

        string name = Path.GetFileNameWithoutExtension(lnkPath);

        BitmapSource? icon = null;
        try
        {
            // Use the .lnk path itself for icon lookup so we get the proper
            // shell icon (works even when the target is a Windows Store app).
            var info = default(NativeMethods.SHFILEINFO);
            uint flags = NativeMethods.SHGFI_ICON | NativeMethods.SHGFI_LARGEICON;
            var result = NativeMethods.SHGetFileInfo(lnkPath, 0, ref info,
                (uint)Marshal.SizeOf(info), flags);

            if (result != IntPtr.Zero && info.hIcon != IntPtr.Zero)
            {
                try
                {
                    icon = Imaging.CreateBitmapSourceFromHIcon(
                        info.hIcon, Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    icon.Freeze();
                }
                finally
                {
                    NativeMethods.DestroyIcon(info.hIcon);
                }
            }
        }
        catch { }

        return new AppInfo
        {
            Name = name,
            TargetPath = target,
            Arguments = args,
            Icon = icon,
        };
    }

    // COM interop for IShellLink
    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink { }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch, IntPtr pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out ushort pwHotkey);
        void SetHotkey(ushort wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cch, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }
}
