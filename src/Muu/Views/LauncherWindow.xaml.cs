using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using Muu.Interop;
using Muu.Models;
using Muu.ViewModels;

namespace Muu.Views;

public partial class LauncherWindow : Window
{
    private const int GridSize = 5;
    private const int CenterIndex = 12; // row=2, col=2 in 5x5 (drag handle)
    private const double CellSize = 48;
    private const double CellMargin = 4;

    private readonly Border?[] _cellBorders = new Border?[GridSize * GridSize];
    private int _focusedCellIndex = -1;

    private LauncherViewModel ViewModel => (LauncherViewModel)DataContext;

    public LauncherWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => BuildGrid();
    }

    // ─── Grid ────────────────────────────────────────────────

    private void BuildGrid()
    {
        AppGrid.Children.Clear();
        Array.Clear(_cellBorders);
        _focusedCellIndex = -1;

        for (int r = 0; r < GridSize; r++)
        for (int c = 0; c < GridSize; c++)
        {
            int idx = r * GridSize + c;
            var cell = ViewModel.GridCells[idx];

            if (cell.IsCenter)
            {
                AppGrid.Children.Add(CreateDragHandle());
                continue;
            }

            // System slots (search / settings) — placement is now config-driven.
            if (cell.IsSystem)
            {
                Border systemBtn = cell.SystemAction switch
                {
                    SystemAction.Search => CreateSearchToggle(cell),
                    SystemAction.Settings => CreateSettingsButton(cell),
                    _ => CreateCellButton(cell),
                };
                _cellBorders[idx] = systemBtn;
                AppGrid.Children.Add(systemBtn);
                continue;
            }

            var button = CreateCellButton(cell);
            _cellBorders[idx] = button;
            AppGrid.Children.Add(button);
        }
    }

    private Border CreateDragHandle()
    {
        // Three dots vertically as a "grip" hint (Segoe Fluent Icons \uE712 = MoreVertical)
        var grip = new Image
        {
            Source = new System.Windows.Media.Imaging.BitmapImage(
                new Uri("pack://application:,,,/Assets/muu-icon.png", UriKind.Absolute)),
            Width = 40,
            Height = 40,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
        };
        RenderOptions.SetBitmapScalingMode(grip, BitmapScalingMode.HighQuality);

        // Match the size and margin of regular cells so the hit area is identical.
        // Use alpha=1 instead of Brushes.Transparent to guarantee hit-testability.
        var handle = new Border
        {
            Width = CellSize,
            Height = CellSize,
            Margin = new Thickness(CellMargin),
            Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)),
            Cursor = Cursors.SizeAll,
            Child = grip,
        };

        handle.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                // Win32 title-bar drag emulation
                var hwnd = new WindowInteropHelper(this).Handle;
                NativeMethods.ReleaseCapture();
                NativeMethods.SendMessage(hwnd, NativeMethods.WM_NCLBUTTONDOWN,
                    (IntPtr)NativeMethods.HTCAPTION, IntPtr.Zero);
                e.Handled = true;
            }
        };

        return handle;
    }

    private Border CreateSearchToggle(GridCellViewModel cell) =>
        CreateSystemButton(cell, "", "検索", ToggleSearch);

    private Border CreateSettingsButton(GridCellViewModel cell) =>
        CreateSystemButton(cell, "", "設定", OpenSettings);

    private Border CreateSystemButton(
        GridCellViewModel cell, string glyph, string tooltip, Action onClick)
    {
        var icon = new TextBlock
        {
            Text = glyph,
            FontFamily = new FontFamily("Segoe Fluent Icons"),
            FontSize = 22,
            Foreground = (SolidColorBrush)FindResource("PrimaryTextBrush"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
        };

        var border = new Border
        {
            Width = CellSize,
            Height = CellSize,
            Margin = new Thickness(CellMargin),
            CornerRadius = new CornerRadius(8),
            Background = (SolidColorBrush)FindResource("CellBackgroundBrush"),
            BorderBrush = (SolidColorBrush)FindResource("SubtleBorderBrush"),
            BorderThickness = new Thickness(0.5),
            Child = icon,
            Cursor = Cursors.Hand,
            ToolTip = tooltip,
            Effect = new DropShadowEffect
            {
                BlurRadius = 10,
                ShadowDepth = 2,
                Direction = 270,
                Opacity = 0.35,
                Color = Colors.Black,
            },
        };

        var normalBg = (SolidColorBrush)FindResource("CellBackgroundBrush");
        var hoverBg = (SolidColorBrush)FindResource("HoverItemBrush");
        border.MouseEnter += (_, _) => border.Background = hoverBg;
        border.MouseLeave += (_, _) => border.Background = normalBg;

        border.MouseLeftButtonUp += (_, _) => onClick();
        // Right-click opens slot settings so users can re-assign system slots.
        border.MouseRightButtonUp += (_, e) =>
        {
            e.Handled = true;
            OpenCellSettings(cell);
        };

        return border;
    }

    private void ToggleSearch()
    {
        if (SearchPanel.Visibility == Visibility.Visible)
        {
            SearchPanel.Visibility = Visibility.Collapsed;
            ViewModel.ClearSearch();
        }
        else
        {
            SearchPanel.Visibility = Visibility.Visible;
            QueryBox.Focus();
            QueryBox.SelectAll();
        }
    }

    public void OpenSettings()
    {
        var dlg = new SettingsWindow();
        if (IsVisible) dlg.Owner = this;
        dlg.ShowDialog();
    }

    private Border CreateCellButton(GridCellViewModel cell)
    {
        // Icon-only display (no label) so the icon can fill more of the cell.
        FrameworkElement content;
        if (cell.HasItem)
        {
            var icon = new Image
            {
                Source = cell.Icon,
                Width = 32,
                Height = 32,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                ToolTip = cell.Name,
            };
            RenderOptions.SetBitmapScalingMode(icon, BitmapScalingMode.HighQuality);
            content = icon;
        }
        else
        {
            // Empty slot: keep the subtle "+" hint
            content = new TextBlock
            {
                Text = "+",
                FontSize = 18,
                FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
                Foreground = (SolidColorBrush)FindResource("TertiaryTextBrush"),
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
        }

        var cellBg = (SolidColorBrush)FindResource("CellBackgroundBrush");
        var hoverBg = (SolidColorBrush)FindResource("HoverItemBrush");
        var subtleBorder = (SolidColorBrush)FindResource("SubtleBorderBrush");
        // alpha=1 invisible brush keeps empty cells hit-testable while looking transparent
        var invisibleBg = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));

        var shadow = new DropShadowEffect
        {
            BlurRadius = 10,
            ShadowDepth = 2,
            Direction = 270,
            Opacity = 0.35,
            Color = Colors.Black,
        };

        var border = new Border
        {
            Width = CellSize,
            Height = CellSize,
            Margin = new Thickness(CellMargin),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(0.5),
            Child = content,
            Cursor = Cursors.Hand,
        };

        if (cell.HasItem)
        {
            // Registered cells always look like a card.
            border.Background = cellBg;
            border.BorderBrush = subtleBorder;
            border.Effect = shadow;
            border.MouseEnter += (_, _) => border.Background = hoverBg;
            border.MouseLeave += (_, _) => border.Background = cellBg;
        }
        else
        {
            // Empty cells are invisible by default; on hover they reveal
            // the same card chrome (white background + border + shadow + "+").
            border.Background = invisibleBg;
            border.BorderBrush = Brushes.Transparent;
            content.Visibility = Visibility.Collapsed;

            border.MouseEnter += (_, _) =>
            {
                border.Background = hoverBg;
                border.BorderBrush = subtleBorder;
                border.Effect = shadow;
                content.Visibility = Visibility.Visible;
            };
            border.MouseLeave += (_, _) =>
            {
                border.Background = invisibleBg;
                border.BorderBrush = Brushes.Transparent;
                border.Effect = null;
                content.Visibility = Visibility.Collapsed;
            };
        }

        border.MouseLeftButtonUp += (_, _) =>
        {
            if (cell.HasItem)
            {
                cell.Launch();
                HideWindow();
            }
            else
            {
                OpenCellSettings(cell);
            }
        };

        border.MouseRightButtonUp += (_, e) =>
        {
            e.Handled = true;
            OpenCellSettings(cell);
        };

        return border;
    }

    private void OpenCellSettings(GridCellViewModel cell)
    {
        var existing = cell.HasItem ? cell.ToGridItem() : null;
        var dlg = new GridItemSettingsWindow(cell.Row, cell.Column, existing)
        {
            Owner = this,
        };

        if (dlg.ShowDialog() == true && dlg.Result is not null)
        {
            ViewModel.UpdateGridItem(cell.Row, cell.Column, dlg.Result);
            BuildGrid();
        }
    }

    // ─── Grid focus / keyboard nav ───────────────────────────

    private void SetFocusedCell(int index)
    {
        // Clear previous focus
        if (_focusedCellIndex >= 0 && _cellBorders[_focusedCellIndex] is { } prev)
        {
            prev.BorderBrush = DefaultBorderBrushFor(_focusedCellIndex);
            prev.BorderThickness = new Thickness(0.5);
        }

        _focusedCellIndex = index;

        if (index >= 0 && _cellBorders[index] is { } next)
        {
            next.BorderBrush = SystemParameters.WindowGlassBrush ?? Brushes.DodgerBlue;
            next.BorderThickness = new Thickness(2);
        }
    }

    private Brush DefaultBorderBrushFor(int index)
    {
        // Empty user-cells render with a transparent border; system slots
        // (search/settings) and registered cells use the subtle border.
        if (index >= 0 && index < ViewModel.GridCells.Length
            && !ViewModel.GridCells[index].HasItem)
        {
            return Brushes.Transparent;
        }
        return (SolidColorBrush)FindResource("SubtleBorderBrush");
    }

    private static int Wrap(int v, int max)
    {
        v %= max;
        return v < 0 ? v + max : v;
    }

    private void MoveGridFocus(int dRow, int dCol)
    {
        // Pick a starting position if nothing is focused yet
        int row, col;
        if (_focusedCellIndex < 0)
        {
            row = 2; col = 2;
        }
        else
        {
            row = _focusedCellIndex / GridSize;
            col = _focusedCellIndex % GridSize;
        }

        // Step until we land on a non-center cell
        for (int safety = 0; safety < GridSize * GridSize; safety++)
        {
            row = Wrap(row + dRow, GridSize);
            col = Wrap(col + dCol, GridSize);
            int idx = row * GridSize + col;
            if (idx == CenterIndex) continue;
            SetFocusedCell(idx);
            return;
        }
    }

    private void TabGridFocus(int direction)
    {
        int start = _focusedCellIndex < 0 ? -direction : _focusedCellIndex;
        int total = GridSize * GridSize;
        for (int safety = 0; safety < total; safety++)
        {
            start = Wrap(start + direction, total);
            if (start == CenterIndex) continue;
            SetFocusedCell(start);
            return;
        }
    }

    private void ActivateFocusedCell()
    {
        if (_focusedCellIndex < 0) return;

        var cell = ViewModel.GridCells[_focusedCellIndex];

        // System slot: dispatch to the corresponding action
        if (cell.IsSystem)
        {
            switch (cell.SystemAction)
            {
                case SystemAction.Search: ToggleSearch(); return;
                case SystemAction.Settings: OpenSettings(); return;
            }
        }

        if (cell.HasItem)
        {
            cell.Launch();
            HideWindow();
        }
        else
        {
            OpenCellSettings(cell);
        }
    }

    // ─── Window Events ───────────────────────────────────────

    private void Window_SourceInitialized(object sender, EventArgs e) { }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        HideWindow();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Escape: close search panel if visible, otherwise hide window
        if (e.Key == Key.Escape)
        {
            if (SearchPanel.Visibility == Visibility.Visible)
            {
                SearchPanel.Visibility = Visibility.Collapsed;
                ViewModel.ClearSearch();
            }
            else
            {
                HideWindow();
            }
            e.Handled = true;
            return;
        }

        bool searching = SearchPanel.Visibility == Visibility.Visible;

        if (searching)
        {
            // Navigate the search results list
            switch (e.Key)
            {
                case Key.Down:
                    ViewModel.MoveSelection(1);
                    e.Handled = true;
                    break;
                case Key.Up:
                    ViewModel.MoveSelection(-1);
                    e.Handled = true;
                    break;
                case Key.Enter:
                    ViewModel.ExecuteSelectedCommand.Execute(null);
                    HideWindow();
                    e.Handled = true;
                    break;
                case Key.Tab:
                    ViewModel.MoveSelection(1);
                    e.Handled = true;
                    break;
            }
            return;
        }

        // No search active → navigate the 5x5 grid
        switch (e.Key)
        {
            case Key.Up:
                MoveGridFocus(-1, 0);
                e.Handled = true;
                break;
            case Key.Down:
                MoveGridFocus(1, 0);
                e.Handled = true;
                break;
            case Key.Left:
                MoveGridFocus(0, -1);
                e.Handled = true;
                break;
            case Key.Right:
                MoveGridFocus(0, 1);
                e.Handled = true;
                break;
            case Key.Tab:
                TabGridFocus((e.KeyboardDevice.Modifiers & ModifierKeys.Shift) != 0 ? -1 : 1);
                e.Handled = true;
                break;
            case Key.Enter:
                ActivateFocusedCell();
                e.Handled = true;
                break;
        }
    }

    public void ShowWindow()
    {
        ViewModel.ClearSearch();
        // Search panel is hidden by default each time the launcher appears
        SearchPanel.Visibility = Visibility.Collapsed;
        PositionAtCursor();
        Show();
        Activate();
        // Give the window itself focus so global key handler runs;
        // QueryBox will get focus only when the search panel is opened.
        Focus();

        BeginAnimation(OpacityProperty, new DoubleAnimation
        {
            From = 0, To = 1,
            Duration = TimeSpan.FromMilliseconds(150),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
    }

    public void HideWindow()
    {
        var anim = new DoubleAnimation
        {
            From = 1, To = 0,
            Duration = TimeSpan.FromMilliseconds(100),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        anim.Completed += (_, _) => Hide();
        BeginAnimation(OpacityProperty, anim);
    }

    private void PositionAtCursor()
    {
        if (!NativeMethods.GetCursorPos(out var pt))
            return;

        double totalCellWithMargin = CellSize + CellMargin * 2;
        double centerOffsetX = totalCellWithMargin * 2.5;
        double centerOffsetY = totalCellWithMargin * 2.5;
        double paddingOffset = 10;
        centerOffsetX += paddingOffset;
        centerOffsetY += paddingOffset;

        var source = PresentationSource.FromVisual(this);
        double dpiScaleX = source?.CompositionTarget?.TransformFromDevice.M11 ?? 1.0;
        double dpiScaleY = source?.CompositionTarget?.TransformFromDevice.M22 ?? 1.0;

        Left = pt.X * dpiScaleX - centerOffsetX;
        Top = pt.Y * dpiScaleY - centerOffsetY;
    }
}
