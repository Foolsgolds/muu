using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using Muu.Interop;
using Muu.ViewModels;

namespace Muu.Views;

public partial class LauncherWindow : Window
{
    private const int GridSize = 5;
    private const double CellSize = 48;
    private const double CellMargin = 4;

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

        for (int r = 0; r < GridSize; r++)
        for (int c = 0; c < GridSize; c++)
        {
            var cell = ViewModel.GridCells[r * GridSize + c];

            if (cell.IsCenter)
            {
                AppGrid.Children.Add(CreateDragHandle());
                continue;
            }

            var button = CreateCellButton(cell);
            AppGrid.Children.Add(button);
        }
    }

    private Border CreateDragHandle()
    {
        // Three dots vertically as a "grip" hint (Segoe Fluent Icons \uE712 = MoreVertical)
        var grip = new TextBlock
        {
            Text = "\uE712",
            FontFamily = new FontFamily("Segoe Fluent Icons"),
            FontSize = 22,
            Foreground = (SolidColorBrush)FindResource("TertiaryTextBrush"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
        };

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

    private Border CreateCellButton(GridCellViewModel cell)
    {
        var icon = new Image
        {
            Source = cell.Icon,
            Width = 22,
            Height = 22,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        RenderOptions.SetBitmapScalingMode(icon, BitmapScalingMode.HighQuality);

        var label = new TextBlock
        {
            Text = cell.Name,
            FontSize = 8,
            FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
            Foreground = (SolidColorBrush)FindResource("PrimaryTextBrush"),
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(2, 2, 2, 0),
            MaxWidth = CellSize - 8,
        };

        var stack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        stack.Children.Add(icon);
        stack.Children.Add(label);

        if (!cell.HasItem)
        {
            icon.Visibility = Visibility.Collapsed;
            label.Text = "+";
            label.FontSize = 14;
            label.Foreground = (SolidColorBrush)FindResource("TertiaryTextBrush");
            label.Margin = new Thickness(0);
        }

        var border = new Border
        {
            Width = CellSize,
            Height = CellSize,
            Margin = new Thickness(CellMargin),
            CornerRadius = new CornerRadius(8),
            Background = (SolidColorBrush)FindResource("CellBackgroundBrush"),
            BorderBrush = (SolidColorBrush)FindResource("SubtleBorderBrush"),
            BorderThickness = new Thickness(0.5),
            Child = stack,
            Cursor = Cursors.Hand,
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

    // ─── Window Events ───────────────────────────────────────

    private void Window_SourceInitialized(object sender, EventArgs e) { }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        HideWindow();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                HideWindow();
                e.Handled = true;
                break;
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
    }

    public void ShowWindow()
    {
        ViewModel.ClearSearch();
        PositionAtCursor();
        Show();
        Activate();
        QueryBox.Focus();

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
