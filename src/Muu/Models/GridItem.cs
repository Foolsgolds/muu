using System.Text.Json.Serialization;

namespace Muu.Models;

public sealed class GridItem
{
    public int Row { get; set; }
    public int Column { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TargetPath { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string IconPath { get; set; } = string.Empty;

    /// <summary>
    /// What kind of registration this item represents. Defaults to File so
    /// existing config.json files (which lack this field) keep working.
    /// </summary>
    public GridItemKind Kind { get; set; } = GridItemKind.File;

    /// <summary>
    /// When <see cref="Kind"/> is <see cref="GridItemKind.System"/>, identifies
    /// which built-in action this slot represents. Otherwise <c>None</c>.
    /// </summary>
    public SystemAction SystemAction { get; set; } = SystemAction.None;

    [JsonIgnore]
    public bool IsCenter => Row == 2 && Column == 2; // 0-indexed center of 5x5
}
