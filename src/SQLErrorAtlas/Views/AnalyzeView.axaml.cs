using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SQLErrorAtlas.Views;

public partial class AnalyzeView : UserControl
{
    public AnalyzeView()
    {
        AvaloniaXamlLoader.Load(this);
        // Land the caret in the paste box so the user can paste straight away.
        Loaded += (_, _) => this.FindControl<TextBox>("LogBox")?.Focus();
    }
}
