using SQLErrorAtlas.Models;
using SQLErrorAtlas.Services;
using Xunit;

namespace SQLErrorAtlas.Tests;

public sealed class AtlasDatabaseTests : IDisposable
{
    private readonly AtlasDatabase _db;

    public AtlasDatabaseTests()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "sqlerroratlas.duckdb");
        Assert.True(File.Exists(path), $"dataset not copied to test output: {path}");
        _db = new AtlasDatabase(path);
    }

    [Fact]
    public void Meta_row_counts_match_expected()
    {
        var meta = _db.GetMeta();
        Assert.Equal(16708, meta.Rows("error_message_catalog"));
        Assert.Equal(32, meta.Rows("error_message_group"));
        Assert.Equal(180, meta.Rows("error_message_playbook"));
        Assert.NotEqual(default, meta.GeneratedUtc);
    }

    [Fact]
    public void Known_fatal_number_resolves_to_playbook_entry()
    {
        var g = _db.Resolve(823);
        Assert.Equal(GuidanceKind.Playbook, g.Kind);
        Assert.False(string.IsNullOrWhiteSpace(g.Fix));
        Assert.Equal("Emergency", g.Urgency);
    }

    [Fact]
    public void Catalog_only_number_falls_back_to_group_guidance()
    {
        // 1105 (filegroup is full) is catalogued and grouped but has, at snapshot
        // time, no message-specific playbook entry.
        var g = _db.Resolve(1105);
        Assert.Equal(GuidanceKind.Group, g.Kind);
        Assert.NotNull(g.GroupName);
        Assert.False(string.IsNullOrWhiteSpace(g.Investigate));
    }

    [Fact]
    public void Nonsense_number_is_unknown()
    {
        var g = _db.Resolve(99999999);
        Assert.Equal(GuidanceKind.Unknown, g.Kind);
    }

    [Fact]
    public void Search_checkdb_returns_playbook_and_issue()
    {
        var hits = _db.Search("checkdb");
        Assert.Contains(hits, h => h.Kind == "Playbook");
        Assert.Contains(hits, h => h.Kind == "Issue");
    }

    [Fact]
    public void Search_by_number_pins_that_message()
    {
        var hits = _db.Search("823");
        Assert.Equal(823, hits[0].Id);
    }

    [Fact]
    public void Corruption_group_links_to_the_checkdb_runbook()
    {
        var g = _db.Resolve(823);
        Assert.Contains(g.RelatedIssues, i => i.IssueId == 3);
    }

    [Fact]
    public void All_groups_load_with_detail()
    {
        foreach (var group in _db.GetGroups())
        {
            var detail = _db.GetGroup(group.GroupId);
            Assert.NotNull(detail);
            Assert.False(string.IsNullOrWhiteSpace(detail!.Fix));
        }
    }

    [Fact]
    public void Issues_have_sections()
    {
        var issue = _db.GetIssue(3);
        Assert.NotNull(issue);
        Assert.NotEmpty(issue!.Sections);
    }

    public void Dispose() => _db.Dispose();
}
