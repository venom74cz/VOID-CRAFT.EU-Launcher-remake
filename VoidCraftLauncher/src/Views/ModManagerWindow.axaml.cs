using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System;
using System.Linq;
using VoidCraftLauncher.ViewModels;

namespace VoidCraftLauncher.Views
{
    public partial class ModManagerWindow : Window
    {
        public ModManagerWindow()
        {
            InitializeComponent();
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            if (DataContext is ModManagerViewModel vm)
            {
                vm.RequestClose += Close;
            }
        }

        private async void OnAddModsClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is not ModManagerViewModel vm || StorageProvider == null)
            {
                return;
            }

            var results = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Vyber mody",
                AllowMultiple = true,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Minecraft Mods")
                    {
                        Patterns = new[] { "*.jar", "*.jar.disabled" },
                        MimeTypes = new[] { "application/java-archive" }
                    }
                }
            });

            var paths = results
                .Select(file => file.TryGetLocalPath())
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Cast<string>()
                .ToList();

            if (paths.Count > 0)
            {
                vm.AddModsFromPaths(paths);
            }
        }
    }
}
