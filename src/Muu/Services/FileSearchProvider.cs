using System.Data.OleDb;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Muu.Models;

namespace Muu.Services;

public sealed class FileSearchProvider : ISearchProvider
{
    public bool CanHandle(string query) =>
        !query.StartsWith('=') && query.Length >= 3;

    public Task<IReadOnlyList<SearchResult>> SearchAsync(string query, CancellationToken ct)
    {
        var results = new List<SearchResult>();

        try
        {
            string escaped = query.Replace("'", "''");
            string sql = $"""
                SELECT TOP 6 System.ItemPathDisplay, System.FileName
                FROM SystemIndex
                WHERE SCOPE = 'file:'
                AND System.FileName LIKE '%{escaped}%'
                """;

            using var conn = new OleDbConnection(
                "Provider=Search.CollatorDSO;Extended Properties='Application=Windows'");
            conn.Open();

            using var cmd = new OleDbCommand(sql, conn);
            using var reader = cmd.ExecuteReader();

            while (reader is not null && reader.Read())
            {
                string path = reader.GetString(0);
                string fileName = reader.GetString(1);

                ImageSource? icon = null;
                try
                {
                    if (System.IO.File.Exists(path))
                    {
                        using var ico = System.Drawing.Icon.ExtractAssociatedIcon(path);
                        if (ico is not null)
                        {
                            icon = Imaging.CreateBitmapSourceFromHIcon(
                                ico.Handle, Int32Rect.Empty,
                                BitmapSizeOptions.FromEmptyOptions());
                            icon.Freeze();
                        }
                    }
                }
                catch { }

                results.Add(new SearchResult
                {
                    Title = fileName,
                    Subtitle = path,
                    Kind = SearchResultKind.File,
                    Icon = icon,
                    Score = FuzzyMatcher.Score(query, fileName) * 0.8,
                    Execute = () => Process.Start(new ProcessStartInfo
                    {
                        FileName = path,
                        UseShellExecute = true,
                    })
                });
            }
        }
        catch
        {
            // Windows Search might not be available
        }

        return Task.FromResult<IReadOnlyList<SearchResult>>(results);
    }
}
