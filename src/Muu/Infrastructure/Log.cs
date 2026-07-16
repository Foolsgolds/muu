using System.IO;

namespace Muu.Infrastructure;

/// <summary>
/// Opt-in diagnostic logger. Disabled by default; enabled via the
/// "デバッグログを出力する" checkbox in the settings dialog
/// (persisted as AppSettings.DebugLogging). Writes to %TEMP%\muu.log.
/// </summary>
public static class Log
{
    private static readonly string LogPath =
        Path.Combine(Path.GetTempPath(), "muu.log");

    private static readonly object _sync = new();

    /// <summary>Runtime switch, mirrored from AppSettings.DebugLogging.</summary>
    public static bool Enabled { get; set; }

    public static string FilePath => LogPath;

    public static void Write(string message)
    {
        if (!Enabled) return;
        try
        {
            lock (_sync)
            {
                File.AppendAllText(LogPath,
                    $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}");
            }
        }
        catch { }
    }

    /// <summary>Start a fresh log (called once per app run when enabled).</summary>
    public static void StartSession(string header)
    {
        if (!Enabled) return;
        try
        {
            lock (_sync)
            {
                File.Delete(LogPath);
            }
        }
        catch { }
        Write(header);
    }
}
