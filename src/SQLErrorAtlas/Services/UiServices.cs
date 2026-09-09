using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;

namespace SQLErrorAtlas.Services;

/// <summary>
/// Thin bridge from view models to window-scoped Avalonia services (clipboard,
/// save-file dialog, opening a URL). Set once when the main window opens.
/// </summary>
public sealed class UiServices
{
    private readonly TopLevel _topLevel;

    public UiServices(TopLevel topLevel) => _topLevel = topLevel;

    public static UiServices? Current { get; set; }

    public async Task SetClipboardTextAsync(string text)
    {
        if (_topLevel.Clipboard is { } cb)
            await cb.SetTextAsync(text);
    }

    public async Task<string?> SaveTextFileAsync(string suggestedName, string text,
        string filterName, string extension)
    {
        var file = await _topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = suggestedName,
            DefaultExtension = extension,
            FileTypeChoices = new[]
            {
                new FilePickerFileType(filterName) { Patterns = new[] { "*." + extension } },
            },
        });
        if (file is null) return null;

        await using var stream = await file.OpenWriteAsync();
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(text);
        return file.Name;
    }

    public async Task OpenUrlAsync(string url)
    {
        try
        {
            await _topLevel.Launcher.LaunchUriAsync(new Uri(url));
        }
        catch
        {
            // no browser / bad URL — nothing sensible to do offline
        }
    }
}
