using SQLErrorAtlas.Services;
using Xunit;

namespace SQLErrorAtlas.Tests;

public class ErrorLogParserTests
{
    [Fact]
    public void Extracts_number_severity_state_from_standard_line()
    {
        var result = ErrorLogParser.Parse(
            "2024-01-05 12:00:00.42 spid57  Error: 823, Severity: 24, State: 2.");

        var e = Assert.Single(result.Errors);
        Assert.Equal(823, e.Number);
        Assert.Equal(24, e.Severity);
        Assert.Equal(2, e.State);
    }

    [Fact]
    public void Dedupes_by_number_and_counts_occurrences_keeping_max_severity()
    {
        var log = string.Join('\n',
            "spid1  Error: 9002, Severity: 17, State: 2.",
            "spid1  Error: 9002, Severity: 17, State: 4.",
            "spid1  Error: 9002, Severity: 17, State: 2.");

        var e = Assert.Single(ErrorLogParser.Parse(log).Errors);
        Assert.Equal(9002, e.Number);
        Assert.Equal(3, e.Occurrences);
    }

    [Fact]
    public void Orders_by_severity_descending()
    {
        var log = string.Join('\n',
            "Error: 9002, Severity: 17, State: 2.",
            "Error: 823, Severity: 24, State: 1.",
            "Error: 17883, Severity: 20, State: 1.");

        var numbers = ErrorLogParser.Parse(log).Errors.Select(e => e.Number).ToArray();
        Assert.Equal(new[] { 823, 17883, 9002 }, numbers);
    }

    [Fact]
    public void Flags_notable_lines_without_a_number()
    {
        var log = "2024-01-05 12:00:00.10 spid5  SQL Server has encountered 1 occurrence(s) of I/O requests taking longer than 15 seconds to complete on file [D:\\data\\x.mdf]";
        var result = ErrorLogParser.Parse(log);

        Assert.Empty(result.Errors);
        Assert.Single(result.NotableLines);
    }

    [Fact]
    public void Empty_input_is_safe()
    {
        var result = ErrorLogParser.Parse("   ");
        Assert.Empty(result.Errors);
        Assert.Empty(result.NotableLines);
    }
}
