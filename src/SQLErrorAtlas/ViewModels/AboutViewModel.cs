using System.Reflection;
using CommunityToolkit.Mvvm.Input;
using SQLErrorAtlas.Models;
using SQLErrorAtlas.Services;

namespace SQLErrorAtlas.ViewModels;

public partial class AboutViewModel : ViewModelBase
{
    private const string RepoUrl = "https://github.com/ronaldgithub/SQLErrorAtlas";
    private const string ContactEmail = "ronald.de.groot@opendata.nl";

    private readonly AtlasMeta _meta;
    private readonly string _dataPath;

    public AboutViewModel(AtlasMeta meta, string dataPath)
    {
        _meta = meta;
        _dataPath = dataPath;
    }

    public string Version =>
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion?.Split('+')[0]
        ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
        ?? "0.0.0";

    public string Purpose =>
        "Offline triage for the SQL Server ERRORLOG: what a message means, what to check, and what to do — with no internet.";

    public string PrivacyLine => "Fully offline. No telemetry, no network calls, no SQL Server connection.";

    public string DataProvenance =>
        $"{_meta.Rows("error_message_catalog"):N0} catalogued messages · " +
        $"{_meta.Rows("error_message_group")} groups · " +
        $"{_meta.Rows("error_message_playbook")} playbook entries · " +
        $"{_meta.Rows("issue")} deep-dive runbooks";

    public string DataAsOf =>
        _meta.GeneratedUtc == default
            ? "data date unknown"
            : $"Data generated {_meta.GeneratedUtc.ToLocalTime():yyyy-MM-dd HH:mm} from SQL Server build {_meta.SourceBuild}";

    public string RepoUrlText => RepoUrl;

    public string License => "MIT License · © 2026 Ronald de Groot";

    public string Disclaimer =>
        "\"SQL Server\" is a trademark of Microsoft Corporation. SQL Error Atlas is an independent tool and is not affiliated with or endorsed by Microsoft.";

    /// <summary>Version + environment block, appended to bug reports / help e-mails.</summary>
    private string Diagnostics =>
        $"SQL Error Atlas {Version}\n" +
        $"{DataAsOf}\n" +
        $"{DataProvenance}\n" +
        $"DuckDB.NET: {typeof(DuckDB.NET.Data.DuckDBConnection).Assembly.GetName().Version}\n" +
        $"Runtime: {Environment.Version} · OS: {Environment.OSVersion}\n" +
        $"Data file: {_dataPath}";

    [RelayCommand]
    private async Task OpenRepoAsync()
    {
        if (UiServices.Current is { } ui) await ui.OpenUrlAsync(RepoUrl);
    }

    [RelayCommand]
    private async Task CopyDiagnosticsAsync()
    {
        if (UiServices.Current is { } ui) await ui.SetClipboardTextAsync(Diagnostics);
    }

    /// <summary>Open the user's mail client with a pre-filled message to the author.</summary>
    [RelayCommand]
    private async Task SendMessageAsync()
    {
        if (UiServices.Current is not { } ui) return;

        const string subject = "SQL Error Atlas - help";
        var body =
            "Describe what you need help with (paste the ERRORLOG lines if relevant):\n\n\n" +
            "----- version / environment (leave this in) -----\n" +
            Diagnostics + "\n";

        var mailto = $"mailto:{ContactEmail}?subject={Uri.EscapeDataString(subject)}&body={Uri.EscapeDataString(body)}";
        await ui.OpenUrlAsync(mailto);
    }
}
