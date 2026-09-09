using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SQLErrorAtlas.Models;
using SQLErrorAtlas.Services;

namespace SQLErrorAtlas.ViewModels;

/// <summary>Free-text search across catalog, playbook, groups and issues (debounced).</summary>
public partial class SearchViewModel : ViewModelBase
{
    private readonly AtlasDatabase _db;
    private readonly IAtlasNavigator _navigator;
    private CancellationTokenSource? _debounce;

    public SearchViewModel(AtlasDatabase db, IAtlasNavigator navigator)
    {
        _db = db;
        _navigator = navigator;
        Detail = new EntryDetailViewModel(navigator);
    }

    public EntryDetailViewModel Detail { get; }

    public ObservableCollection<SearchHit> Results { get; } = new();

    [ObservableProperty]
    private string _query = "";

    [ObservableProperty]
    private string _statusText = "Type at least two characters. A number jumps straight to that message.";

    [ObservableProperty]
    private SearchHit? _selectedHit;

    partial void OnQueryChanged(string value)
    {
        _debounce?.Cancel();
        _debounce = new CancellationTokenSource();
        var token = _debounce.Token;
        _ = DebouncedSearchAsync(value, token);
    }

    private async Task DebouncedSearchAsync(string value, CancellationToken token)
    {
        try
        {
            await Task.Delay(150, token);
        }
        catch (TaskCanceledException)
        {
            return;
        }
        if (token.IsCancellationRequested) return;
        RunSearch(value);
    }

    [RelayCommand]
    private void Search() => RunSearch(Query);

    private void RunSearch(string value)
    {
        var hits = _db.Search(value);
        Results.Clear();
        foreach (var h in hits) Results.Add(h);
        StatusText = value.Trim().Length < 2
            ? "Type at least two characters. A number jumps straight to that message."
            : hits.Count == 0 ? "No matches." : $"{hits.Count} match(es).";
    }

    partial void OnSelectedHitChanged(SearchHit? value)
    {
        if (value is null) return;
        switch (value.Kind)
        {
            case "Issue" when value.Id is { } issueId:
                _navigator.ShowIssue(issueId);
                break;
            case "Group" when value.Id is { } groupId:
                _navigator.ShowGroup(groupId);
                break;
            case "Playbook" or "Message" when value.Id is { } number:
                Detail.Guidance = _db.Resolve(number);
                break;
        }
    }
}
