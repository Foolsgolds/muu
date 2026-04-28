using System.Windows.Interop;

namespace Muu.Interop;

public sealed class HotkeyManager : IDisposable
{
    private const int HotkeyId = 9000;
    private IntPtr _hwnd;
    private HwndSource? _source;
    private bool _registered;

    public event Action? HotkeyPressed;

    /// <summary>
    /// Register the global hotkey. <paramref name="modifiers"/> already
    /// combines Alt/Ctrl/Shift/Win flags (MOD_NOREPEAT is added automatically).
    /// </summary>
    public bool Register(IntPtr hwnd, uint modifiers, uint vk)
    {
        _hwnd = hwnd;
        if (_source is null)
        {
            _source = HwndSource.FromHwnd(hwnd);
            _source?.AddHook(WndProc);
        }

        if (_registered)
        {
            NativeMethods.UnregisterHotKey(_hwnd, HotkeyId);
            _registered = false;
        }

        bool ok = NativeMethods.RegisterHotKey(
            hwnd, HotkeyId,
            modifiers | NativeMethods.MOD_NOREPEAT,
            vk);

        _registered = ok;
        if (!ok)
            System.Diagnostics.Debug.WriteLine($"Failed to register hotkey (modifiers=0x{modifiers:X}, vk=0x{vk:X})");
        return ok;
    }

    /// <summary>Re-register with a new hotkey combination (used by settings UI).</summary>
    public bool Reregister(uint modifiers, uint vk) => Register(_hwnd, modifiers, vk);

    public void Unregister()
    {
        if (_registered)
        {
            NativeMethods.UnregisterHotKey(_hwnd, HotkeyId);
            _registered = false;
        }
        _source?.RemoveHook(WndProc);
        _source = null;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            HotkeyPressed?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose() => Unregister();
}
