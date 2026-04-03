using System.Diagnostics;
using Muu.Models;

namespace Muu.Services;

public sealed class WebSearchProvider : ISearchProvider
{
    public bool CanHandle(string query) => !string.IsNullOrWhiteSpace(query);

    public Task<IReadOnlyList<SearchResult>> SearchAsync(string query, CancellationToken ct)
    {
        var result = new SearchResult
        {
            Title = $"\"{query}\" を Web で検索",
            Subtitle = "Google 検索",
            Kind = SearchResultKind.Web,
            Score = 0.1,
            Execute = () => Process.Start(new ProcessStartInfo
            {
                FileName = $"https://www.google.com/search?q={Uri.EscapeDataString(query)}",
                UseShellExecute = true,
            })
        };

        return Task.FromResult<IReadOnlyList<SearchResult>>([result]);
    }
}
