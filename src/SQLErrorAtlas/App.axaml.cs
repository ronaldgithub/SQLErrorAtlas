using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Threading;
using SQLErrorAtlas.Services;
using SQLErrorAtlas.ViewModels;
using SQLErrorAtlas.Views;

namespace SQLErrorAtlas;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            try
            {
                var dataPath = DataFileProvisioner.EnsureDataFile();
                var db = new AtlasDatabase(dataPath);
                var settingsService = new SettingsService();
                var settings = settingsService.Load();

                ApplyTheme(settings.Theme);

                var vm = new MainWindowViewModel(db, settingsService, settings, ApplyTheme, dataPath);
                var window = new MainWindow { DataContext = vm };

                vm.ShowAboutRequested += () =>
                {
                    var about = new AboutWindow { DataContext = vm.CreateAboutViewModel() };
                    about.ShowDialog(window);
                };

                desktop.MainWindow = window;
                desktop.Exit += (_, _) => db.Dispose();
            }
            catch (Exception ex)
            {
                desktop.MainWindow = StartupErrorWindow.For(ex);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    public static void ApplyTheme(AppTheme theme)
    {
        void Set()
        {
            if (Current is null) return;
            Current.RequestedThemeVariant = theme switch
            {
                AppTheme.Light => ThemeVariant.Light,
                AppTheme.Dark => ThemeVariant.Dark,
                _ => ThemeVariant.Default,
            };
        }

        if (Dispatcher.UIThread.CheckAccess()) Set();
        else Dispatcher.UIThread.Post(Set);
    }
}
