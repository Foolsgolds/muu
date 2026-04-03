using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Muu.Interop;
using Muu.ViewModels;

namespace Muu.Views;

public partial class LauncherWindow : Window
{
    private const int GridSize = 5;
    private const double CellSize = 48;
    private const double CellMargin = 2;

    private readonly DispatcherTimer _fogTimer;
    private readonly FogLayer[] _fogLayers;
    private double _time;

    private LauncherViewModel ViewModel => (LauncherViewModel)DataContext;

    public LauncherWindow()
    {
        InitializeComponent();

        // Define fog layers
        _fogLayers =
        [
            new(-60, -30, 280, 240, "#BBFFFFFF",  0.06,  0.04,   70, -40),
            new( 40,  80, 260, 220, "#BBD8E0EC",  0.04,  0.05,  -50,  45),
            new( 10, 150, 220, 180, "#BBEAE6E2",  0.05,  0.06,  -60, -35),
            new(120, -20, 240, 200, "#BBFFFFFF",  0.035, 0.03,   40,  50),
            new(-30,  60, 300, 200, "#BBCCD6E4",  0.03,  0.045, -45, -30),
        ];

        // Create fog ellipses on canvas
        Loaded += (_, _) =>
        {
            BuildGrid();
            BuildFogLayers();
        };

        // Timer for fog animation (~30fps)
        _fogTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(33),
        };
        _fogTimer.Tick += FogTimer_Tick;
        _fogTimer.Start();
    }

    // ─── Fog ─────────────────────────────────────────────────

    private sealed class FogLayer
    {
        public Ellipse Ellipse = null!;
        public double BaseX, BaseY;
        public double SpeedX, SpeedY;
        public double RangeX, RangeY;

        public FogLayer(double x, double y, double w, double h, string color,
                        double speedX, double speedY, double rangeX, double rangeY)
        {
            BaseX = x; BaseY = y;
            SpeedX = speedX; SpeedY = speedY;
            RangeX = rangeX; RangeY = rangeY;

            Ellipse = new Ellipse
            {
                Width = w,
                Height = h,
                IsHitTestVisible = false,
                Fill = new RadialGradientBrush
                {
                    GradientStops =
                    {
                        new GradientStop((Color)ColorConverter.ConvertFromString(color), 0),
                        new GradientStop(Colors.Transparent, 1),
                    }
                }
            };
        }
    }

    private void BuildFogLayers()
    {
        FogCanvas.Children.Clear();
        foreach (var layer in _fogLayers)
        {
            Canvas.SetLeft(layer.Ellipse, layer.BaseX);
            Canvas.SetTop(layer.Ellipse, layer.BaseY);
            FogCanvas.Children.Add(layer.Ellipse);
        }
    }

    private void FogTimer_Tick(object? sender, EventArgs e)
    {
        if (!IsVisible) return;

        _time += 0.033;

        for (int i = 0; i < _fogLayers.Length; i++)
        {
            var layer = _fogLayers[i];
            double offsetX = Math.Sin(_time * layer.SpeedX * 2 * Math.PI) * layer.RangeX;
            double offsetY = Math.Cos(_time * layer.SpeedY * 2 * Math.PI + i) * layer.RangeY;
            double opacity = 0.35 + 0.2 * Math.Sin(_time * 0.15 + i * 1.3);

            Canvas.SetLeft(layer.Ellipse, layer.BaseX + offsetX);
            Canvas.SetTop(layer.Ellipse, layer.BaseY + offsetY);
            layer.Ellipse.Opacity = opacity;
        }
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
                var placeholder = new Border
                {
                    Width = CellSize,
                    Height = CellSize,
                    Margin = new Thickness(CellMargin),
                    Background = Brushes.Transparent,
                };
                AppGrid.Children.Add(placeholder);
                continue;
            }

            var button = CreateCellButton(cell);
            AppGrid.Children.Add(button);
        }
    }

    private Border CreateCellButton(GridCellViewModel cell)
    {
        var icon = new Image
        {
            Source = cell.Icon,
            Width = 20,
            Height = 20,
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
            BorderThickness = new Thickness(0.8),
            Child = stack,
            Cursor = Cursors.Hand,
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
