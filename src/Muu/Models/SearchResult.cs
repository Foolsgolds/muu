using System.Windows.Media;

namespace Muu.Models;

public sealed class SearchResult
{
    public required string Title { get; init; }
    public string Subtitle { get; init; } = string.Empty;
    public SearchResultKind Kind { get; init; }
    public ImageSource? Icon { get; init; }
    public double Score { get; init; }
    public required Action Execute { get; init; }
}
