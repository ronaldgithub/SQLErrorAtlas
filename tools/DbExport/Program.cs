using System.Data;
using DuckDB.NET.Data;
using Microsoft.Data.SqlClient;

// SQL Error Atlas - data export tool
// Reads the `issues` database on SQL Server and writes a self-contained DuckDB
// file that ships with the app. Re-run whenever the playbook/catalog is extended:
//
//   dotnet run --project tools/DbExport
//
// Connection string: env SQLERRORATLAS_ISSUES_CONN, else the WIN10 default below.
// Output path: first CLI arg, else <repo>/data/sqlerroratlas.duckdb.

const string DefaultConn =
    "Server=win10;Database=issues;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=True";

string sqlConn = Environment.GetEnvironmentVariable("SQLERRORATLAS_ISSUES_CONN") ?? DefaultConn;

string outPath = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data", "sqlerroratlas.duckdb"));

Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
foreach (var f in new[] { outPath, outPath + ".wal" })
    if (File.Exists(f)) File.Delete(f);

Console.WriteLine($"Source : {new SqlConnectionStringBuilder(sqlConn) { Password = "***" }}");
Console.WriteLine($"Target : {outPath}");

using var src = new SqlConnection(sqlConn);
src.Open();

using var dst = new DuckDBConnection($"DataSource={outPath}");
dst.Open();

Exec(dst, """
    CREATE TABLE error_message_group (
        group_id       INTEGER PRIMARY KEY,
        group_name     VARCHAR NOT NULL,
        area_tag       VARCHAR NOT NULL,
        urgency        VARCHAR NOT NULL,
        what_it_means  VARCHAR NOT NULL,
        investigate    VARCHAR NOT NULL,
        fix            VARCHAR NOT NULL,
        key_tools      VARCHAR NOT NULL,
        author_tool    VARCHAR NOT NULL,
        created_utc    TIMESTAMP NOT NULL,
        updated_utc    TIMESTAMP NOT NULL
    );
    CREATE TABLE error_message_catalog (
        message_id     INTEGER PRIMARY KEY,
        severity       INTEGER NOT NULL,
        severity_band  VARCHAR NOT NULL,
        message_text   VARCHAR NOT NULL,
        source_build   VARCHAR,
        captured_utc   TIMESTAMP NOT NULL,
        updated_utc    TIMESTAMP NOT NULL,
        is_event_logged BOOLEAN NOT NULL,
        group_id       INTEGER
    );
    CREATE TABLE error_message_playbook (
        message_id     INTEGER PRIMARY KEY,
        group_id       INTEGER,
        short_name     VARCHAR NOT NULL,
        urgency        VARCHAR NOT NULL,
        meaning        VARCHAR NOT NULL,
        investigate    VARCHAR NOT NULL,
        fix            VARCHAR NOT NULL,
        docs_url       VARCHAR,
        status         VARCHAR NOT NULL,
        author_tool    VARCHAR NOT NULL,
        created_utc    TIMESTAMP NOT NULL,
        updated_utc    TIMESTAMP NOT NULL
    );
    CREATE TABLE issue (
        issue_id          INTEGER PRIMARY KEY,
        title             VARCHAR NOT NULL,
        category          VARCHAR,
        product           VARCHAR,
        error_number      INTEGER,
        error_severity    INTEGER,
        error_state       INTEGER,
        status            VARCHAR NOT NULL,
        severity          VARCHAR,
        source            VARCHAR,
        first_observed_utc TIMESTAMP,
        summary           VARCHAR,
        created_utc       TIMESTAMP NOT NULL,
        created_by        VARCHAR NOT NULL,
        analysis_by       VARCHAR
    );
    CREATE TABLE issue_analysis (
        analysis_id   INTEGER PRIMARY KEY,
        issue_id      INTEGER NOT NULL,
        sort_order    INTEGER NOT NULL,
        section       VARCHAR NOT NULL,
        content_md    VARCHAR NOT NULL,
        author        VARCHAR NOT NULL,
        created_utc   TIMESTAMP NOT NULL,
        author_tool   VARCHAR NOT NULL
    );
    CREATE TABLE issue_diagnostic_query (
        diagnostic_id INTEGER PRIMARY KEY,
        issue_id      INTEGER NOT NULL,
        sort_order    INTEGER NOT NULL,
        purpose       VARCHAR NOT NULL,
        sql_text      VARCHAR NOT NULL,
        created_utc   TIMESTAMP NOT NULL,
        author_tool   VARCHAR NOT NULL
    );
    -- Curated cross-links (source DB has no FK between Issue and groups).
    CREATE TABLE issue_group_link (
        issue_id  INTEGER NOT NULL,
        group_id  INTEGER NOT NULL
    );
    CREATE TABLE meta (
        key    VARCHAR PRIMARY KEY,
        value  VARCHAR NOT NULL
    );
    """);

var counts = new Dictionary<string, long>();

counts["error_message_group"] = Copy(src, dst, "error_message_group", """
    SELECT GroupId, GroupName, AreaTag, Urgency, WhatItMeans, Investigate, Fix, KeyTools, AuthorTool, CreatedUtc, UpdatedUtc
    FROM dbo.ErrorMessageGroup ORDER BY GroupId
    """, 11);

counts["error_message_catalog"] = Copy(src, dst, "error_message_catalog", """
    SELECT MessageId, Severity, SeverityBand, MessageText, SourceBuild, CapturedUtc, UpdatedUtc, IsEventLogged, GroupId
    FROM dbo.ErrorMessageCatalog ORDER BY MessageId
    """, 9);

counts["error_message_playbook"] = Copy(src, dst, "error_message_playbook", """
    SELECT MessageId, GroupId, ShortName, Urgency, Meaning, Investigate, Fix, DocsUrl, Status, AuthorTool, CreatedUtc, UpdatedUtc
    FROM dbo.ErrorMessagePlaybook ORDER BY MessageId
    """, 12);

counts["issue"] = Copy(src, dst, "issue", """
    SELECT IssueId, Title, Category, Product, ErrorNumber, ErrorSeverity, ErrorState, Status, Severity, Source,
           FirstObservedUtc, Summary, CreatedUtc, CreatedBy, AnalysisBy
    FROM dbo.Issue ORDER BY IssueId
    """, 15);

counts["issue_analysis"] = Copy(src, dst, "issue_analysis", """
    SELECT AnalysisId, IssueId, SortOrder, Section, ContentMd, Author, CreatedUtc, AuthorTool
    FROM dbo.IssueAnalysis ORDER BY AnalysisId
    """, 8);

counts["issue_diagnostic_query"] = Copy(src, dst, "issue_diagnostic_query", """
    SELECT DiagnosticId, IssueId, SortOrder, Purpose, SqlText, CreatedUtc, AuthorTool
    FROM dbo.IssueDiagnosticQuery ORDER BY DiagnosticId
    """, 7);

// Curated Issue <-> group links. Issue 3 is the ERRORLOG fatal-message playbook and
// maps to the emergency corruption/storage/recovery groups; Issue 1 is Service Broker.
Exec(dst, """
    INSERT INTO issue_group_link VALUES
      (3, 1), (3, 2), (3, 3), (3, 4), (3, 9),
      (1, 17);
    """);

// Lower-case helper columns + indexes for fast case-insensitive search.
Exec(dst, """
    ALTER TABLE error_message_catalog  ADD COLUMN message_text_lc VARCHAR;
    UPDATE error_message_catalog  SET message_text_lc = lower(message_text);
    ALTER TABLE error_message_playbook ADD COLUMN search_lc VARCHAR;
    UPDATE error_message_playbook SET search_lc = lower(short_name || ' ' || meaning || ' ' || investigate || ' ' || fix);
    ALTER TABLE error_message_group    ADD COLUMN search_lc VARCHAR;
    UPDATE error_message_group    SET search_lc = lower(group_name || ' ' || area_tag || ' ' || what_it_means || ' ' || investigate || ' ' || fix || ' ' || key_tools);
    ALTER TABLE issue_analysis          ADD COLUMN content_lc VARCHAR;
    UPDATE issue_analysis          SET content_lc = lower(section || ' ' || content_md);
    CREATE INDEX ix_catalog_group ON error_message_catalog(group_id);
    CREATE INDEX ix_playbook_group ON error_message_playbook(group_id);
    """);

string build = ScalarString(src, "SELECT TOP 1 SourceBuild FROM dbo.ErrorMessageCatalog WHERE SourceBuild IS NOT NULL") ?? "unknown";
using (var m = dst.CreateCommand())
{
    m.CommandText = "INSERT INTO meta VALUES ($k, $v)";
    void Put(string k, string v)
    {
        m.Parameters.Clear();
        var pk = m.CreateParameter(); pk.ParameterName = "k"; pk.Value = k; m.Parameters.Add(pk);
        var pv = m.CreateParameter(); pv.ParameterName = "v"; pv.Value = v; m.Parameters.Add(pv);
        m.ExecuteNonQuery();
    }
    Put("generated_utc", DateTime.UtcNow.ToString("O"));
    Put("source_build", build);
    Put("schema_version", "1");
    foreach (var (k, v) in counts) Put("rows_" + k, v.ToString());
}

Console.WriteLine();
foreach (var (k, v) in counts) Console.WriteLine($"  {k,-26} {v,8:N0}");

var expected = new Dictionary<string, long>
{
    ["error_message_group"] = 32,
    ["error_message_catalog"] = 16708,
    ["error_message_playbook"] = 180,
    ["issue"] = 3,
    ["issue_analysis"] = 21,
    ["issue_diagnostic_query"] = 15,
};
var drift = expected.Where(e => counts.GetValueOrDefault(e.Key) != e.Value).ToList();
if (drift.Count > 0)
{
    Console.Error.WriteLine();
    foreach (var d in drift)
        Console.Error.WriteLine($"  ROW COUNT DRIFT: {d.Key} expected {d.Value}, got {counts.GetValueOrDefault(d.Key)}");
    Console.Error.WriteLine("  (update the expected counts in tools/DbExport/Program.cs if this is intentional)");
    return 1;
}

Exec(dst, "CHECKPOINT");
dst.Close();
Console.WriteLine($"\nWrote {new FileInfo(outPath).Length / 1024.0 / 1024.0:N1} MB -> {outPath}");
return 0;

static void Exec(DuckDBConnection c, string sql)
{
    using var cmd = c.CreateCommand();
    cmd.CommandText = sql;
    cmd.ExecuteNonQuery();
}

static string? ScalarString(SqlConnection c, string sql)
{
    using var cmd = new SqlCommand(sql, c);
    var o = cmd.ExecuteScalar();
    return o is null or DBNull ? null : Convert.ToString(o);
}

static long Copy(SqlConnection src, DuckDBConnection dst, string table, string query, int columnCount)
{
    using var cmd = new SqlCommand(query, src) { CommandTimeout = 120 };
    using var reader = cmd.ExecuteReader();
    long n = 0;
    using (var appender = dst.CreateAppender(table))
    {
        while (reader.Read())
        {
            var row = appender.CreateRow();
            for (int i = 0; i < columnCount; i++)
            {
                if (reader.IsDBNull(i)) { row.AppendNullValue(); continue; }
                switch (reader.GetFieldType(i))
                {
                    case var t when t == typeof(int): row.AppendValue(reader.GetInt32(i)); break;
                    case var t when t == typeof(long): row.AppendValue(reader.GetInt64(i)); break;
                    case var t when t == typeof(bool): row.AppendValue(reader.GetBoolean(i)); break;
                    case var t when t == typeof(DateTime): row.AppendValue(reader.GetDateTime(i)); break;
                    default: row.AppendValue(reader.GetValue(i)?.ToString()); break;
                }
            }
            row.EndRow();
            n++;
        }
    }
    return n;
}
