using System.Data;
using DuckDB.NET.Data;
using SQLErrorAtlas.Models;

namespace SQLErrorAtlas.Services;

/// <summary>
/// Read-only access to the shipped DuckDB dataset. All queries are cheap
/// (the dataset is a few MB); a single connection is guarded by a lock because
/// the DuckDB ADO.NET connection is not thread-safe.
/// </summary>
public sealed class AtlasDatabase : IDisposable
{
    private readonly DuckDBConnection _cn;
    private readonly object _gate = new();

    public AtlasDatabase(string duckDbPath)
    {
        _cn = new DuckDBConnection($"DataSource={duckDbPath};ACCESS_MODE=READ_ONLY");
        _cn.Open();
    }

    public AtlasMeta GetMeta()
    {
        var rows = new Dictionary<string, long>();
        DateTime generated = default;
        string build = "unknown";
        foreach (var (k, v) in Query("SELECT key, value FROM meta", r => (r.GetString(0), r.GetString(1))))
        {
            if (k == "generated_utc") DateTime.TryParse(v, null, System.Globalization.DateTimeStyles.RoundtripKind, out generated);
            else if (k == "source_build") build = v;
            else if (k.StartsWith("rows_", StringComparison.Ordinal) && long.TryParse(v, out var n)) rows[k[5..]] = n;
        }
        return new AtlasMeta(generated, build, rows);
    }

    // ---- error-number resolution --------------------------------------------

    public MessageGuidance Resolve(int number)
    {
        var pb = Query(
            """
            SELECT p.short_name, p.urgency, p.status, p.meaning, p.investigate, p.fix, p.docs_url,
                   p.group_id, g.group_name
            FROM error_message_playbook p
            LEFT JOIN error_message_group g ON g.group_id = p.group_id
            WHERE p.message_id = $n
            """,
            r => new
            {
                ShortName = r.GetString(0),
                Urgency = r.GetString(1),
                Status = r.GetString(2),
                Meaning = r.GetString(3),
                Investigate = r.GetString(4),
                Fix = r.GetString(5),
                DocsUrl = r.IsDBNull(6) ? null : r.GetString(6),
                GroupId = r.IsDBNull(7) ? (int?)null : r.GetInt32(7),
                GroupName = r.IsDBNull(8) ? null : r.GetString(8),
            },
            ("n", number)).FirstOrDefault();

        if (pb is not null)
        {
            return new MessageGuidance(number, GuidanceKind.Playbook, pb.ShortName, pb.Urgency, pb.Status,
                pb.Meaning, pb.Investigate, pb.Fix, null, pb.DocsUrl, pb.GroupId, pb.GroupName,
                RelatedIssuesForGroup(pb.GroupId));
        }

        var cat = Query(
            """
            SELECT c.severity, c.severity_band, c.message_text, c.group_id,
                   g.group_name, g.urgency, g.what_it_means, g.investigate, g.fix, g.key_tools
            FROM error_message_catalog c
            LEFT JOIN error_message_group g ON g.group_id = c.group_id
            WHERE c.message_id = $n
            """,
            r => new
            {
                Severity = r.GetInt32(0),
                Band = r.GetString(1),
                Text = r.GetString(2),
                GroupId = r.IsDBNull(3) ? (int?)null : r.GetInt32(3),
                GroupName = r.IsDBNull(4) ? null : r.GetString(4),
                Urgency = r.IsDBNull(5) ? null : r.GetString(5),
                WhatItMeans = r.IsDBNull(6) ? null : r.GetString(6),
                Investigate = r.IsDBNull(7) ? null : r.GetString(7),
                Fix = r.IsDBNull(8) ? null : r.GetString(8),
                KeyTools = r.IsDBNull(9) ? null : r.GetString(9),
            },
            ("n", number)).FirstOrDefault();

        if (cat is { GroupName: not null })
        {
            var title = $"{cat.GroupName}";
            return new MessageGuidance(number, GuidanceKind.Group, title, cat.Urgency ?? "Unknown", null,
                cat.WhatItMeans ?? "", cat.Investigate ?? "", cat.Fix ?? "", cat.KeyTools, null,
                cat.GroupId, cat.GroupName, RelatedIssuesForGroup(cat.GroupId))
            {
            };
        }

        if (cat is not null)
        {
            // In the catalog but no group classification.
            return new MessageGuidance(number, GuidanceKind.Group,
                $"Message {number} (severity {cat.Severity}, {cat.Band})", "Unknown", null,
                cat.Text,
                "No group-level guidance has been written for this message yet. Treat it by its severity band and the surrounding ERRORLOG context.",
                "No fix guidance yet — see the severity band and any accompanying messages.",
                null, null, null, null, Array.Empty<RelatedIssue>());
        }

        return new MessageGuidance(number, GuidanceKind.Unknown,
            $"Unknown message number {number}", "Unknown", null,
            $"Number {number} is not in the SQL Server message catalog snapshot. Check it is an engine error (not an application RAISERROR) and that the digits are correct.",
            "", "", null, null, null, null, Array.Empty<RelatedIssue>());
    }

    private IReadOnlyList<RelatedIssue> RelatedIssuesForGroup(int? groupId)
    {
        if (groupId is null) return Array.Empty<RelatedIssue>();
        return Query(
            """
            SELECT DISTINCT i.issue_id, i.title
            FROM issue_group_link l JOIN issue i ON i.issue_id = l.issue_id
            WHERE l.group_id = $g
            ORDER BY i.issue_id
            """,
            r => new RelatedIssue(r.GetInt32(0), r.GetString(1)),
            ("g", groupId.Value)).ToArray();
    }

    // ---- browse -------------------------------------------------------------

    public IReadOnlyList<GroupSummary> GetGroups() => Query(
        """
        SELECT g.group_id, g.group_name, g.area_tag, g.urgency,
               (SELECT count(*) FROM error_message_catalog c WHERE c.group_id = g.group_id),
               (SELECT count(*) FROM error_message_playbook p WHERE p.group_id = g.group_id)
        FROM error_message_group g
        ORDER BY g.group_id
        """,
        r => new GroupSummary(r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetString(3),
            (int)r.GetInt64(4), (int)r.GetInt64(5))).ToArray();

    public GroupDetail? GetGroup(int groupId)
    {
        var g = Query(
            "SELECT group_id, group_name, area_tag, urgency, what_it_means, investigate, fix, key_tools FROM error_message_group WHERE group_id = $g",
            r => new
            {
                Summary = new GroupSummary(r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetString(3), 0, 0),
                What = r.GetString(4),
                Investigate = r.GetString(5),
                Fix = r.GetString(6),
                KeyTools = r.GetString(7),
            },
            ("g", groupId)).FirstOrDefault();
        if (g is null) return null;

        var playbooks = Query(
            "SELECT message_id, short_name, urgency, status FROM error_message_playbook WHERE group_id = $g ORDER BY message_id",
            r => new PlaybookRef(r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetString(3)),
            ("g", groupId)).ToArray();

        var messages = Query(
            """
            SELECT c.message_id, c.severity, c.severity_band, c.message_text, c.is_event_logged,
                   (p.message_id IS NOT NULL) AS has_pb
            FROM error_message_catalog c
            LEFT JOIN error_message_playbook p ON p.message_id = c.message_id
            WHERE c.group_id = $g
            ORDER BY c.severity DESC, c.message_id
            """,
            r => new CatalogRef(r.GetInt32(0), r.GetInt32(1), r.GetString(2), r.GetString(3),
                r.GetBoolean(4), r.GetBoolean(5)),
            ("g", groupId)).ToArray();

        var summary = g.Summary with { MessageCount = messages.Length, PlaybookCount = playbooks.Length };
        return new GroupDetail(summary, g.What, g.Investigate, g.Fix, g.KeyTools, playbooks, messages,
            RelatedIssuesForGroup(groupId));
    }

    // ---- search ------------------------------------------------------------

    public IReadOnlyList<SearchHit> Search(string? query, int limit = 200)
    {
        query = query?.Trim() ?? "";
        if (query.Length < 2) return Array.Empty<SearchHit>();

        var terms = query.ToLowerInvariant()
            .Split(new[] { ' ', '\t', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= 2).Distinct().Take(6).ToArray();
        if (terms.Length == 0) return Array.Empty<SearchHit>();

        var hits = new List<SearchHit>();

        // Exact number pin.
        if (int.TryParse(query, out var num))
        {
            var g = Resolve(num);
            if (!g.IsUnknown)
                hits.Add(new SearchHit(g.IsPlaybook ? "Playbook" : "Message", num, $"{num} — {g.Title}",
                    Snippet(g.Meaning, terms), g.Urgency, 10_000));
        }

        string Where(string col) => string.Join(" AND ", terms.Select((_, i) => $"{col} LIKE $t{i}"));
        (string, object)[] Params() => terms.Select((t, i) => ($"t{i}", (object)$"%{Escape(t)}%")).ToArray();

        hits.AddRange(Query(
            $"SELECT message_id, short_name, meaning, urgency, status FROM error_message_playbook WHERE {Where("search_lc")} ORDER BY message_id LIMIT 60",
            r => new SearchHit("Playbook", r.GetInt32(0), $"{r.GetInt32(0)} — {r.GetString(1)}",
                Snippet(r.GetString(2), terms), r.GetString(3), 100 + (r.GetString(4) == "done" ? 5 : 0)),
            Params()));

        hits.AddRange(Query(
            $"SELECT group_id, group_name, what_it_means, urgency FROM error_message_group WHERE {Where("search_lc")} ORDER BY group_id LIMIT 32",
            r => new SearchHit("Group", r.GetInt32(0), r.GetString(1),
                Snippet(r.GetString(2), terms), r.GetString(3), 60),
            Params()));

        hits.AddRange(Query(
            $"SELECT message_id, severity, message_text FROM error_message_catalog WHERE {Where("message_text_lc")} ORDER BY severity DESC, message_id LIMIT 120",
            r => new SearchHit("Message", r.GetInt32(0), $"{r.GetInt32(0)} (severity {r.GetInt32(1)})",
                Snippet(r.GetString(2), terms), "", 40),
            Params()));

        hits.AddRange(Query(
            $"SELECT a.issue_id, i.title, a.section, a.content_md FROM issue_analysis a JOIN issue i ON i.issue_id = a.issue_id WHERE {Where("a.content_lc")} ORDER BY a.issue_id, a.sort_order LIMIT 40",
            r => new SearchHit("Issue", r.GetInt32(0), $"Issue {r.GetInt32(0)}: {r.GetString(2)}",
                Snippet(r.GetString(3), terms), "", 45),
            Params()));

        return hits
            .GroupBy(h => (h.Kind, h.Id, h.Title))
            .Select(grp => grp.OrderByDescending(h => h.Rank).First())
            .OrderByDescending(h => h.Rank)
            .ThenBy(h => h.Title, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToArray();
    }

    // ---- issues ----------------------------------------------------------

    public IReadOnlyList<IssueSummary> GetIssues() => Query(
        "SELECT issue_id, title, category, product, status, severity, error_number FROM issue ORDER BY issue_id",
        r => new IssueSummary(r.GetInt32(0), r.GetString(1),
            r.IsDBNull(2) ? null : r.GetString(2), r.IsDBNull(3) ? null : r.GetString(3),
            r.GetString(4), r.IsDBNull(5) ? null : r.GetString(5),
            r.IsDBNull(6) ? (int?)null : r.GetInt32(6))).ToArray();

    public IssueDetail? GetIssue(int issueId)
    {
        var summary = GetIssues().FirstOrDefault(i => i.IssueId == issueId);
        if (summary is null) return null;

        var summaryMd = Query("SELECT summary FROM issue WHERE issue_id = $i",
            r => r.IsDBNull(0) ? null : r.GetString(0), ("i", issueId)).FirstOrDefault();

        var sections = Query(
            "SELECT sort_order, section, content_md FROM issue_analysis WHERE issue_id = $i ORDER BY sort_order",
            r => new IssueSectionItem(r.GetInt32(0), r.GetString(1), r.GetString(2)),
            ("i", issueId)).ToArray();

        var diags = Query(
            "SELECT sort_order, purpose, sql_text FROM issue_diagnostic_query WHERE issue_id = $i ORDER BY sort_order",
            r => new DiagnosticItem(r.GetInt32(0), r.GetString(1), r.GetString(2)),
            ("i", issueId)).ToArray();

        return new IssueDetail(summary, summaryMd, sections, diags);
    }

    // ---- plumbing --------------------------------------------------------

    // LIKE wildcards would let a stray '%' or '_' in a search term match everything;
    // drop them rather than dealing with an ESCAPE clause.
    private static string Escape(string s)
    {
        Span<char> buf = stackalloc char[s.Length];
        int n = 0;
        foreach (var c in s)
            if (c is not ('%' or '_' or '\\')) buf[n++] = c;
        return new string(buf[..n]);
    }

    private static string Snippet(string text, string[] terms)
    {
        if (string.IsNullOrEmpty(text)) return "";
        text = text.Replace('\n', ' ').Replace('\r', ' ');
        int at = -1;
        foreach (var t in terms)
        {
            var idx = text.IndexOf(t, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0 && (at < 0 || idx < at)) at = idx;
        }
        if (at < 0) return text.Length <= 180 ? text : text[..180] + "…";
        var start = Math.Max(0, at - 60);
        var len = Math.Min(text.Length - start, 200);
        var s = text.Substring(start, len).Trim();
        return (start > 0 ? "…" : "") + s + (start + len < text.Length ? "…" : "");
    }

    private List<T> Query<T>(string sql, Func<IDataReader, T> map, params (string Name, object Value)[] ps)
    {
        lock (_gate)
        {
            using var cmd = _cn.CreateCommand();
            cmd.CommandText = sql;
            foreach (var (name, value) in ps)
            {
                var p = cmd.CreateParameter();
                p.ParameterName = name;
                p.Value = value;
                cmd.Parameters.Add(p);
            }
            using var reader = cmd.ExecuteReader();
            var list = new List<T>();
            while (reader.Read()) list.Add(map(reader));
            return list;
        }
    }

    public void Dispose() => _cn.Dispose();
}
