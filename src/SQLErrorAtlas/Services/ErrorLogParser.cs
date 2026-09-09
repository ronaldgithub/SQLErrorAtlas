using System.Text.RegularExpressions;
using SQLErrorAtlas.Models;

namespace SQLErrorAtlas.Services;

/// <summary>
/// Pulls SQL Server error numbers out of a pasted ERRORLOG chunk. Pure and
/// side-effect free so it can be unit tested against real log snippets.
/// </summary>
public static partial class ErrorLogParser
{
    // e.g.  "2024-01-05 12:00:00.42 spid57  Error: 823, Severity: 24, State: 2."
    [GeneratedRegex(@"Error:\s*(?<num>\d{1,6})(?:\s*,\s*Severity:\s*(?<sev>\d{1,2}))?(?:\s*,\s*State:\s*(?<state>\d{1,4}))?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ErrorLine();

    // Lines that carry no error number but are worth surfacing on their own.
    private static readonly string[] NotableMarkers =
    {
        "I/O requests taking longer than",
        "stack dump",
        "non-yielding",
        "External dump process",
        "latch",
        "FlushCache",
        "AppDomain",
        "SQL Server has encountered",
        "A significant part of sql server process memory has been paged out",
        "There is insufficient system memory",
        "The tempdb database",
        "recovery of database",
        "assertion",
        "Access violation",
    };

    public static LogParseResult Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new LogParseResult(Array.Empty<ParsedError>(), Array.Empty<string>());

        var byNumber = new Dictionary<int, MutableError>();
        var notable = new List<string>();
        var seenNotable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim().TrimEnd('\r');
            if (line.Length == 0) continue;

            var m = ErrorLine().Match(line);
            if (m.Success && int.TryParse(m.Groups["num"].Value, out var num) && num > 0)
            {
                int? sev = m.Groups["sev"].Success ? int.Parse(m.Groups["sev"].Value) : null;
                int? state = m.Groups["state"].Success ? int.Parse(m.Groups["state"].Value) : null;

                if (byNumber.TryGetValue(num, out var existing))
                {
                    existing.Occurrences++;
                    existing.Severity ??= sev;
                    if (sev is { } s && (existing.Severity is null || s > existing.Severity))
                        existing.Severity = s;
                    existing.State ??= state;
                }
                else
                {
                    byNumber[num] = new MutableError
                    {
                        Number = num,
                        Severity = sev,
                        State = state,
                        RawLine = Collapse(line),
                        Occurrences = 1,
                    };
                }
                continue;
            }

            foreach (var marker in NotableMarkers)
            {
                if (line.Contains(marker, StringComparison.OrdinalIgnoreCase))
                {
                    var collapsed = Collapse(line);
                    if (seenNotable.Add(collapsed))
                        notable.Add(collapsed);
                    break;
                }
            }
        }

        var errors = byNumber.Values
            .OrderByDescending(e => e.Severity ?? -1)
            .ThenByDescending(e => e.Occurrences)
            .ThenBy(e => e.Number)
            .Select(e => new ParsedError(e.Number, e.Severity, e.State, e.RawLine, e.Occurrences))
            .ToArray();

        return new LogParseResult(errors, notable);
    }

    private static string Collapse(string line)
    {
        line = WhitespaceRun().Replace(line, " ").Trim();
        return line.Length <= 400 ? line : line[..400] + "…";
    }

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex WhitespaceRun();

    private sealed class MutableError
    {
        public int Number;
        public int? Severity;
        public int? State;
        public string RawLine = "";
        public int Occurrences;
    }
}
