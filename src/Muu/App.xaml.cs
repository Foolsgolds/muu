using System.Windows;
using System.Windows.Interop;
using Hardcodet.Wpf.TaskbarNotification;
using Muu.Infrastructure;
using Muu.Interop;
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
            _hotkeyManager.Register(hwnd);

            // No DWM backdrop - fully transparent window
        };

        // Show once to initialize, then hide
        _launcherWindow.Show();
        _launcherWindow.Hide();
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
            ToolTipText = "Muu Launcher (Win+Ctrl+Alt+M)",
        };

        var menu = new System.Windows.Controls.ContextMenu();

        var showItem = new System.Windows.Controls.MenuItem { Header = "表示 (_S)" };
        showItem.Click += (_, _) => _launcherWindow?.ShowWindow();
        menu.Items.Add(showItem);

        menu.Items.Add(new System.Windows.Controls.Separator());

        var exitItem = new System.Windows.Controls.MenuItem { Header = "終了 (_X)" };
        exitItem.Click += (_, _) =>
        {
            _trayIcon?.Dispose();
            Shutdown();
        };
        menu.Items.Add(exitItem);

        _trayIcon.ContextMenu = menu;
        _trayIcon.TrayMouseDoubleClick += (_, _) => _launcherWindow?.ShowWindow();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeyManager?.Dispose();
        _trayIcon?.Dispose();
        _guard?.Dispose();
        base.OnExit(e);
    }
}
