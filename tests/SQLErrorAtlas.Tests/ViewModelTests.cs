using SQLErrorAtlas.Models;
using SQLErrorAtlas.Services;
using SQLErrorAtlas.ViewModels;
using Xunit;

namespace SQLErrorAtlas.Tests;

/// <summary>
/// Exercises the view-model layer against the real dataset without a UI, so the
/// wiring (parse -> resolve -> collections) is covered even though the Avalonia
/// views are not unit tested.
/// </summary>
public sealed class ViewModelTests : IDisposable
{
    private readonly AtlasDatabase _db;
    private readonly StubNavigator _nav = new();

    public ViewModelTests()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "sqlerroratlas.duckdb");
        _db = new AtlasDatabase(path);
    }

    [Fact]
    public void Analyze_populates_ranked_results_and_selects_first()
    {
        var vm = new AnalyzeViewModel(_db, _nav)
        {
            LogText = string.Join('\n',
                "spid19s Error: 9001, Severity: 21, State: 1.",
                "spid19s Error: 823, Severity: 24, State: 2.",
                "spid5   SQL Server has encountered 3 occurrence(s) of I/O requests taking longer than 15 seconds"),
        };

        vm.AnalyzeCommand.Execute(null);

        Assert.Equal(2, vm.Results.Count);
        Assert.Equal(823, vm.Results[0].Number);          // severity 24 ranks first
        Assert.Single(vm.NotableLines);
        Assert.NotNull(vm.SelectedResult);
        Assert.Same(vm.SelectedResult!.Guidance, vm.Detail.Guidance);
        Assert.True(vm.Detail.HasGuidance);
    }

    [Fact]
    public void Lookup_resolves_into_detail_pane()
    {
        var vm = new LookupViewModel(_db, _nav);
        vm.LookupNumber(9002);
        Assert.True(vm.Detail.HasGuidance);
        Assert.Contains("9002", vm.Detail.HeaderText);
    }

    [Fact]
    public void Browse_lists_groups_by_urgency_and_loads_detail()
    {
        var vm = new BrowseViewModel(_db, _nav);
        Assert.Equal(32, vm.Groups.Count);
        Assert.Equal("Emergency", vm.Groups[0].Urgency);

        vm.SelectedGroup = vm.Groups.First(g => g.GroupName.Contains("corruption", StringComparison.OrdinalIgnoreCase));
        Assert.True(vm.HasGroup);
        Assert.NotEmpty(vm.Messages);
        Assert.NotEmpty(vm.Playbooks);
    }

    [Fact]
    public void Search_debounced_command_fills_results_and_navigates_issue()
    {
        var vm = new SearchViewModel(_db, _nav);
        vm.Query = "checkdb";
        vm.SearchCommand.Execute(null);
        Assert.NotEmpty(vm.Results);

        var issueHit = vm.Results.First(h => h.Kind == "Issue");
        vm.SelectedHit = issueHit;
        Assert.Equal(issueHit.Id, _nav.LastIssue);
    }

    [Fact]
    public void Issues_tab_loads_sections_and_diagnostics()
    {
        var vm = new IssuesViewModel(_db);
        Assert.Equal(3, vm.Issues.Count);
        vm.SelectedIssue = vm.Issues.First(i => i.IssueId == 3);
        Assert.True(vm.HasIssue);
        Assert.NotEmpty(vm.Sections);
    }

    [Fact]
    public void Exported_markdown_and_html_contain_the_guidance()
    {
        var g = _db.Resolve(823);
        var md = EntryExporter.ToMarkdown(g);
        Assert.Contains("What it means", md);
        Assert.Contains("Fix", md);

        var html = EntryExporter.ToHtml("823", md);
        Assert.StartsWith("<!doctype html>", html);
        Assert.Contains("<h2>", html);
    }

    public void Dispose() => _db.Dispose();

    private sealed class StubNavigator : IAtlasNavigator
    {
        public int? LastIssue { get; private set; }
        public int? LastGroup { get; private set; }
        public int? LastGuidance { get; private set; }
        public void ShowIssue(int issueId) => LastIssue = issueId;
        public void ShowGroup(int groupId) => LastGroup = groupId;
        public void ShowGuidance(int errorNumber) => LastGuidance = errorNumber;
        public void ShowSearch(string query) { }
    }
}
