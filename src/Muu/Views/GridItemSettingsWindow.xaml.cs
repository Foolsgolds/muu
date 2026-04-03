using System.Windows;
using Microsoft.Win32;
using Muu.Models;

namespace Muu.Views;

public partial class GridItemSettingsWindow : Window
{
    public GridItem? Result { get; private set; }
    public bool Cleared { get; private set; }
    private readonly int _row;
    private readonly int _col;

    public GridItemSettingsWindow(int row, int col, GridItem? existing)
    {
        InitializeComponent();
        _row = row;
        _col = col;

        PositionLabel.Text = $"スロット位置: 行 {row + 1}, 列 {col + 1}";

        if (existing is not null)
        {
            NameBox.Text = existing.Name;
            PathBox.Text = existing.TargetPath;
            ArgsBox.Text = existing.Arguments;
        }
    }

    private void BrowseFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "アプリケーションを選択",
            Filter = "実行ファイル (*.exe;*.lnk)|*.exe;*.lnk|すべてのファイル (*.*)|*.*",
        };
        if (dlg.ShowDialog() == true)
        {
            PathBox.Text = dlg.FileName;
            if (string.IsNullOrWhiteSpace(NameBox.Text))
                NameBox.Text = System.IO.Path.GetFileNameWithoutExtension(dlg.FileName);
        }
    }

    private void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog
        {
            Title = "フォルダを選択",
        };
        if (dlg.ShowDialog() == true)
        {
            PathBox.Text = dlg.FolderName;
            if (string.IsNullOrWhiteSpace(NameBox.Text))
                NameBox.Text = System.IO.Path.GetFileName(dlg.FolderName);
        }
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        Result = new GridItem
        {
            Row = _row,
            Column = _col,
            Name = NameBox.Text.Trim(),
            TargetPath = PathBox.Text.Trim(),
            Arguments = ArgsBox.Text.Trim(),
        };
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        Cleared = true;
        Result = new GridItem { Row = _row, Column = _col };
        DialogResult = true;
    }
}
