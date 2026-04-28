using System.Text;
using System.Windows.Input;
using Muu.Models;

namespace Muu.Infrastructure;

/// <summary>
/// Format a hotkey combination as a human-readable string
/// (e.g. "Win + Ctrl + Alt + M") and convert WPF Key values to
/// Win32 virtual-key codes.
/// </summary>
public static class HotkeyDisplay
{
    public static string Format(AppSettings s) => Format(s.HotkeyModifiers, s.HotkeyVirtualKey);

    public static string Format(HotkeyModifiers mods, uint vk)
    {
        var parts = new List<string>();
        if (mods.HasFlag(HotkeyModifiers.Win)) parts.Add("Win");
        if (mods.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
        if (mods.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (mods.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");

        string keyName = VkToName(vk);
        if (!string.IsNullOrEmpty(keyName)) parts.Add(keyName);

        return string.Join(" + ", parts);
    }

    public static HotkeyModifiers FromWpfModifiers(ModifierKeys mods)
    {
        var result = HotkeyModifiers.None;
        if ((mods & ModifierKeys.Alt) != 0) result |= HotkeyModifiers.Alt;
        if ((mods & ModifierKeys.Control) != 0) result |= HotkeyModifiers.Control;
        if ((mods & ModifierKeys.Shift) != 0) result |= HotkeyModifiers.Shift;
        if ((mods & ModifierKeys.Windows) != 0) result |= HotkeyModifiers.Win;
        return result;
    }

    public static uint KeyToVk(Key key) => (uint)KeyInterop.VirtualKeyFromKey(key);

    public static bool IsModifierKey(Key key) =>
        key is Key.LeftCtrl or Key.RightCtrl
        or Key.LeftAlt or Key.RightAlt or Key.System
        or Key.LeftShift or Key.RightShift
        or Key.LWin or Key.RWin
        or Key.None;

    private static string VkToName(uint vk)
    {
        // Letters A-Z and digits 0-9 use ASCII directly
        if (vk >= 0x30 && vk <= 0x39) return ((char)vk).ToString();        // 0-9
        if (vk >= 0x41 && vk <= 0x5A) return ((char)vk).ToString();        // A-Z
        if (vk >= 0x70 && vk <= 0x87) return $"F{vk - 0x6F}";              // F1-F24

        return vk switch
        {
            0x20 => "Space",
            0x09 => "Tab",
            0x0D => "Enter",
            0x1B => "Esc",
            0x08 => "Backspace",
            0x2E => "Delete",
            0x2D => "Insert",
            0x24 => "Home",
            0x23 => "End",
            0x21 => "PageUp",
            0x22 => "PageDown",
            0x25 => "Left",
            0x26 => "Up",
            0x27 => "Right",
            0x28 => "Down",
            0xBA => ";",
            0xBB => "=",
            0xBC => ",",
            0xBD => "-",
            0xBE => ".",
            0xBF => "/",
            0xC0 => "`",
            0xDB => "[",
            0xDC => "\\",
            0xDD => "]",
            0xDE => "'",
            _ => $"VK_{vk:X2}",
        };
    }
}
