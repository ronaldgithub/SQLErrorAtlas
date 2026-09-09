using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SQLErrorAtlas.Models;
using SQLErrorAtlas.Services;

namespace SQLErrorAtlas.ViewModels;

/// <summary>Paste an ERRORLOG chunk -> ranked list of matched errors with guidance.</summary>
public partial class AnalyzeViewModel : ViewModelBase
{
    private readonly AtlasDatabase _db;

    public AnalyzeViewModel(AtlasDatabase db, IAtlasNavigator navigator)
    {
        _db = db;
        Detail = new EntryDetailViewModel(navigator);
    }

    public EntryDetailViewModel Detail { get; }

    public ObservableCollection<AnalyzeHit> Results { get; } = new();
    public ObservableCollection<string> NotableLines { get; } = new();

    [ObservableProperty]
    private string _logText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasResults))]
    private AnalyzeHit? _selectedResult;

    [ObservableProperty]
    private string _statusText = "Paste a chunk of the SQL Server ERRORLOG and press Analyze.";

    public bool HasResults => Results.Count > 0;
    public bool HasNotableLines => NotableLines.Count > 0;

    partial void OnSelectedResultChanged(AnalyzeHit? value) => Detail.Guidance = value?.Guidance;

    [RelayCommand]
    private void Analyze()
    {
        Results.Clear();
        NotableLines.Clear();
        Detail.Guidance = null;

        var parsed = ErrorLogParser.Parse(LogText);
        foreach (var e in parsed.Errors)
        {
            var guidance = _db.Resolve(e.Number);
            Results.Add(new AnalyzeHit(e, guidance));
        }
        foreach (var n in parsed.NotableLines)
            NotableLines.Add(n);

        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(HasNotableLines));

        StatusText = parsed.Errors.Count switch
        {
            0 when parsed.NotableLines.Count > 0 => "No error numbers found, but some noteworthy lines were flagged below.",
            0 => "No SQL Server error numbers found in the pasted text.",
            1 => "1 error number found.",
            var n => $"{n} distinct error numbers found — highest severity first.",
        };

        SelectedResult = Results.FirstOrDefault();
    }

    [RelayCommand(CanExecute = nameof(HasResults))]
    private async Task ExportAnalysisAsync()
    {
        if (UiServices.Current is not { } ui || Results.Count == 0) return;
        var md = EntryExporter.AnalysisToMarkdown(
            $"{Results.Count} error(s) from pasted ERRORLOG",
            Results.Select(r => r.Guidance));
        var html = EntryExporter.ToHtml("SQL Error Atlas — ERRORLOG analysis", md);
        await ui.SaveTextFileAsync("errorlog-analysis.html", html, "HTML document", "html");
    }

    [RelayCommand]
    private void Clear()
    {
        LogText = "";
        Results.Clear();
        NotableLines.Clear();
        Detail.Guidance = null;
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(HasNotableLines));
        StatusText = "Paste a chunk of the SQL Server ERRORLOG and press Analyze.";
    }

    partial void OnLogTextChanged(string value) => ExportAnalysisCommand.NotifyCanExecuteChanged();
}

public sealed record AnalyzeHit(ParsedError Parsed, MessageGuidance Guidance)
{
    public int Number => Parsed.Number;
    public string SeverityText => Parsed.Severity is { } s ? $"Sev {s}" : "Sev ?";
    public string OccurrenceText => Parsed.Occurrences > 1 ? $"×{Parsed.Occurrences}" : "";
    public string Title => Guidance.Title;
    public string Urgency => Guidance.Urgency;
    public string KindText => Guidance.Kind switch
    {
        GuidanceKind.Playbook => "playbook",
        GuidanceKind.Group => "group guidance",
        _ => "not catalogued",
    };
}
