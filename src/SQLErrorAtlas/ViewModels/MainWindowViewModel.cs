using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SQLErrorAtlas.Models;
using SQLErrorAtlas.Services;

namespace SQLErrorAtlas.ViewModels;

public partial class MainWindowViewModel : ViewModelBase, IAtlasNavigator
{
    private readonly AtlasDatabase _db;
    private readonly SettingsService _settings;
    private readonly Action<AppTheme> _applyTheme;
    private readonly AtlasMeta _meta;
    private readonly string _dataPath;

    public MainWindowViewModel(
        AtlasDatabase db,
        SettingsService settings,
        AppSettings current,
        Action<AppTheme> applyTheme,
        string dataPath)
    {
        _db = db;
        _settings = settings;
        _applyTheme = applyTheme;
        _dataPath = dataPath;
        _meta = db.GetMeta();
        _selectedTheme = current.Theme;

        Analyze = new AnalyzeViewModel(db, this);
        Lookup = new LookupViewModel(db, this);
        Browse = new BrowseViewModel(db, this);
        SearchTab = new SearchViewModel(db, this);
        Issues = new IssuesViewModel(db);
    }

    public AnalyzeViewModel Analyze { get; }
    public LookupViewModel Lookup { get; }
    public BrowseViewModel Browse { get; }
    public SearchViewModel SearchTab { get; }
    public IssuesViewModel Issues { get; }

    public event Action? ShowAboutRequested;

    public AppTheme[] ThemeOptions { get; } = { AppTheme.Dark, AppTheme.Light, AppTheme.System };

    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private AppTheme _selectedTheme;

    public string DataAsOfText =>
        _meta.GeneratedUtc == default
            ? "offline dataset"
            : $"Data as of {_meta.GeneratedUtc.ToLocalTime():yyyy-MM-dd}  ·  {_meta.Rows("error_message_catalog"):N0} messages, {_meta.Rows("error_message_playbook")} playbook entries";

    partial void OnSelectedThemeChanged(AppTheme value)
    {
        _applyTheme(value);
        var s = _settings.Load();
        s.Theme = value;
        _settings.Save(s);
    }

    [RelayCommand]
    private void ShowAbout() => ShowAboutRequested?.Invoke();

    [RelayCommand]
    private void FocusSearch() => SelectedTabIndex = 3;

    public AboutViewModel CreateAboutViewModel() => new(_meta, _dataPath);

    // ---- IAtlasNavigator ----

    public void ShowIssue(int issueId)
    {
        Issues.ShowIssue(issueId);
        SelectedTabIndex = 4;
    }

    public void ShowGroup(int groupId)
    {
        Browse.ShowGroup(groupId);
        SelectedTabIndex = 2;
    }

    public void ShowGuidance(int errorNumber)
    {
        Lookup.LookupNumber(errorNumber);
        SelectedTabIndex = 1;
    }

    public void ShowSearch(string query)
    {
        SearchTab.Query = query;
        SelectedTabIndex = 3;
    }
}
