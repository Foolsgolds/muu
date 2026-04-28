using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Muu.Infrastructure;
using Muu.Models;
using Muu.Services;

namespace Muu.Views;

public partial class GridItemSettingsWindow : Window
{
    public GridItem? Result { get; private set; }
    public bool Cleared { get; private set; }

    private readonly int _row;
    private readonly int _col;
    private IReadOnlyList<AppInfo> _allApps = [];
    private bool _initialised;

    public GridItemSettingsWindow(int row, int col, GridItem? existing)
    {
        InitializeComponent();
        _row = row;
        _col = col;

        PositionLabel.Text = $"スロット位置: 行 {row + 1}, 列 {col + 1}";

        // Determine initial kind
        var kind = existing?.Kind ?? GridItemKind.App;
        SelectKind(kind);

        // Pre-fill fields based on kind
        if (existing is not null)
        {
            switch (kind)
            {
                case GridItemKind.App:
                    AppNameBox.Text = existing.Name;
                    break;
                case GridItemKind.Folder:
                    FolderNameBox.Text = existing.Name;
                    FolderPathBox.Text = existing.TargetPath;
                    break;
                case GridItemKind.File:
                    FileNameBox.Text = existing.Name;
                    FilePathBox.Text = existing.TargetPath;
                    FileArgsBox.Text = existing.Arguments;
                    break;
            }
        }

        _initialised = true;
        Loaded += GridItemSettingsWindow_Loaded;
    }

    private async void GridItemSettingsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Theme follows the OS light/dark setting
        ThemeHelper.Apply(this);

        // Populate the apps list off the UI thread (.lnk scanning can take a moment)
        _allApps = await Task.Run(() => InstalledAppsService.GetApps());
        AppListBox.ItemsSource = _allApps;

        // If editing an existing App registration, pre-select that app
        if (KindCombo.SelectedItem is ComboBoxItem item
            && (string?)item.Tag == "App"
            && !string.IsNullOrWhiteSpace(AppNameBox.Text))
        {
            var match = _allApps.FirstOrDefault(a =>
                string.Equals(a.Name, AppNameBox.Text, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
                AppListBox.SelectedItem = match;
        }
    }

    // ─── Kind selection ──────────────────────────────────────
    // The ComboBox additionally supports a "None" option used to clear the
    // slot (replaces the old "Clear" button). It is represented internally
    // as a null GridItemKind.

    private void SelectKind(GridItemKind kind) => SelectKindByTag(kind.ToString());

    private void SelectKindByTag(string tag)
    {
        foreach (ComboBoxItem item in KindCombo.Items)
        {
            if ((string?)item.Tag == tag)
            {
                KindCombo.SelectedItem = item;
                break;
            }
        }
        UpdateSectionVisibility(CurrentKind());
    }

    /// <summary>Currently selected GridItemKind, or null if "None" is selected.</summary>
    private GridItemKind? CurrentKind()
    {
        if (KindCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            if (tag == "None") return null;
            if (Enum.TryParse(tag, out GridItemKind k)) return k;
        }
        return GridItemKind.File;
    }

    private void UpdateSectionVisibility(GridItemKind? kind)
    {
        AppSection.Visibility = kind == GridItemKind.App ? Visibility.Visible : Visibility.Collapsed;
        FolderSection.Visibility = kind == GridItemKind.Folder ? Visibility.Visible : Visibility.Collapsed;
        FileSection.Visibility = kind == GridItemKind.File ? Visibility.Visible : Visibility.Collapsed;
    }

    private void KindCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialised) return;
        UpdateSectionVisibility(CurrentKind());
    }

    // ─── App section ─────────────────────────────────────────

    private void AppFilterBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var query = AppFilterBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(query))
        {
            AppListBox.ItemsSource = _allApps;
            return;
        }

        var filtered = _allApps
            .Select(a => (app: a, score: FuzzyMatcher.Score(query, a.Name)))
            .Where(x => x.score > 0)
            .OrderByDescending(x => x.score)
            .Select(x => x.app)
            .ToList();
        AppListBox.ItemsSource = filtered;
    }

    private void AppListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AppListBox.SelectedItem is AppInfo app)
        {
            AppNameBox.Text = app.Name;
        }
    }

    // ─── Folder / File browse ────────────────────────────────

    private void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog
        {
            Title = "フォルダを選択",
        };
        if (dlg.ShowDialog() == true)
        {
            FolderPathBox.Text = dlg.FolderName;
            if (string.IsNullOrWhiteSpace(FolderNameBox.Text))
                FolderNameBox.Text = Path.GetFileName(dlg.FolderName);
        }
    }

    private void BrowseFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "ファイルを選択",
            Filter = "実行ファイル (*.exe;*.lnk)|*.exe;*.lnk|すべてのファイル (*.*)|*.*",
        };
        if (dlg.ShowDialog() == true)
        {
            FilePathBox.Text = dlg.FileName;
            if (string.IsNullOrWhiteSpace(FileNameBox.Text))
                FileNameBox.Text = Path.GetFileNameWithoutExtension(dlg.FileName);
        }
    }

    // ─── OK / Cancel ─────────────────────────────────────────

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        var kind = CurrentKind();

        // "なし (未登録)" — clear the slot
        if (kind is null)
        {
            Cleared = true;
            Result = new GridItem { Row = _row, Column = _col };
            DialogResult = true;
            return;
        }

        switch (kind)
        {
            case GridItemKind.App:
                if (AppListBox.SelectedItem is not AppInfo app)
                {
                    MessageBox.Show(this, "アプリを選択してください。",
                        "Muu", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                Result = new GridItem
                {
                    Row = _row,
                    Column = _col,
                    Kind = GridItemKind.App,
                    Name = string.IsNullOrWhiteSpace(AppNameBox.Text) ? app.Name : AppNameBox.Text.Trim(),
                    TargetPath = app.TargetPath,
                    Arguments = app.Arguments,
                };
                break;

            case GridItemKind.Folder:
                if (string.IsNullOrWhiteSpace(FolderPathBox.Text))
                {
                    MessageBox.Show(this, "フォルダパスを入力してください。",
                        "Muu", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                Result = new GridItem
                {
                    Row = _row,
                    Column = _col,
                    Kind = GridItemKind.Folder,
                    Name = string.IsNullOrWhiteSpace(FolderNameBox.Text)
                        ? Path.GetFileName(FolderPathBox.Text.Trim())
                        : FolderNameBox.Text.Trim(),
                    TargetPath = FolderPathBox.Text.Trim(),
                    Arguments = string.Empty,
                };
                break;

            case GridItemKind.File:
                if (string.IsNullOrWhiteSpace(FilePathBox.Text))
                {
                    MessageBox.Show(this, "ファイルパスを入力してください。",
                        "Muu", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                Result = new GridItem
                {
                    Row = _row,
                    Column = _col,
                    Kind = GridItemKind.File,
                    Name = string.IsNullOrWhiteSpace(FileNameBox.Text)
                        ? Path.GetFileNameWithoutExtension(FilePathBox.Text.Trim())
                        : FileNameBox.Text.Trim(),
                    TargetPath = FilePathBox.Text.Trim(),
                    Arguments = FileArgsBox.Text.Trim(),
                };
                break;
        }

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
