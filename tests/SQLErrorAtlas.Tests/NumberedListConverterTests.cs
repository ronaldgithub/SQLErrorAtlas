using System.Globalization;
using SQLErrorAtlas;
using Xunit;

namespace SQLErrorAtlas.Tests;

public class NumberedListConverterTests
{
    private static string Run(string input) =>
        (string)Converters.NumberedListToLines.Convert(input, typeof(string), null, CultureInfo.InvariantCulture)!;

    [Fact]
    public void Splits_inline_numbered_steps_onto_their_own_lines()
    {
        var outp = Run("1. Note the database. 2. Check the event log. 3. Run DBCC CHECKDB.");
        Assert.Equal(
            "1. Note the database.\n2. Check the event log.\n3. Run DBCC CHECKDB.",
            outp);
    }

    [Fact]
    public void Indents_lettered_substeps()
    {
        var outp = Run("5. Choose recovery: a. Restore the pages. b. Restore the database.");
        Assert.Contains("\n    a. Restore the pages.", outp);
        Assert.Contains("\n    b. Restore the database.", outp);
    }

    [Fact]
    public void Leaves_prose_without_markers_untouched()
    {
        const string prose = "Check sys.dm_io_virtual_file_stats and confirm antivirus excludes the data files.";
        Assert.Equal(prose, Run(prose));
    }

    [Fact]
    public void Does_not_break_on_abbreviations_or_versions()
    {
        const string s = "Applies to SQL Server 2019 CU12. See e.g. the docs. Also i.e. the log.";
        Assert.Equal(s, Run(s));
    }

    [Fact]
    public void Null_and_empty_pass_through()
    {
        Assert.Null(Converters.NumberedListToLines.Convert(null, typeof(string), null, CultureInfo.InvariantCulture));
        Assert.Equal("", Run(""));
    }
}
