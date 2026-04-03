using System.Windows.Interop;

namespace Muu.Interop;

public sealed class HotkeyManager : IDisposable
{
    private const int HotkeyId = 9000;
    private IntPtr _hwnd;
    private HwndSource? _source;

    public event Action? HotkeyPressed;

    public void Register(IntPtr hwnd)
    {
        _hwnd = hwnd;
        _source = HwndSource.FromHwnd(hwnd);
        _source?.AddHook(WndProc);

        bool ok = NativeMethods.RegisterHotKey(
            hwnd, HotkeyId,
            NativeMethods.MOD_WIN | NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT,
            NativeMethods.VK_M);

        if (!ok)
            System.Diagnostics.Debug.WriteLine("Failed to register hotkey Win+Ctrl+Alt+M");
    }

    public void Unregister()
    {
        NativeMethods.UnregisterHotKey(_hwnd, HotkeyId);
        _source?.RemoveHook(WndProc);
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
