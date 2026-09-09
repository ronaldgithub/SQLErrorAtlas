using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SQLErrorAtlas.Models;
using SQLErrorAtlas.Services;

namespace SQLErrorAtlas.ViewModels;

/// <summary>Browse the 32 groups (ordered by urgency) and drill into their messages.</summary>
public partial class BrowseViewModel : ViewModelBase
{
    private static readonly string[] UrgencyOrder = { "Emergency", "High", "Medium", "Low", "Info" };

    private readonly AtlasDatabase _db;
    private readonly IAtlasNavigator _navigator;

    public BrowseViewModel(AtlasDatabase db, IAtlasNavigator navigator)
    {
        _db = db;
        _navigator = navigator;
        Detail = new EntryDetailViewModel(navigator);

        Groups = new ObservableCollection<GroupSummary>(
            _db.GetGroups().OrderBy(g => Rank(g.Urgency)).ThenBy(g => g.GroupId));
    }

    public EntryDetailViewModel Detail { get; }

    public ObservableCollection<GroupSummary> Groups { get; }
    public ObservableCollection<PlaybookRef> Playbooks { get; } = new();
    public ObservableCollection<CatalogRef> Messages { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasGroup), nameof(GroupName), nameof(WhatItMeans),
        nameof(Investigate), nameof(Fix), nameof(KeyTools), nameof(RelatedIssues), nameof(HasRelatedIssues))]
    private GroupDetail? _currentGroup;

    [ObservableProperty]
    private GroupSummary? _selectedGroup;

    [ObservableProperty]
    private CatalogRef? _selectedMessage;

    [ObservableProperty]
    private PlaybookRef? _selectedPlaybook;

    public bool HasGroup => CurrentGroup is not null;
    public string GroupName => CurrentGroup?.Summary.GroupName ?? "";
    public string WhatItMeans => CurrentGroup?.WhatItMeans ?? "";
    public string Investigate => CurrentGroup?.Investigate ?? "";
    public string Fix => CurrentGroup?.Fix ?? "";
    public string KeyTools => CurrentGroup?.KeyTools ?? "";
    public bool HasRelatedIssues => CurrentGroup?.RelatedIssues.Count > 0;
    public IReadOnlyList<RelatedIssue> RelatedIssues => CurrentGroup?.RelatedIssues ?? Array.Empty<RelatedIssue>();

    partial void OnSelectedGroupChanged(GroupSummary? value)
    {
        if (value is null) return;
        LoadGroup(value.GroupId);
    }

    partial void OnSelectedMessageChanged(CatalogRef? value)
    {
        if (value is not null) Detail.Guidance = _db.Resolve(value.MessageId);
    }

    partial void OnSelectedPlaybookChanged(PlaybookRef? value)
    {
        if (value is not null) Detail.Guidance = _db.Resolve(value.MessageId);
    }

    public void ShowGroup(int groupId)
    {
        SelectedGroup = Groups.FirstOrDefault(g => g.GroupId == groupId);
        LoadGroup(groupId);
    }

    private void LoadGroup(int groupId)
    {
        CurrentGroup = _db.GetGroup(groupId);
        Playbooks.Clear();
        Messages.Clear();
        Detail.Guidance = null;
        if (CurrentGroup is null) return;
        foreach (var p in CurrentGroup.Playbooks) Playbooks.Add(p);
        foreach (var m in CurrentGroup.Messages) Messages.Add(m);
    }

    [RelayCommand]
    private void OpenIssue(RelatedIssue? issue)
    {
        if (issue is not null) _navigator.ShowIssue(issue.IssueId);
    }

    private static int Rank(string urgency)
    {
        var i = Array.IndexOf(UrgencyOrder, urgency);
        return i < 0 ? UrgencyOrder.Length : i;
    }
}
