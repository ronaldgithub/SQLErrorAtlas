using Avalonia.Controls;
using Avalonia.Layout;

namespace SQLErrorAtlas.Views;

/// <summary>Shown instead of the main window if the dataset fails to load on startup.</summary>
public static class StartupErrorWindow
{
    public static Window For(Exception ex) => new()
    {
        Title = "SQL Error Atlas — startup problem",
        Width = 560,
        Height = 320,
        WindowStartupLocation = WindowStartupLocation.CenterScreen,
        Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(20),
            Spacing = 10,
            Children =
            {
                new TextBlock
                {
                    Text = "SQL Error Atlas could not open its dataset.",
                    FontWeight = Avalonia.Media.FontWeight.SemiBold,
                    FontSize = 16,
                },
                new TextBlock
                {
                    Text = "Try reinstalling. If the problem persists, open an issue on GitHub with the detail below.",
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                },
                new ScrollViewer
                {
                    MaxHeight = 180,
                    Content = new SelectableTextBlock
                    {
                        Text = ex.ToString(),
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                        FontFamily = new Avalonia.Media.FontFamily("Consolas, Menlo, monospace"),
                        FontSize = 12,
                    },
                },
            },
        },
    };
}
