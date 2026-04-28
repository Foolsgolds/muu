namespace Muu.Models;

/// <summary>
/// Built-in launcher actions that can be assigned to a grid slot
/// when its <see cref="GridItem.Kind"/> is <see cref="GridItemKind.System"/>.
/// </summary>
public enum SystemAction
{
    None,
    /// <summary>Toggles the search panel (magnifying glass icon).</summary>
    Search,
    /// <summary>Opens the application settings dialog (gear icon).</summary>
    Settings,
}
