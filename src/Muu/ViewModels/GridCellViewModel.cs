using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
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
        try
        {
            if (File.Exists(path))
            {
                using var ico = System.Drawing.Icon.ExtractAssociatedIcon(path);
                if (ico is not null)
                {
                    Icon = Imaging.CreateBitmapSourceFromHIcon(
                        ico.Handle, Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    ((BitmapSource)Icon).Freeze();
                }
            }
        }
        catch
        {
            Icon = null;
        }
    }
}
