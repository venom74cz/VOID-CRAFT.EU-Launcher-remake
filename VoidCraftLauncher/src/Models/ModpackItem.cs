using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace VoidCraftLauncher.Models;

public partial class ModpackItem : ObservableObject
{
    public ModpackItem()
    {
        AvailableVersionOptions.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasAvailableVersionOptions));
    }

    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Author { get; set; } = "";
    public string IconUrl { get; set; } = "";
    public string Id { get; set; } = ""; // Project ID
    public string Source { get; set; } = ""; // "CurseForge", "Modrinth" or "Manual"
    public string WebLink { get; set; } = "";
    public long DownloadCount { get; set; }
    public string Slug { get; set; } = "";
    public string FileId { get; set; } = "";
    public string VersionId { get; set; } = "";
    public string DownloadUrl { get; set; } = "";

    // Custom Profile Support
    [ObservableProperty]
    private bool _isInstalled;

    [ObservableProperty]
    private bool _isEnabled = true;

    [ObservableProperty]
    private bool _isSelected;

    // Download progress
    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private string _downloadStatusLabel = "";

    [ObservableProperty]
    private bool _isLoadingVersionOptions;

    [ObservableProperty]
    private string _versionSelectionStatusLabel = "";

    [ObservableProperty]
    private ModInstallVersionOption? _selectedVersionOption;

    public ObservableCollection<ModInstallVersionOption> AvailableVersionOptions { get; } = new();

    public bool HasAvailableVersionOptions => AvailableVersionOptions.Count > 0;

    partial void OnSelectedVersionOptionChanged(ModInstallVersionOption? value)
    {
        OnPropertyChanged(nameof(SelectedVersionOptionLabel));
    }

    public string SelectedVersionOptionLabel => SelectedVersionOption?.Label ?? "Nejnovejsi kompatibilni";

    public void ReplaceVersionOptions(IEnumerable<ModInstallVersionOption> options)
    {
        var selectedKey = SelectedVersionOption?.IdentityKey;

        AvailableVersionOptions.Clear();
        foreach (var option in options)
        {
            AvailableVersionOptions.Add(option);
        }

        SelectedVersionOption = !string.IsNullOrWhiteSpace(selectedKey)
            ? AvailableVersionOptions.FirstOrDefault(option => option.IdentityKey == selectedKey) ?? AvailableVersionOptions.FirstOrDefault()
            : AvailableVersionOptions.FirstOrDefault();

        OnPropertyChanged(nameof(HasAvailableVersionOptions));
        OnPropertyChanged(nameof(SelectedVersionOptionLabel));
    }

    public string InstalledFileName { get; set; } = "";
}
