using System.Windows;
using System.Windows.Input;
using Muu.Infrastructure;
using Muu.Models;

namespace Muu.Views;

public partial class SettingsWindow : Window
{
    private HotkeyModifiers _capturedMods;
    private uint _capturedVk;
    private bool _captured;

    public SettingsWindow()
    {
        InitializeComponent();

        var s = App.Instance.Settings;
        _capturedMods = s.HotkeyModifiers;
        _capturedVk = s.HotkeyVirtualKey;
        _captured = true;
        UpdateDisplay();

        // Reflect the current Run-key state in the auto-start checkbox.
        AutoStartCheckBox.IsChecked = StartupRegistration.IsRegistered();

        Loaded += (_, _) => ThemeHelper.Apply(this);
    }

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
        // Allow Tab/Esc/Enter to pass through for normal navigation when no modifiers held
        if (e.KeyboardDevice.Modifiers == ModifierKeys.None
            && (e.Key is Key.Tab or Key.Escape or Key.Enter))
        {
            return;
        }

        e.Handled = true;

        // Get the actual key (handle System key for Alt combinations)
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Modifier-only press: don't accept yet, but show partial state
        if (HotkeyDisplay.IsModifierKey(key))
        {
            var mods = HotkeyDisplay.FromWpfModifiers(e.KeyboardDevice.Modifiers);
            HotkeyBox.Text = HotkeyDisplay.Format(mods, 0).TrimEnd(' ', '+');
            return;
        }

        // Capture full combination
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

        // Apply, and roll back if registration fails (e.g. combination in use elsewhere)
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

        s.Save();

        // Apply the auto-start preference (registers/removes the HKCU Run entry).
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
