using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SQLErrorAtlas.Services;

namespace SQLErrorAtlas.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
        Opened += (_, _) => UiServices.Current = new UiServices(this);
        AddHandler(KeyDownEvent, OnKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            if (DataContext is ViewModels.MainWindowViewModel vm)
                vm.SelectedTabIndex = 3;
            Dispatcher.UIThread.Post(() =>
            {
                foreach (var box in this.GetVisualDescendants())
                    if (box is TextBox { Name: "SearchBox" } tb)
                    {
                        tb.Focus();
                        break;
                    }
            }, DispatcherPriority.Background);
            e.Handled = true;
        }
    }
}
