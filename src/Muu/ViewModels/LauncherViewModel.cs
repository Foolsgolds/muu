using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Muu.Models;
using Muu.Services;

namespace Muu.ViewModels;

public partial class LauncherViewModel : ObservableObject
{
    private readonly SearchOrchestrator _orchestrator;
    private readonly LauncherConfig _config;

    [ObservableProperty]
    private string _queryText = string.Empty;

    [ObservableProperty]
    private int _selectedIndex;

    public ObservableCollection<SearchResult> Results { get; } = [];

    // 5x5 grid (25 cells)
    public GridCellViewModel[] GridCells { get; }

    public LauncherViewModel(SearchOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
        _config = LauncherConfig.Load();

        // Initialize 5x5 grid
        GridCells = new GridCellViewModel[25];
        for (int r = 0; r < 5; r++)
        for (int c = 0; c < 5; c++)
        {
            var cell = new GridCellViewModel(r, c);
            cell.LoadFrom(_config.GetItem(r, c));
            GridCells[r * 5 + c] = cell;
        }
    }

    partial void OnQueryTextChanged(string value)
    {
        _ = PerformSearchAsync(value);
    }

    private async Task PerformSearchAsync(string query)
    {
        try
        {
            var results = await _orchestrator.SearchAsync(query);
            Results.Clear();
            foreach (var r in results)
                Results.Add(r);
            SelectedIndex = Results.Count > 0 ? 0 : -1;
        }
        catch (OperationCanceledException)
        {
            // Debounce cancellation, expected
        }
    }

    [RelayCommand]
    private void ExecuteSelected()
    {
        if (SelectedIndex >= 0 && SelectedIndex < Results.Count)
        {
            Results[SelectedIndex].Execute();
        }
    }

    public void MoveSelection(int delta)
    {
        if (Results.Count == 0) return;
        int next = SelectedIndex + delta;
        if (next < 0) next = Results.Count - 1;
        else if (next >= Results.Count) next = 0;
        SelectedIndex = next;
    }

    public void ClearSearch()
    {
        QueryText = string.Empty;
        Results.Clear();
        SelectedIndex = -1;
    }

    public void UpdateGridItem(int row, int col, GridItem item)
    {
        _config.SetItem(item);
        _config.Save();
        var cell = GridCells[row * 5 + col];
        cell.LoadFrom(string.IsNullOrWhiteSpace(item.TargetPath) ? null : item);
    }
}
