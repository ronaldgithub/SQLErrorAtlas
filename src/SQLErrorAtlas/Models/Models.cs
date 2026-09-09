namespace SQLErrorAtlas.Models;

/// <summary>Where a piece of guidance for an error number came from.</summary>
public enum GuidanceKind
{
    /// <summary>A message-specific entry in <c>error_message_playbook</c>.</summary>
    Playbook,

    /// <summary>Fallback: the group-level guidance the message belongs to.</summary>
    Group,

    /// <summary>The number is not in the catalog at all.</summary>
    Unknown,
}

/// <summary>Resolved "what it means / what to check / what to do" for one error number.</summary>
public sealed record MessageGuidance(
    int? MessageId,
    GuidanceKind Kind,
    string Title,
    string Urgency,
    string? Status,
    string Meaning,
    string Investigate,
    string Fix,
    string? KeyTools,
    string? DocsUrl,
    int? GroupId,
    string? GroupName,
    IReadOnlyList<RelatedIssue> RelatedIssues)
{
    public bool IsPlaybook => Kind == GuidanceKind.Playbook;
    public bool IsGroupFallback => Kind == GuidanceKind.Group;
    public bool IsUnknown => Kind == GuidanceKind.Unknown;
    public bool HasDocsUrl => !string.IsNullOrWhiteSpace(DocsUrl);
    public bool HasKeyTools => !string.IsNullOrWhiteSpace(KeyTools);
    public bool HasRelatedIssues => RelatedIssues.Count > 0;
}

public sealed record RelatedIssue(int IssueId, string Title);

public sealed record GroupSummary(
    int GroupId,
    string GroupName,
    string AreaTag,
    string Urgency,
    int MessageCount,
    int PlaybookCount);

public sealed record GroupDetail(
    GroupSummary Summary,
    string WhatItMeans,
    string Investigate,
    string Fix,
    string KeyTools,
    IReadOnlyList<PlaybookRef> Playbooks,
    IReadOnlyList<CatalogRef> Messages,
    IReadOnlyList<RelatedIssue> RelatedIssues);

public sealed record PlaybookRef(int MessageId, string ShortName, string Urgency, string Status);

public sealed record CatalogRef(
    int MessageId,
    int Severity,
    string SeverityBand,
    string MessageText,
    bool IsEventLogged,
    bool HasPlaybook);

public sealed record SearchHit(
    string Kind,
    int? Id,
    string Title,
    string Snippet,
    string Urgency,
    double Rank);

public sealed record IssueSummary(
    int IssueId,
    string Title,
    string? Category,
    string? Product,
    string Status,
    string? Severity,
    int? ErrorNumber);

public sealed record IssueDetail(
    IssueSummary Summary,
    string? SummaryMd,
    IReadOnlyList<IssueSectionItem> Sections,
    IReadOnlyList<DiagnosticItem> Diagnostics);

public sealed record IssueSectionItem(int SortOrder, string Section, string ContentMd);

public sealed record DiagnosticItem(int SortOrder, string Purpose, string SqlText);

/// <summary>One error number pulled out of a pasted ERRORLOG blob.</summary>
public sealed record ParsedError(int Number, int? Severity, int? State, string RawLine, int Occurrences);

/// <summary>Result of running <see cref="Services.ErrorLogParser"/> over pasted text.</summary>
public sealed record LogParseResult(
    IReadOnlyList<ParsedError> Errors,
    IReadOnlyList<string> NotableLines);

public sealed record AtlasMeta(
    DateTime GeneratedUtc,
    string SourceBuild,
    IReadOnlyDictionary<string, long> RowCounts)
{
    public long Rows(string table) => RowCounts.TryGetValue(table, out var n) ? n : 0;
}
