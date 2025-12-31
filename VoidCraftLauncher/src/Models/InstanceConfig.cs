using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace VoidCraftLauncher.Models;

public partial class InstanceConfig : ObservableObject
{
    public string ModpackName { get; set; } = "";

    [ObservableProperty]
    private string? _overrideJavaPath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RamSliderValue))]
    private int? _overrideMaxRamMb;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OptimizationFlagsHelper))]
    private bool? _overrideEnableOptimizationFlags;

    [ObservableProperty]
    private GcType? _overrideGcType;

    [ObservableProperty]
    private bool _isEnabled = true; // "Zapínat/Vypnout"
    
    // UI Helpers (Not serialized if needed, but useful)
    [JsonIgnore]
    public bool HasRamOverride => OverrideMaxRamMb.HasValue;
    
    [JsonIgnore]
    public bool HasJavaOverride => !string.IsNullOrEmpty(OverrideJavaPath);

    [System.Text.Json.Serialization.JsonIgnore]
    public double RamSliderValue
    {
        get => OverrideMaxRamMb ?? 2048; // Default to min if null
        set
        {
            if (Math.Abs(value - (OverrideMaxRamMb ?? 0)) > 1) 
            {
                OverrideMaxRamMb = (int)value;
                OnPropertyChanged(nameof(RamSliderValue));
                OnPropertyChanged(nameof(OverrideMaxRamMb));
            }
        }
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public bool OptimizationFlagsHelper
    {
        get => OverrideEnableOptimizationFlags ?? true; // Default true if null
        set
        {
            if (OverrideEnableOptimizationFlags != value)
            {
                OverrideEnableOptimizationFlags = value;
            }
        }
    }
}
