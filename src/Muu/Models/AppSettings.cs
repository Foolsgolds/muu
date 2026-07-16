using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Muu.Interop;

namespace Muu.Models;

[Flags]
public enum HotkeyModifiers : uint
{
    None = 0,
    Alt = NativeMethods.MOD_ALT,         // 0x0001
    Control = NativeMethods.MOD_CONTROL, // 0x0002
    Shift = 0x0004,
    Win = NativeMethods.MOD_WIN,         // 0x0008
}

public sealed class AppSettings
{
    /// <summary>Modifier keys (Alt/Ctrl/Shift/Win) for the global hotkey.</summary>
    public HotkeyModifiers HotkeyModifiers { get; set; } =
        HotkeyModifiers.Win | HotkeyModifiers.Control | HotkeyModifiers.Alt;

    /// <summary>Win32 virtual key code for the global hotkey (default: 'M' = 0x4D).</summary>
    public uint HotkeyVirtualKey { get; set; } = NativeMethods.VK_M;

    /// <summary>
    /// When true, show/hide diagnostics are appended to %TEMP%\muu.log.
    /// Toggled from the settings dialog; off by default.
    /// </summary>
    public bool DebugLogging { get; set; }

    private static readonly string ConfigDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Muu");
    private static readonly string ConfigPath = Path.Combine(ConfigDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new();
            }
        }
        catch { }
        return new();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            File.WriteAllText(ConfigPath,
                JsonSerializer.Serialize(this, JsonOptions));
        }
        catch { }
    }
}
