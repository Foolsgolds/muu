using System.Data;
using System.Text.RegularExpressions;
using System.Windows;
using Muu.Models;

namespace Muu.Services;

public sealed partial class CalculatorProvider : ISearchProvider
{
    [GeneratedRegex(@"^=?[\d\s\+\-\*\/\.\(\)\%]+$")]
    private static partial Regex MathPattern();

    public bool CanHandle(string query)
    {
        if (query.StartsWith('=')) return true;
        return MathPattern().IsMatch(query.Trim());
    }

    public Task<IReadOnlyList<SearchResult>> SearchAsync(string query, CancellationToken ct)
    {
        string expr = query.TrimStart('=').Trim();
        if (string.IsNullOrWhiteSpace(expr))
            return Task.FromResult<IReadOnlyList<SearchResult>>([]);

        try
        {
            var table = new DataTable();
            var value = table.Compute(expr, null);
            string answer = Convert.ToDouble(value).ToString("G15");

            var result = new SearchResult
            {
                Title = $"= {answer}",
                Subtitle = $"{expr} をコピー",
                Kind = SearchResultKind.Calculator,
                Score = 1.0,
                Execute = () => Clipboard.SetText(answer),
            };

            return Task.FromResult<IReadOnlyList<SearchResult>>([result]);
        }
        catch
        {
            return Task.FromResult<IReadOnlyList<SearchResult>>([]);
        }
    }
}
