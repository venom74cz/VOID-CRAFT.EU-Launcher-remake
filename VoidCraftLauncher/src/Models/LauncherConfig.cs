using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using VoidCraftLauncher.Models.CreatorStudio;

namespace VoidCraftLauncher.Models;

public partial class LauncherConfig : ObservableObject
{
    
    // Default 12GB
    [ObservableProperty]
    private int _maxRamMb = 12288; 
    
    [ObservableProperty]
    private int _minRamMb = 512;

    [ObservableProperty]
    private bool _enableOptimizationFlags = true;
    
    [ObservableProperty]
    private string[]? _jvmArguments;

    [ObservableProperty]
    private GcType _selectedGc = GcType.G1GC;

    public Dictionary<string, InstanceConfig> InstanceOverrides { get; set; } = new();

    // Global options.txt presets (Name -> File Content)
    public Dictionary<string, string> OptionsPresets { get; set; } = new();

    [ObservableProperty]
    private string? _lastOfflineUsername;

    // Multi-account support
    public List<AccountProfile> Accounts { get; set; } = new();

    // User-defined custom servers for Server Hub
    public List<ServerInfo> CustomServers { get; set; } = new();

    // Recently played normal instances
    public List<string> RecentInstances { get; set; } = new();

    [ObservableProperty]
    private string _currentThemeId = "obsidian";

    [ObservableProperty]
    private string _preferredLanguageCode = "system";

    [ObservableProperty]
    private string _motionPreference = "system";

    [ObservableProperty]
    private string? _activeAccountId;

    public CreatorStudioPreferences CreatorStudio { get; set; } = new();
}

public enum GcType
{
    G1GC,
    ZGC,
    None
}
