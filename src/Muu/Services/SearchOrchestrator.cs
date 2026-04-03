using Muu.Models;

namespace Muu.Services;

public sealed class SearchOrchestrator
{
    private readonly IReadOnlyList<ISearchProvider> _providers;
    private CancellationTokenSource? _cts;
    private const int MaxResults = 8;

    public SearchOrchestrator(IEnumerable<ISearchProvider> providers)
    {
        _providers = providers.ToList();
    }

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(string query)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        if (string.IsNullOrWhiteSpace(query))
            return [];

        // Small debounce
        await Task.Delay(80, token);

        var tasks = _providers
            .Where(p => p.CanHandle(query))
            .Select(p => p.SearchAsync(query, token));

        var resultSets = await Task.WhenAll(tasks);

        return resultSets
            .SelectMany(r => r)
            .OrderByDescending(r => r.Score)
            .Take(MaxResults)
            .ToList();
    }
}
