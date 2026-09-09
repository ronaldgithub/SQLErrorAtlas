using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SQLErrorAtlas.Models;
using SQLErrorAtlas.Services;

namespace SQLErrorAtlas.ViewModels;

/// <summary>Shared right-hand detail pane: shows resolved guidance for one error number.</summary>
public partial class EntryDetailViewModel : ViewModelBase
{
    private readonly IAtlasNavigator _navigator;

    public EntryDetailViewModel(IAtlasNavigator navigator) => _navigator = navigator;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasGuidance), nameof(HeaderText), nameof(Meaning), nameof(Investigate),
        nameof(Fix), nameof(KeyTools), nameof(HasKeyTools), nameof(DocsUrl), nameof(HasDocsUrl),
        nameof(SourceNote), nameof(RelatedIssues), nameof(HasRelatedIssues), nameof(Urgency))]
    private MessageGuidance? _guidance;

    public bool HasGuidance => Guidance is not null;

    public string HeaderText => Guidance is null ? "" :
        Guidance.MessageId is { } id ? $"{id} — {Guidance.Title}" : Guidance.Title;

    public string Urgency => Guidance?.Urgency ?? "";

    public string Meaning => Guidance?.Meaning ?? "";
    public string Investigate => Guidance?.Investigate ?? "";
    public string Fix => Guidance?.Fix ?? "";
    public string KeyTools => Guidance?.KeyTools ?? "";
    public bool HasKeyTools => Guidance?.HasKeyTools == true;
    public string DocsUrl => Guidance?.DocsUrl ?? "";
    public bool HasDocsUrl => Guidance?.HasDocsUrl == true;
    public bool HasRelatedIssues => Guidance?.HasRelatedIssues == true;
    public IReadOnlyList<RelatedIssue> RelatedIssues => Guidance?.RelatedIssues ?? Array.Empty<RelatedIssue>();

    public string SourceNote => Guidance switch
    {
        null => "",
        { Kind: GuidanceKind.Playbook, Status: "stub" } => "Playbook entry — stub (skeleton guidance, not yet fully written).",
        { Kind: GuidanceKind.Playbook } => "Message-specific playbook entry.",
        { Kind: GuidanceKind.Group } => "No message-specific playbook entry — showing group-level guidance.",
        { Kind: GuidanceKind.Unknown } => "Not found in the message catalog.",
        _ => "",
    };

    [RelayCommand(CanExecute = nameof(HasGuidance))]
    private async Task CopyMarkdownAsync()
    {
        if (Guidance is null || UiServices.Current is null) return;
        await UiServices.Current.SetClipboardTextAsync(EntryExporter.ToMarkdown(Guidance));
    }

    [RelayCommand(CanExecute = nameof(HasGuidance))]
    private async Task SaveHtmlAsync()
    {
        if (Guidance is null || UiServices.Current is null) return;
        var md = EntryExporter.ToMarkdown(Guidance);
        var html = EntryExporter.ToHtml(HeaderText, md);
        var name = (Guidance.MessageId?.ToString() ?? "guidance") + ".html";
        await UiServices.Current.SaveTextFileAsync(name, html, "HTML document", "html");
    }

    [RelayCommand(CanExecute = nameof(HasDocsUrl))]
    private async Task OpenDocsAsync()
    {
        if (UiServices.Current is { } ui && Guidance?.DocsUrl is { } url)
            await ui.OpenUrlAsync(url);
    }

    [RelayCommand]
    private void OpenIssue(RelatedIssue? issue)
    {
        if (issue is not null) _navigator.ShowIssue(issue.IssueId);
    }

    partial void OnGuidanceChanged(MessageGuidance? value)
    {
        CopyMarkdownCommand.NotifyCanExecuteChanged();
        SaveHtmlCommand.NotifyCanExecuteChanged();
        OpenDocsCommand.NotifyCanExecuteChanged();
    }
}
