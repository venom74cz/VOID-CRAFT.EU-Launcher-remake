using Avalonia.Controls;
using Avalonia.Input;
using VoidCraftLauncher.ViewModels;
using VoidCraftLauncher.Models;

namespace VoidCraftLauncher.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void PointerPressedHandler(object? sender, PointerPressedEventArgs e)
        {
            var pointerProperties = e.GetCurrentPoint(this).Properties;
            if (!pointerProperties.IsLeftButtonPressed) return;

            // If the user clicked the Play button or its immediate container, do NOT open settings
            var source = e.Source as Control;
            while (source != null)
            {
                // Explicitly check for play button related elements
                if (source.Name == "PlayButtonContainer" || (source is Button b && b.Classes.Contains("PlayButton")))
                {
                    e.Handled = false; // Let the button handle its own click
                    return;
                }
                if (source == sender) break;
                source = source.Parent as Control;
            }

            if (sender is Control control && control.DataContext is ModpackInfo modpack)
            {
                if (DataContext is MainViewModel vm)
                {
                    vm.SelectAndConfigureCommand.Execute(modpack);
                }
            }
        }
    }
}
