using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SQLErrorAtlas.Services;

namespace SQLErrorAtlas.ViewModels;

public abstract partial class ViewModelBase : ObservableObject
{
    /// <summary>Copy an arbitrary string (a guidance section, a URL, …) to the clipboard.</summary>
    [RelayCommand]
    protected async Task CopyText(string? text)
    {
        if (!string.IsNullOrWhiteSpace(text) && UiServices.Current is { } ui)
            await ui.SetClipboardTextAsync(text);
    }
}
