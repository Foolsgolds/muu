using System.Windows;
using System.Windows.Interop;
using Hardcodet.Wpf.TaskbarNotification;
using Muu.Infrastructure;
using Muu.Interop;
using Muu.Models;
using Muu.Services;
using Muu.ViewModels;
using Muu.Views;

namespace Muu;

public partial class App : Application
{
    private SingleInstanceGuard? _guard;
    private HotkeyManager? _hotkeyManager;
    private LauncherWindow? _launcherWindow;
    private TaskbarIcon? _trayIcon;

    public AppSettings Settings { get; private set; } = new();

    public static App Instance => (App)Application.Current;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Single instance check
        _guard = new SingleInstanceGuard();
        if (!_guard.IsFirstInstance)
        {
            Shutdown();
            return;
        }

        // Load persisted settings
        Settings = AppSettings.Load();

        // Build search pipeline
        var providers = new ISearchProvider[]
        {
            new CalculatorProvider(),
            new AppSearchProvider(),
            new FileSearchProvider(),
            new WebSearchProvider(),
        };
        var orchestrator = new SearchOrchestrator(providers);
        var viewModel = new LauncherViewModel(orchestrator);

        // Create main window
        _launcherWindow = new LauncherWindow { DataContext = viewModel };

        // Setup tray icon
        SetupTrayIcon();

        // Register hotkey after window handle is available
        _launcherWindow.SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(_launcherWindow).Handle;
            _hotkeyManager = new HotkeyManager();
            _hotkeyManager.HotkeyPressed += ToggleLauncher;
            _hotkeyManager.Register(hwnd, (uint)Settings.HotkeyModifiers, Settings.HotkeyVirtualKey);
        };

        // Show once to initialize, then hide
        _launcherWindow.Show();
        _launcherWindow.Hide();
    }

    /// <summary>
    /// Re-register the global hotkey using the current AppSettings values.
    /// Returns false if RegisterHotKey failed (e.g. combination already in use).
    /// </summary>
    public bool ReapplyHotkey()
    {
        if (_hotkeyManager is null) return false;
        bool ok = _hotkeyManager.Reregister(
            (uint)Settings.HotkeyModifiers,
            Settings.HotkeyVirtualKey);

        if (_trayIcon is not null)
            _trayIcon.ToolTipText = $"Muu Launcher ({HotkeyDisplay.Format(Settings)})";

        return ok;
    }

    private void ToggleLauncher()
    {
        if (_launcherWindow is null) return;

        if (_launcherWindow.IsVisible)
            _launcherWindow.HideWindow();
        else
            _launcherWindow.ShowWindow();
    }

    private void SetupTrayIcon()
    {
        _trayIcon = new TaskbarIcon
        {
            ToolTipText = $"Muu Launcher ({HotkeyDisplay.Format(Settings)})",
            IconSource = new System.Windows.Media.Imaging.BitmapImage(
                new Uri("pack://application:,,,/Assets/muu-icon.ico", UriKind.Absolute)),
        };

        var menu = new System.Windows.Controls.ContextMenu();

        var showItem = new System.Windows.Controls.MenuItem { Header = "表示 (_S)" };
        showItem.Click += (_, _) => _launcherWindow?.ShowWindow();
        menu.Items.Add(showItem);

        var settingsItem = new System.Windows.Controls.MenuItem { Header = "設定 (_C)" };
        settingsItem.Click += (_, _) => _launcherWindow?.OpenSettings();
        menu.Items.Add(settingsItem);

        menu.Items.Add(new System.Windows.Controls.Separator());

        var exitItem = new System.Windows.Controls.MenuItem { Header = "終了 (_X)" };
        exitItem.Click += (_, _) =>
        {
            _trayIcon?.Dispose();
            Shutdown();
        };
        menu.Items.Add(exitItem);

        _trayIcon.ContextMenu = menu;
        // Single left-click on the tray icon opens the settings dialog
        // (matches the behaviour of the gear cell). The right-click menu
        // still exposes "表示" / "終了".
        _trayIcon.TrayLeftMouseUp += (_, _) => _launcherWindow?.OpenSettings();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeyManager?.Dispose();
        _trayIcon?.Dispose();
        _guard?.Dispose();
        base.OnExit(e);
    }
}
