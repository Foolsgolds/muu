namespace Muu.Models;

public enum GridItemKind
{
    /// <summary>An arbitrary file (e.g. document, executable).</summary>
    File,

    /// <summary>A folder opened in Explorer.</summary>
    Folder,

    /// <summary>An installed application picked from the Start Menu list.</summary>
    App,

    /// <summary>
    /// A built-in system action (search toggle, settings, ...).
    /// The specific behaviour is selected via <see cref="GridItem.SystemAction"/>.
    /// </summary>
    System,
}
