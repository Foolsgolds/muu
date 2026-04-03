using Muu.Models;

namespace Muu.Services;

public interface ISearchProvider
{
    bool CanHandle(string query);
    Task<IReadOnlyList<SearchResult>> SearchAsync(string query, CancellationToken ct);
}
