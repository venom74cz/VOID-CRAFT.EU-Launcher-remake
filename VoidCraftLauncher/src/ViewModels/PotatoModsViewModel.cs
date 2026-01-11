using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using VoidCraftLauncher.Models;
using VoidCraftLauncher.Services;

namespace VoidCraftLauncher.ViewModels
{
    public partial class PotatoModsViewModel : ViewModelBase
    {
        private readonly string _modpackPath;
        private readonly string _modsPath;
        private readonly string _configPath;
        private readonly string _metadataPath;
        private Dictionary<string, ModMetadata> _metadataDict = new();
        private List<ModItemViewModel> _allMods = new();

        [ObservableProperty]
        private string _searchQuery = "";

        [ObservableProperty]
        private ObservableCollection<ModItemViewModel> _filteredMods = new();

        [ObservableProperty]
        private bool _isLoading = true;

        public event Action? RequestClose;

        public PotatoModsViewModel(string modpackPath)
        {
            _modpackPath = modpackPath;
            _modsPath = Path.Combine(modpackPath, "mods");
            _configPath = Path.Combine(modpackPath, "potato_mods.json");
            _metadataPath = Path.Combine(modpackPath, "mods_metadata.json");

            LoadData();
        }

        private void LoadData()
        {
            IsLoading = true;
            try
            {
                // 1. Load Metadata
                if (File.Exists(_metadataPath))
                {
                    try
                    {
                        var json = File.ReadAllText(_metadataPath);
                        var list = JsonSerializer.Deserialize<List<ModMetadata>>(json);
                        if (list != null)
                        {
                            foreach (var item in list)
                            {
                                if (!string.IsNullOrEmpty(item.FileName))
                                    _metadataDict[item.FileName] = item;
                            }
                        }
                    }
                    catch { /* Log error */ }
                }

                // 2. Load Existing Blacklist
                var blacklist = ModUtils.GetPotatoModList(_modpackPath);
                var validBlacklist = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach(var item in blacklist) validBlacklist.Add(item);

                // 3. Scan Mods Folder
                if (!Directory.Exists(_modsPath)) Directory.CreateDirectory(_modsPath);

                var files = Directory.GetFiles(_modsPath);
                _allMods.Clear();

                foreach (var file in files)
                {
                    var fileName = Path.GetFileName(file);
                    var isJar = fileName.EndsWith(".jar", StringComparison.OrdinalIgnoreCase);
                    var isDisabled = fileName.EndsWith(".jar.disabled", StringComparison.OrdinalIgnoreCase);

                    if (!isJar && !isDisabled) continue;

                    // Normalize filename (remove .disabled)
                    var cleanName = isDisabled ? fileName.Substring(0, fileName.Length - ".disabled".Length) : fileName;
                    
                    // Check if blacklisted (by exact name or partial match as per ModUtils logic)
                    // ModUtils uses "Contains", so we should replicate that logic to show currently affected mods?
                    // BUT, the user wants to "click mods to turn off". 
                    // To support the new explicit "list of files", maybe we should switch ModUtils to exact match if we use this tool?
                    // OR we just check if it matches any pattern.
                    
                    // For the UI, let's assume validBlacklist contains Patterns.
                    // If a file matches a pattern, it is "Selected" (to be disabled).
                    bool isSelected = IsMatch(cleanName, blacklist);

                    // Get Metadata
                    _metadataDict.TryGetValue(cleanName, out var meta);

                    var cleanNameLower = cleanName.ToLowerInvariant();
                    // Guess side if not known?
                    string side = "Unknown";
                    string description = "";
                    List<string> tags = new();

                    if (meta != null)
                    {
                        side = meta.Categories.Contains("Server Utility") || meta.Categories.Contains("Server Only") ? "Server" : "Client/Both";
                        if (meta.Categories.Any(c => c.Contains("Performance"))) tags.Add("Performance");
                        if (meta.Categories.Any(c => c.Contains("Cosmetic") || c.Contains("Decoration"))) tags.Add("Visual");
                        description = meta.Summary;
                    }
                    
                    var modItem = new ModItemViewModel
                    {
                        FileName = cleanName,
                        DisplayName = meta?.Name ?? cleanName,
                        Description = description,
                        IsSelected = isSelected,
                        Categories = string.Join(", ", tags),
                        Side = side
                    };

                    _allMods.Add(modItem);
                }

                Refilter();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private bool IsMatch(string fileName, List<string> patterns)
        {
            var nameLower = fileName.ToLowerInvariant();
            return patterns.Any(p => nameLower.Contains(p.ToLowerInvariant()));
        }

        partial void OnSearchQueryChanged(string value)
        {
            Refilter();
        }

        private void Refilter()
        {
            if (string.IsNullOrWhiteSpace(SearchQuery))
            {
                FilteredMods = new ObservableCollection<ModItemViewModel>(_allMods.OrderBy(x => x.DisplayName));
            }
            else
            {
                var q = SearchQuery.ToLowerInvariant();
                var filtered = _allMods
                    .Where(m => m.DisplayName.ToLowerInvariant().Contains(q) || m.FileName.ToLowerInvariant().Contains(q))
                    .OrderBy(x => x.DisplayName);
                FilteredMods = new ObservableCollection<ModItemViewModel>(filtered);
            }
        }

        [RelayCommand]
        public void Save()
        {
            // Collect all selected mods
            // We'll save the exact filenames (or patterns?). Users prefer exact control usually.
            // But legacy support uses patterns "irisshaders".
            // Let's stick to EXACT FILENAMES for new entries generated by this UI to avoid accidental collateral damage,
            // UNLESS the user explicitly entered a pattern manually (not supported here).
            // Actually, if we save "iris-1.2.3.jar", updating the mod to "iris-1.2.4.jar" will break the blacklist.
            // Ideally we save the "Slug" or a smart pattern. But we don't have slugs easily unless we use metadata.
            
            // Compromise: Save the filename, but maybe strip version if possible? Too risky.
            // Let's save the full filename for now. The user can "update mod list" if they update mods.
            
            // Save simple list of patterns.
            // If we have a Slug, use it (it's robust across versions).
            // If not, use filename (less robust but works).
            
            var patternsToSave = new List<string>();
            foreach (var mod in _allMods)
            {
                if (mod.IsSelected)
                {
                    // Find metadata to get slug
                    if (_metadataDict.TryGetValue(mod.FileName, out var meta) && !string.IsNullOrEmpty(meta.Slug))
                    {
                        patternsToSave.Add(meta.Slug);
                    }
                    else
                    {
                        patternsToSave.Add(mod.FileName);
                    }
                }
            }
            
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(patternsToSave, options);
                File.WriteAllText(_configPath, json);
                
                RequestClose?.Invoke();
            }
            catch (Exception ex)
            {
                // Error handling
            }
        }

        [RelayCommand]
        public void Cancel()
        {
            RequestClose?.Invoke();
        }
    }

    public partial class ModItemViewModel : ObservableObject
    {
        public string FileName { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Description { get; set; } = "";
        public string Side { get; set; } = "";
        public string Categories { get; set; } = ""; // Display string

        [ObservableProperty]
        private bool _isSelected;
    }
}
