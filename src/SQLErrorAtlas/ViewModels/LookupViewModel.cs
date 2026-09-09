using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SQLErrorAtlas.Services;

namespace SQLErrorAtlas.ViewModels;

/// <summary>Type an error number, get its guidance.</summary>
public partial class LookupViewModel : ViewModelBase
{
    private readonly AtlasDatabase _db;

    public LookupViewModel(AtlasDatabase db, IAtlasNavigator navigator)
    {
        _db = db;
        Detail = new EntryDetailViewModel(navigator);
    }

    public EntryDetailViewModel Detail { get; }

    [ObservableProperty]
    private string _numberText = "";

    [ObservableProperty]
    private string _hint = "Enter a SQL Server error number (for example 823, 9002, 17883).";

    [RelayCommand]
    private void Lookup()
    {
        if (!int.TryParse(NumberText.Trim(), out var number) || number <= 0)
        {
            Detail.Guidance = null;
            Hint = "That is not a valid error number.";
            return;
        }
        Detail.Guidance = _db.Resolve(number);
        Hint = "";
    }

    public void LookupNumber(int number)
    {
        NumberText = number.ToString();
        Lookup();
    }
}
