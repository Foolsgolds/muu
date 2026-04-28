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
    private ImageSource? _icon;

    public bool HasItem => !string.IsNullOrWhiteSpace(TargetPath);

    public GridCellViewModel(int row, int column)
    {
        Row = row;
        Column = column;
    }

    public void LoadFrom(GridItem? item)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.TargetPath))
        {
            Name = string.Empty;
            TargetPath = string.Empty;
            Arguments = string.Empty;
            Icon = null;
        }
        else
        {
            Name = item.Name;
            TargetPath = item.TargetPath;
            Arguments = item.Arguments;
            LoadIcon(item.TargetPath);
        }
        OnPropertyChanged(nameof(HasItem));
    }

    public GridItem ToGridItem() => new()
    {
        Row = Row,
        Column = Column,
        Name = Name,
        TargetPath = TargetPath,
        Arguments = Arguments,
    };

    public void Launch()
    {
        if (!HasItem) return;
        try
        {
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
}
