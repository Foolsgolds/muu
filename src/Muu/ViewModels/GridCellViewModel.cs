using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Muu.Interop;
using Muu.Models;

namespace Muu.ViewModels;

public partial class GridCellViewModel : ObservableObject
{
    public int Row { get; }
    public int Column { get; }
    public bool IsCenter => Row == 2 && Column == 2;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _targetPath = string.Empty;

    [ObservableProperty]
    private string _arguments = string.Empty;

    [ObservableProperty]
    private GridItemKind _kind = GridItemKind.File;

    [ObservableProperty]
    private SystemAction _systemAction = SystemAction.None;

    [ObservableProperty]
    private ImageSource? _icon;

    public bool HasItem =>
        (Kind == GridItemKind.System && SystemAction != SystemAction.None)
        || !string.IsNullOrWhiteSpace(TargetPath);

    public bool IsSystem => Kind == GridItemKind.System && SystemAction != SystemAction.None;

    public GridCellViewModel(int row, int column)
    {
        Row = row;
        Column = column;
    }

    public void LoadFrom(GridItem? item)
    {
        bool isSystem = item is { Kind: GridItemKind.System, SystemAction: not SystemAction.None };

        if (item is null || (!isSystem && string.IsNullOrWhiteSpace(item.TargetPath)))
        {
            Name = string.Empty;
            TargetPath = string.Empty;
            Arguments = string.Empty;
            Kind = GridItemKind.File;
            SystemAction = SystemAction.None;
            Icon = null;
        }
        else
        {
            Name = item.Name;
            TargetPath = item.TargetPath;
            Arguments = item.Arguments;
            Kind = item.Kind;
            SystemAction = item.SystemAction;
            if (!isSystem)
                LoadIcon(item.TargetPath);
            else
                Icon = null; // system slots use a glyph rendered by the view
        }
        OnPropertyChanged(nameof(HasItem));
        OnPropertyChanged(nameof(IsSystem));
    }

    public GridItem ToGridItem() => new()
    {
        Row = Row,
        Column = Column,
        Name = Name,
        TargetPath = TargetPath,
        Arguments = Arguments,
        Kind = Kind,
        SystemAction = SystemAction,
    };

    public void Launch()
    {
        if (!HasItem) return;
        try
        {
            // For UWP / Store apps the TargetPath is "shell:AppsFolder\<AUMID>".
            // Process.Start can't invoke that directly even with UseShellExecute,
            // so we route it through explorer.exe.
            if (TargetPath.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = "\"" + TargetPath + "\"",
                    UseShellExecute = false,
                });
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = TargetPath,
                Arguments = Arguments,
                UseShellExecute = true,
            });
        }
        catch { }
    }

    private void LoadIcon(string path)
    {
        Icon = null;
        try
        {
            // shell: URIs (UWP / Store apps) need PIDL-based icon lookup.
            if (path.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
            {
                Icon = LoadShellIcon(path);
                return;
            }

            bool isDirectory = Directory.Exists(path);
            bool exists = isDirectory || File.Exists(path);

            // Use SHGetFileInfo so we get proper icons for files AND folders.
            uint attrs = isDirectory ? NativeMethods.FILE_ATTRIBUTE_DIRECTORY
                                     : NativeMethods.FILE_ATTRIBUTE_NORMAL;
            uint flags = NativeMethods.SHGFI_ICON | NativeMethods.SHGFI_LARGEICON;
            // If the path doesn't exist, fall back to file-attribute-only mode so
            // we still get a generic icon for the type.
            if (!exists)
                flags |= NativeMethods.SHGFI_USEFILEATTRIBUTES;

            var info = default(NativeMethods.SHFILEINFO);
            var result = NativeMethods.SHGetFileInfo(path, attrs, ref info,
                (uint)System.Runtime.InteropServices.Marshal.SizeOf(info), flags);

            if (result != IntPtr.Zero && info.hIcon != IntPtr.Zero)
            {
                try
                {
                    var bmp = Imaging.CreateBitmapSourceFromHIcon(
                        info.hIcon, Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    bmp.Freeze();
                    Icon = bmp;
                }
                finally
                {
                    NativeMethods.DestroyIcon(info.hIcon);
                }
            }
        }
        catch
        {
            Icon = null;
        }
    }

    private static BitmapSource? LoadShellIcon(string parsingName)
    {
        IntPtr pidl = IntPtr.Zero;
        try
        {
            int hr = NativeMethods.SHParseDisplayName(parsingName, IntPtr.Zero, out pidl, 0, out _);
            if (hr != 0 || pidl == IntPtr.Zero) return null;

            var info = default(NativeMethods.SHFILEINFO);
            uint flags = NativeMethods.SHGFI_ICON
                       | NativeMethods.SHGFI_LARGEICON
                       | NativeMethods.SHGFI_PIDL;
            var rc = NativeMethods.SHGetFileInfoPidl(pidl, 0, ref info,
                (uint)System.Runtime.InteropServices.Marshal.SizeOf(info), flags);

            if (rc == IntPtr.Zero || info.hIcon == IntPtr.Zero) return null;

            try
            {
                var bmp = Imaging.CreateBitmapSourceFromHIcon(
                    info.hIcon, Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                bmp.Freeze();
                return bmp;
            }
            finally
            {
                NativeMethods.DestroyIcon(info.hIcon);
            }
        }
        catch
        {
            return null;
        }
        finally
        {
            if (pidl != IntPtr.Zero) NativeMethods.CoTaskMemFree(pidl);
        }
    }
}
