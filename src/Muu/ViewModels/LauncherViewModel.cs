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

        // A slot is "occupied" if it's a registered file/folder/app (TargetPath
        // present) OR a system slot (Search/Settings, no path).
        bool occupied = (item.Kind == GridItemKind.System
                         && item.SystemAction != SystemAction.None)
                        || !string.IsNullOrWhiteSpace(item.TargetPath);

        var cell = GridCells[row * 5 + col];
        cell.LoadFrom(occupied ? item : null);
    }

    /// <summary>
    /// Swap the contents of two grid slots (used by the settings dialog
    /// drag-and-drop). The center cell (drag handle) is not swappable.
    /// </summary>
    public void SwapCells(int idx1, int idx2)
    {
        if (idx1 == idx2) return;
        if (idx1 == 12 || idx2 == 12) return; // center is non-swappable
        if (idx1 < 0 || idx2 < 0 || idx1 >= 25 || idx2 >= 25) return;

        int row1 = idx1 / 5, col1 = idx1 % 5;
        int row2 = idx2 / 5, col2 = idx2 % 5;

        var cell1 = GridCells[idx1];
        var cell2 = GridCells[idx2];

        GridItem? item1 = cell1.HasItem ? cell1.ToGridItem() : null;
        GridItem? item2 = cell2.HasItem ? cell2.ToGridItem() : null;

        // Reassign positions before persisting
        if (item1 is not null)
        {
            item1.Row = row2;
            item1.Column = col2;
        }
        if (item2 is not null)
        {
            item2.Row = row1;
            item2.Column = col1;
        }

        // Clear both slots in config first, then re-add (SetItem dedupes by position).
        _config.SetItem(new GridItem { Row = row1, Column = col1 });
        _config.SetItem(new GridItem { Row = row2, Column = col2 });
        if (item2 is not null) _config.SetItem(item2);
        if (item1 is not null) _config.SetItem(item1);
        _config.Save();

        // Reload cell view-models so visuals update
        cell1.LoadFrom(item2);
        cell2.LoadFrom(item1);
    }
}
