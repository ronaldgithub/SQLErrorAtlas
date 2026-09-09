using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SQLErrorAtlas.Models;
using SQLErrorAtlas.Services;

namespace SQLErrorAtlas.ViewModels;

/// <summary>The long-form incident runbooks (issue / issue_analysis / issue_diagnostic_query).</summary>
public partial class IssuesViewModel : ViewModelBase
{
    private readonly AtlasDatabase _db;

    public IssuesViewModel(AtlasDatabase db)
    {
        _db = db;
        Issues = new ObservableCollection<IssueSummary>(_db.GetIssues());
    }

    public ObservableCollection<IssueSummary> Issues { get; }
    public ObservableCollection<IssueSectionItem> Sections { get; } = new();
    public ObservableCollection<DiagnosticItem> Diagnostics { get; } = new();

    [ObservableProperty]
    private IssueSummary? _selectedIssue;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasIssue), nameof(Title), nameof(SummaryMd), nameof(HasSummary),
        nameof(HasDiagnostics))]
    private IssueDetail? _current;

    public bool HasIssue => Current is not null;
    public string Title => Current?.Summary.Title ?? "";
    public string SummaryMd => Current?.SummaryMd ?? "";
    public bool HasSummary => !string.IsNullOrWhiteSpace(Current?.SummaryMd);
    public bool HasDiagnostics => Current?.Diagnostics.Count > 0;

    partial void OnSelectedIssueChanged(IssueSummary? value)
    {
        if (value is null) return;
        Load(value.IssueId);
    }

    public void ShowIssue(int issueId)
    {
        SelectedIssue = Issues.FirstOrDefault(i => i.IssueId == issueId);
        Load(issueId);
    }

    private void Load(int issueId)
    {
        Current = _db.GetIssue(issueId);
        Sections.Clear();
        Diagnostics.Clear();
        if (Current is null) return;
        foreach (var s in Current.Sections) Sections.Add(s);
        foreach (var d in Current.Diagnostics) Diagnostics.Add(d);
    }

    [RelayCommand]
    private async Task CopyDiagnosticAsync(DiagnosticItem? item)
    {
        if (item is not null && UiServices.Current is { } ui)
            await ui.SetClipboardTextAsync(item.SqlText);
    }
}
