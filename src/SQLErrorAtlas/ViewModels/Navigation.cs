namespace SQLErrorAtlas.ViewModels;

/// <summary>Cross-tab navigation, implemented by <see cref="MainWindowViewModel"/>.</summary>
public interface IAtlasNavigator
{
    void ShowIssue(int issueId);
    void ShowGroup(int groupId);
    void ShowGuidance(int errorNumber);
    void ShowSearch(string query);
}
