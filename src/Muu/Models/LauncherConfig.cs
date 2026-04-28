using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

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
        Converters = { new JsonStringEnumConverter() },
    };

    public static LauncherConfig Load()
    {
        LauncherConfig config = new();
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                config = JsonSerializer.Deserialize<LauncherConfig>(json, JsonOptions) ?? new();
            }
        }
        catch { }

        EnsureSystemDefaults(config);
        return config;
    }

    /// <summary>
    /// Make sure the launcher always has a Search and a Settings slot, so
    /// fresh installs (and old configs that pre-date system slots) start
    /// with a usable layout. Defaults: Search at [4,0], Settings at [4,4].
    /// </summary>
    private static void EnsureSystemDefaults(LauncherConfig config)
    {
        bool hasSearch = config.Items.Any(i =>
            i.Kind == GridItemKind.System && i.SystemAction == SystemAction.Search);
        bool hasSettings = config.Items.Any(i =>
            i.Kind == GridItemKind.System && i.SystemAction == SystemAction.Settings);

        if (!hasSearch && config.GetItem(4, 0) is null)
        {
            config.Items.Add(new GridItem
            {
                Row = 4, Column = 0,
                Name = "検索",
                Kind = GridItemKind.System,
                SystemAction = SystemAction.Search,
            });
        }

        if (!hasSettings && config.GetItem(4, 4) is null)
        {
            config.Items.Add(new GridItem
            {
                Row = 4, Column = 4,
                Name = "設定",
                Kind = GridItemKind.System,
                SystemAction = SystemAction.Settings,
            });
        }
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
        // System slots have no TargetPath but should still be persisted.
        bool isSystem = item.Kind == GridItemKind.System
                        && item.SystemAction != SystemAction.None;
        if (isSystem || !string.IsNullOrWhiteSpace(item.TargetPath))
            Items.Add(item);
    }
}
