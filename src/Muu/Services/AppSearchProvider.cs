using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Muu.Models;

namespace Muu.Services;

public sealed class AppSearchProvider : ISearchProvider
{
    private readonly List<AppEntry> _apps = [];
    private bool _loaded;

    public bool CanHandle(string query) => !query.StartsWith('=');

    public Task<IReadOnlyList<SearchResult>> SearchAsync(string query, CancellationToken ct)
    {
        EnsureLoaded();

        var results = _apps
            .Select(app => (app, score: FuzzyMatcher.Score(query, app.Name)))
            .Where(x => x.score > 0)
            .OrderByDescending(x => x.score)
            .Take(8)
            .Select(x => new SearchResult
            {
                Title = x.app.Name,
                Subtitle = x.app.TargetPath,
                Kind = SearchResultKind.App,
                Icon = x.app.Icon,
                Score = x.score,
                Execute = () => Launch(x.app)
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<SearchResult>>(results);
    }

    private void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;

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
                        _apps.Add(entry);
                }
                catch
                {
                    // Skip broken shortcuts
                }
            }
        }
    }

    private static AppEntry? ResolveShortcut(string lnkPath)
    {
        // Use Shell32 COM directly to resolve .lnk
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
        ImageSource? icon = null;

        try
        {
            if (File.Exists(target))
            {
                using var ico = System.Drawing.Icon.ExtractAssociatedIcon(target);
                if (ico is not null)
                {
                    icon = Imaging.CreateBitmapSourceFromHIcon(
                        ico.Handle,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    icon.Freeze();
                }
            }
        }
        catch { }

        return new AppEntry(name, target, args, icon);
    }

    private static void Launch(AppEntry app)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = app.TargetPath,
            Arguments = app.Arguments,
            UseShellExecute = true,
        });
    }

    private sealed record AppEntry(string Name, string TargetPath, string Arguments, ImageSource? Icon);

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
