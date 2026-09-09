using System.Reflection;

namespace SQLErrorAtlas.Services;

/// <summary>
/// The DuckDB dataset ships as an embedded resource but DuckDB needs a real file
/// path to open (it can't read from inside a single-file bundle). On startup we
/// materialise it under %LOCALAPPDATA%\SQLErrorAtlas and refresh it whenever the
/// bundled copy changes (tracked by a sidecar stamp).
/// </summary>
public static class DataFileProvisioner
{
    private const string ResourceName = "SQLErrorAtlas.Data.sqlerroratlas.duckdb";

    public static string EnsureDataFile()
    {
        var asm = Assembly.GetExecutingAssembly();
        using var stream = asm.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded dataset '{ResourceName}' is missing from the build.");

        Directory.CreateDirectory(AppPaths.DataDirectory);
        var target = Path.Combine(AppPaths.DataDirectory, "sqlerroratlas.duckdb");
        var stampPath = target + ".stamp";

        var stamp = $"{asm.GetName().Version}|{stream.Length}";
        var current = File.Exists(stampPath) ? SafeRead(stampPath) : null;

        if (File.Exists(target) && current == stamp)
            return target;

        try
        {
            foreach (var stale in new[] { target, target + ".wal" })
                if (File.Exists(stale)) File.Delete(stale);

            using (var file = File.Create(target))
                stream.CopyTo(file);

            File.WriteAllText(stampPath, stamp);
        }
        catch (IOException) when (File.Exists(target))
        {
            // Another instance holds the file open — the existing copy is fine to use.
        }

        return target;
    }

    private static string? SafeRead(string path)
    {
        try { return File.ReadAllText(path); }
        catch { return null; }
    }
}
