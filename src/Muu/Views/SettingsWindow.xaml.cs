using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using Muu.Infrastructure;
using Muu.Models;
using Muu.ViewModels;

namespace Muu.Views;

public partial class SettingsWindow : Window
{
    private const int CenterIndex = 12;
    private const double MiniCellSize = 40;
    private const string DragDataFormat = "Muu.GridCellIndex";

    private HotkeyModifiers _capturedMods;
    private uint _capturedVk;
    private bool _captured;

    private readonly LauncherViewModel? _launcherVm;
    private readonly Action? _onLayoutChanged;
    private readonly Border?[] _miniCells = new Border?[25];

    public SettingsWindow() : this(null, null) { }

    public SettingsWindow(LauncherViewModel? launcherVm, Action? onLayoutChanged)
    {
        InitializeComponent();

        _launcherVm = launcherVm;
        _onLayoutChanged = onLayoutChanged;

        var s = App.Instance.Settings;
        _capturedMods = s.HotkeyModifiers;
        _capturedVk = s.HotkeyVirtualKey;
        _captured = true;
        UpdateDisplay();

        AutoStartCheckBox.IsChecked = StartupRegistration.IsRegistered();
        DebugLogCheckBox.IsChecked = s.DebugLogging;

        VersionText.Text = $"Version {GetVersionString()}";

        Loaded += (_, _) =>
        {
            ThemeHelper.Apply(this);
            BuildMiniGrid();
        };
    }

    private static string GetVersionString()
    {
        var asm = System.Reflection.Assembly.GetExecutingAssembly();

        // Prefer the informational version (matches <Version> in the csproj);
        // strip any "+<commit>" build-metadata suffix the SDK may append.
        string? info = asm
            .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        if (!string.IsNullOrEmpty(info))
        {
            int plus = info.IndexOf('+');
            return plus > 0 ? info[..plus] : info;
        }

        return asm.GetName().Version?.ToString(3) ?? "-";
    }

    private void Hyperlink_RequestNavigate(object sender,
        System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true,
            });
        }
        catch { }
        e.Handled = true;
    }

    // ─── Mini grid (drag & drop layout editor) ───────────────

    private void BuildMiniGrid()
    {
        if (_launcherVm is null) return;

        MiniGrid.Children.Clear();
        for (int i = 0; i < _miniCells.Length; i++) _miniCells[i] = null;

        for (int idx = 0; idx < 25; idx++)
        {
            int row = idx / 5;
            int col = idx % 5;

            Border cellUi;
            if (idx == CenterIndex)
            {
                cellUi = CreateCenterPlaceholder();
            }
            else
            {
                cellUi = CreateMiniCell(idx, _launcherVm.GridCells[idx]);
            }

            cellUi.Width = MiniCellSize;
            cellUi.Height = MiniCellSize;
            cellUi.Margin = new Thickness(3);
            Grid.SetRow(cellUi, row);
            Grid.SetColumn(cellUi, col);
            MiniGrid.Children.Add(cellUi);
            _miniCells[idx] = cellUi;
        }
    }

    private Border CreateCenterPlaceholder()
    {
        var icon = new System.Windows.Controls.Image
        {
            Source = new System.Windows.Media.Imaging.BitmapImage(
                new Uri("pack://application:,,,/Assets/muu-icon.png", UriKind.Absolute)),
            Width = 26,
            Height = 26,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
            Opacity = 0.6,
        };

        return new Border
        {
            Background = (SolidColorBrush)FindResource("ListBg"),
            BorderBrush = (SolidColorBrush)FindResource("BorderBrush"),
            BorderThickness = new Thickness(0.5),
            CornerRadius = new CornerRadius(6),
            Child = icon,
            ToolTip = "中央セル (移動ハンドル) は変更できません",
        };
    }

    private Border CreateMiniCell(int idx, GridCellViewModel cell)
    {
        FrameworkElement content;
        if (cell.IsSystem)
        {
            string glyph = cell.SystemAction switch
            {
                SystemAction.Search => "",   // search
                SystemAction.Settings => "", // gear
                _ => "?",
            };
            content = new TextBlock
            {
                Text = glyph,
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                FontSize = 18,
                Foreground = (SolidColorBrush)FindResource("PrimaryFg"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false,
            };
        }
        else if (cell.HasItem && cell.Icon is not null)
        {
            var img = new System.Windows.Controls.Image
            {
                Source = cell.Icon,
                Width = 24, Height = 24,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false,
            };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
            content = img;
        }
        else
        {
            // Empty slot
            content = new TextBlock
            {
                Text = "+",
                FontSize = 14,
                Foreground = (SolidColorBrush)FindResource("SecondaryFg"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false,
            };
        }

        var border = new Border
        {
            Background = (SolidColorBrush)FindResource("ControlBg"),
            BorderBrush = (SolidColorBrush)FindResource("BorderBrush"),
            BorderThickness = new Thickness(0.5),
            CornerRadius = new CornerRadius(6),
            Child = content,
            Cursor = Cursors.Hand,
            AllowDrop = true,
            ToolTip = string.IsNullOrWhiteSpace(cell.Name) ? "(空き)" : cell.Name,
        };

        // Drag-source
        Point dragStart = default;
        bool dragArmed = false;

        border.PreviewMouseLeftButtonDown += (s, e) =>
        {
            dragStart = e.GetPosition(this);
            dragArmed = true;
        };

        border.PreviewMouseMove += (s, e) =>
        {
            if (!dragArmed || e.LeftButton != MouseButtonState.Pressed) return;

            var pos = e.GetPosition(this);
            if (Math.Abs(pos.X - dragStart.X) < SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(pos.Y - dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
                return;

            dragArmed = false;
            DragDrop.DoDragDrop(border, new DataObject(DragDataFormat, idx), DragDropEffects.Move);
        };

        border.PreviewMouseLeftButtonUp += (_, _) => dragArmed = false;

        // Drop-target
        border.DragOver += (_, e) =>
        {
            if (e.Data.GetDataPresent(DragDataFormat))
            {
                int srcIdx = (int)e.Data.GetData(DragDataFormat);
                e.Effects = (srcIdx == idx || srcIdx == CenterIndex || idx == CenterIndex)
                    ? DragDropEffects.None
                    : DragDropEffects.Move;
                border.BorderBrush = e.Effects == DragDropEffects.Move
                    ? (SystemParameters.WindowGlassBrush ?? Brushes.DodgerBlue)
                    : (SolidColorBrush)FindResource("BorderBrush");
                border.BorderThickness = e.Effects == DragDropEffects.Move
                    ? new Thickness(2)
                    : new Thickness(0.5);
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        };

        border.DragLeave += (_, _) =>
        {
            border.BorderBrush = (SolidColorBrush)FindResource("BorderBrush");
            border.BorderThickness = new Thickness(0.5);
        };

        border.Drop += (_, e) =>
        {
            border.BorderBrush = (SolidColorBrush)FindResource("BorderBrush");
            border.BorderThickness = new Thickness(0.5);

            if (!e.Data.GetDataPresent(DragDataFormat)) return;
            int srcIdx = (int)e.Data.GetData(DragDataFormat);
            if (srcIdx == idx || _launcherVm is null) return;

            _launcherVm.SwapCells(srcIdx, idx);
            _onLayoutChanged?.Invoke();
            BuildMiniGrid(); // refresh preview
            e.Handled = true;
        };

        return border;
    }

    // ─── Hotkey ──────────────────────────────────────────────

    private void UpdateDisplay()
    {
        HotkeyBox.Text = _captured
            ? HotkeyDisplay.Format(_capturedMods, _capturedVk)
            : "（キー組み合わせを押してください）";
    }

    private void HotkeyBox_GotFocus(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "修飾キー (Win / Ctrl / Alt / Shift) と通常キーを同時に押してください。";
    }

    private void HotkeyBox_LostFocus(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "";
    }

    private void HotkeyBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyboardDevice.Modifiers == ModifierKeys.None
            && (e.Key is Key.Tab or Key.Escape or Key.Enter))
        {
            return;
        }

        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (HotkeyDisplay.IsModifierKey(key))
        {
            var mods = HotkeyDisplay.FromWpfModifiers(e.KeyboardDevice.Modifiers);
            HotkeyBox.Text = HotkeyDisplay.Format(mods, 0).TrimEnd(' ', '+');
            return;
        }

        _capturedMods = HotkeyDisplay.FromWpfModifiers(e.KeyboardDevice.Modifiers);
        _capturedVk = HotkeyDisplay.KeyToVk(key);
        _captured = true;
        UpdateDisplay();
    }

    private void ResetDefault_Click(object sender, RoutedEventArgs e)
    {
        _capturedMods = HotkeyModifiers.Win | HotkeyModifiers.Control | HotkeyModifiers.Alt;
        _capturedVk = Interop.NativeMethods.VK_M;
        _captured = true;
        UpdateDisplay();
    }

    // ─── OK / Cancel ─────────────────────────────────────────

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        if (!_captured || _capturedVk == 0 || _capturedMods == HotkeyModifiers.None)
        {
            MessageBox.Show(this,
                "修飾キーと通常キーの両方を含む組み合わせを設定してください。",
                "Muu", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var s = App.Instance.Settings;
        var prevMods = s.HotkeyModifiers;
        var prevVk = s.HotkeyVirtualKey;

        s.HotkeyModifiers = _capturedMods;
        s.HotkeyVirtualKey = _capturedVk;

        if (!App.Instance.ReapplyHotkey())
        {
            s.HotkeyModifiers = prevMods;
            s.HotkeyVirtualKey = prevVk;
            App.Instance.ReapplyHotkey();
            MessageBox.Show(this,
                "ホットキーの登録に失敗しました。他のアプリと競合している可能性があります。",
                "Muu", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Debug logging: apply immediately and persist
        s.DebugLogging = DebugLogCheckBox.IsChecked == true;
        bool wasEnabled = Log.Enabled;
        Log.Enabled = s.DebugLogging;
        if (Log.Enabled && !wasEnabled)
            Log.StartSession("Debug logging enabled from settings");

        s.Save();

        if (AutoStartCheckBox.IsChecked == true)
            StartupRegistration.Register();
        else
            StartupRegistration.Unregister();

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
