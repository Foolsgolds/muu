using System.Diagnostics;
using Muu.Models;

namespace Muu.Services;

public sealed class AppSearchProvider : ISearchProvider
{
    public bool CanHandle(string query) => !query.StartsWith('=');

    public Task<IReadOnlyList<SearchResult>> SearchAsync(string query, CancellationToken ct)
    {
        var apps = InstalledAppsService.GetApps();

        var results = apps
            .Select(app => (app, score: FuzzyMatcher.Score(query, app.Name)))
            .Where(x => x.score > 0)
            .OrderByDescending(x => x.score)
            .Take(8)
            .Select(x => new SearchResult
            {
                Title = x.app.Name,
                Subtitle = x.app.TargetPath,
                Kind = SearchResultKind.App,
                Icon = x.app.Icon,
                Score = x.score,
                Execute = () => Launch(x.app)
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<SearchResult>>(results);
    }

    private static void Launch(AppInfo app)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = app.TargetPath,
            Arguments = app.Arguments,
            UseShellExecute = true,
        });
    }
}
