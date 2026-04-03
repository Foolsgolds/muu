using System.IO;
using System.Text.Json;

namespace Muu.Models;

public sealed class LauncherConfig
{
    public List<GridItem> Items { get; set; } = [];

    private static readonly string ConfigDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Muu");
    private static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public static LauncherConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<LauncherConfig>(json, JsonOptions) ?? new();
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
            var json = JsonSerializer.Serialize(this, JsonOptions);
            File.WriteAllText(ConfigPath, json);
        }
        catch { }
    }

    public GridItem? GetItem(int row, int col) =>
        Items.FirstOrDefault(i => i.Row == row && i.Column == col);

    public void SetItem(GridItem item)
    {
        Items.RemoveAll(i => i.Row == item.Row && i.Column == item.Column);
        if (!string.IsNullOrWhiteSpace(item.TargetPath))
            Items.Add(item);
    }
}
