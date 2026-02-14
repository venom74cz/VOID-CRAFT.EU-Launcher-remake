using CommunityToolkit.Mvvm.ComponentModel;
using System.Text.Json.Serialization;

namespace VoidCraftLauncher.Models;

public partial class LauncherConfig : ObservableObject
{
    [ObservableProperty]
    private string? _javaPath;
    
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

    public System.Collections.Generic.Dictionary<string, InstanceConfig> InstanceOverrides { get; set; } = new();

    // Global options.txt presets (Name -> File Content)
    public System.Collections.Generic.Dictionary<string, string> OptionsPresets { get; set; } = new();

    [ObservableProperty]
    private string? _lastOfflineUsername;
}

public enum GcType
{
    G1GC,
    ZGC
}
