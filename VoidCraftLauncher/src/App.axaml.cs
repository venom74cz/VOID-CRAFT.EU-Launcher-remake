using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VoidCraftLauncher.Services;
using VoidCraftLauncher.ViewModels;
using VoidCraftLauncher.Views;

namespace VoidCraftLauncher
{
    public partial class TrayViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _isTrayIconVisible;

        [RelayCommand]
        private void RestoreWindow()
        {
            App.RestoreMainWindow();
        }
    }

    public partial class App : Application
    {
        public static TrayViewModel TrayState { get; } = new();

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Initialize DI container before creating the ViewModel
                var services = ServiceLocator.Initialize();
                var themeEngine = new ThemeEngine(this);
                services.Register(themeEngine);
                var launcherService = services.Resolve<LauncherService>();
                var localizationService = services.Resolve<LocalizationService>();
                Models.LauncherConfig config;

                try
                {
                    config = launcherService.LoadConfig();
                    localizationService.ApplyConfiguredLanguage(config.PreferredLanguageCode);
                    themeEngine.ApplyTheme(config.CurrentThemeId);
                }
                catch
                {
                    config = new Models.LauncherConfig();
                    localizationService.ApplyConfiguredLanguage(LocalizationService.SystemLanguageCode);
                    themeEngine.ApplyTheme("obsidian");
                }

                var mainWindow = new MainWindow
                {
                    DataContext = new MainViewModel()
                };

                themeEngine.ApplyMotionPreference(config.MotionPreference, mainWindow);
                desktop.MainWindow = mainWindow;
                desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            }

            base.OnFrameworkInitializationCompleted();
        }

        public static void MinimizeToTray()
        {
            if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var window = desktop.MainWindow;
                if (window != null)
                {
                    TrayState.IsTrayIconVisible = true;
                    window.Hide();
                }
            }
        }

        public static void RestoreMainWindow()
        {
            if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var window = desktop.MainWindow;
                if (window != null)
                {
                    window.Show();
                    window.WindowState = WindowState.Normal;
                    window.Activate();
                    TrayState.IsTrayIconVisible = false;
                }
            }
        }
    }
}