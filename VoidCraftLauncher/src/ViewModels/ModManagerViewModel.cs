using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace VoidCraftLauncher.ViewModels
{
    public partial class ModManagerViewModel : ViewModelBase
    {
        private readonly string _modsPath;
        private readonly ObservableCollection<ManagedModItemViewModel> _allMods = new();

        [ObservableProperty]
        private string _searchQuery = "";

        [ObservableProperty]
        private ObservableCollection<ManagedModItemViewModel> _filteredMods = new();

        [ObservableProperty]
        private bool _isLoading;

        public string ModpackName { get; }

        public event Action? RequestClose;

        public ModManagerViewModel(string modpackName, string modpackPath)
        {
            ModpackName = modpackName;
            _modsPath = Path.Combine(modpackPath, "mods");

            Directory.CreateDirectory(_modsPath);
            ReloadMods();
        }

        partial void OnSearchQueryChanged(string value)
        {
            Refilter();
        }

        [RelayCommand]
        public void ReloadMods()
        {
            IsLoading = true;
            try
            {
                _allMods.Clear();

                if (!Directory.Exists(_modsPath))
                {
                    Directory.CreateDirectory(_modsPath);
                }

                var files = Directory
                    .GetFiles(_modsPath)
                    .Where(path =>
                    {
                        var name = Path.GetFileName(path);
                        return name.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) ||
                               name.EndsWith(".jar.disabled", StringComparison.OrdinalIgnoreCase);
                    })
                    .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                    .ToList();

                foreach (var file in files)
                {
                    var fileName = Path.GetFileName(file);
                    var isEnabled = fileName.EndsWith(".jar", StringComparison.OrdinalIgnoreCase);
                    var baseName = isEnabled
                        ? fileName
                        : fileName[..^".disabled".Length];

                    var fileInfo = new FileInfo(file);
                    _allMods.Add(new ManagedModItemViewModel
                    {
                        BaseFileName = baseName,
                        DisplayName = Path.GetFileNameWithoutExtension(baseName),
                        IsEnabled = isEnabled,
                        SizeText = FormatSize(fileInfo.Length)
                    });
                }

                Refilter();
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        public void ToggleModState(ManagedModItemViewModel? mod)
        {
            if (mod == null)
            {
                return;
            }

            var enabledPath = Path.Combine(_modsPath, mod.BaseFileName);
            var disabledPath = enabledPath + ".disabled";

            try
            {
                if (mod.IsEnabled)
                {
                    if (File.Exists(disabledPath))
                    {
                        File.Delete(disabledPath);
                    }

                    if (File.Exists(enabledPath))
                    {
                        File.Move(enabledPath, disabledPath);
                    }

                    mod.IsEnabled = false;
                }
                else
                {
                    if (File.Exists(enabledPath))
                    {
                        File.Delete(enabledPath);
                    }

                    if (File.Exists(disabledPath))
                    {
                        File.Move(disabledPath, enabledPath);
                    }

                    mod.IsEnabled = true;
                }
            }
            catch
            {
                ReloadMods();
            }
        }

        public void AddModsFromPaths(System.Collections.Generic.IEnumerable<string> sourceFiles)
        {
            foreach (var sourceFile in sourceFiles)
            {
                if (!File.Exists(sourceFile))
                {
                    continue;
                }

                var fileName = Path.GetFileName(sourceFile);
                var isValid = fileName.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) ||
                              fileName.EndsWith(".jar.disabled", StringComparison.OrdinalIgnoreCase);
                if (!isValid)
                {
                    continue;
                }

                var destinationPath = Path.Combine(_modsPath, fileName);
                File.Copy(sourceFile, destinationPath, overwrite: true);
            }

            ReloadMods();
        }

        [RelayCommand]
        public void Close()
        {
            RequestClose?.Invoke();
        }

        private void Refilter()
        {
            var query = SearchQuery?.Trim();
            if (string.IsNullOrWhiteSpace(query))
            {
                FilteredMods = new ObservableCollection<ManagedModItemViewModel>(_allMods);
                return;
            }

            var lowered = query.ToLowerInvariant();
            var filtered = _allMods.Where(mod =>
                mod.DisplayName.ToLowerInvariant().Contains(lowered) ||
                mod.BaseFileName.ToLowerInvariant().Contains(lowered));

            FilteredMods = new ObservableCollection<ManagedModItemViewModel>(filtered);
        }

        private static string FormatSize(long bytes)
        {
            if (bytes >= 1024 * 1024)
            {
                return $"{bytes / 1024d / 1024d:0.##} MB";
            }

            if (bytes >= 1024)
            {
                return $"{bytes / 1024d:0.##} KB";
            }

            return $"{bytes} B";
        }
    }

    public partial class ManagedModItemViewModel : ObservableObject
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusText))]
        [NotifyPropertyChangedFor(nameof(ToggleButtonText))]
        private bool _isEnabled;

        public string BaseFileName { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string SizeText { get; set; } = "";

        public string StatusText => IsEnabled ? "Zapnuto" : "Vypnuto";
        public string ToggleButtonText => IsEnabled ? "Vypnout" : "Zapnout";
    }
}
