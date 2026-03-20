using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
                desktop.MainWindow = new MainWindow
                {
                    DataContext = new MainViewModel()
                };
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