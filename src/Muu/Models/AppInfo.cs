using System.Windows.Media;

namespace Muu.Models;

public sealed class AppInfo
{
    public required string Name { get; init; }
    public required string TargetPath { get; init; }
    public string Arguments { get; init; } = string.Empty;
    public ImageSource? Icon { get; init; }
}
