using System.Text.Json;
using System.Text.Json.Serialization;

namespace SQLErrorAtlas.Services;

public enum AppTheme
{
    Dark,
    Light,
    System,
}

public sealed class AppSettings
{
    public AppTheme Theme { get; set; } = AppTheme.Dark;
}

/// <summary>Loads / persists <see cref="AppSettings"/> as JSON under %LOCALAPPDATA%\SQLErrorAtlas.</summary>
public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _path;

    public SettingsService()
    {
        var dir = AppPaths.DataDirectory;
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(_path))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), JsonOptions) ?? new AppSettings();
        }
        catch
        {
            // corrupt file -> fall back to defaults
        }
        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        try
        {
            File.WriteAllText(_path, JsonSerializer.Serialize(settings, JsonOptions));
        }
        catch
        {
            // best effort; a read-only profile just means settings don't persist
        }
    }
}

public static class AppPaths
{
    public static string DataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SQLErrorAtlas");
}
