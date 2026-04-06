using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VoidCraftLauncher.Models;
using VoidCraftLauncher.Services;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace VoidCraftLauncher.ViewModels;

/// <summary>
/// Global settings, instance settings, options presets, potato mode.
/// </summary>
public partial class MainViewModel
{
    // ===== SETTINGS STATE =====

    [ObservableProperty]
    private InstanceConfig _currentModpackConfig;

    [ObservableProperty]
    private ObservableCollection<string> _optionsPresetNames = new();

    [ObservableProperty]
    private string _newOptionsPresetName = "";

    [ObservableProperty]
    private string? _selectedOptionsPresetName;

    // ===== SETTINGS COMMANDS =====

    [RelayCommand]
    public void SaveSettings()
    {
        _launcherService.SaveConfig(Config);
        Greeting = "Nastavení uloženo.";
        GoToHome(); 
        _ = Task.Delay(2000).ContinueWith(_ => 
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => Greeting = "Vítejte ve VOID-CRAFT Launcheru");
        });
    }

    [RelayCommand]
    public void GoToModpackSettings()
    {
        if (CurrentModpack == null) return;

        if (Config.InstanceOverrides.TryGetValue(CurrentModpack.Name, out var existingConfig))
        {
            CurrentModpackConfig = existingConfig;
        }
        else
        {
            CurrentModpackConfig = new InstanceConfig 
            { 
                ModpackName = CurrentModpack.Name,
                IsEnabled = true
            };
        }

        NormalizeCurrentModpackConfigDefaults();

        ReloadOptionsPresets();
        GoToInstanceDetail(CurrentModpack);
    }

    private void NormalizeCurrentModpackConfigDefaults()
    {
        if (CurrentModpackConfig == null || CurrentModpack == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(CurrentModpackConfig.ModpackName))
        {
            CurrentModpackConfig.ModpackName = CurrentModpack.Name;
        }

        if (!CurrentModpackConfig.OverrideEnableOptimizationFlags.HasValue)
        {
            CurrentModpackConfig.OverrideEnableOptimizationFlags = Config.EnableOptimizationFlags;
        }

        if (CurrentModpackConfig.OverrideEnableOptimizationFlags == true && CurrentModpackConfig.OverrideGcType == null)
        {
            CurrentModpackConfig.OverrideGcType = Config.SelectedGc;
        }
    }

    private void ReloadOptionsPresets()
    {
        var names = Config.OptionsPresets?
            .Select(kv => kv.Key)
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new System.Collections.Generic.List<string>();

        OptionsPresetNames = new ObservableCollection<string>(names);

        if (names.Count == 0)
        {
            SelectedOptionsPresetName = null;
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedOptionsPresetName) || !names.Contains(SelectedOptionsPresetName))
        {
            SelectedOptionsPresetName = names[0];
        }
    }

    [RelayCommand]
    public void SaveOptionsPreset()
    {
        if (CurrentModpack == null)
        {
            Greeting = "Nejdříve vyber modpack.";
            return;
        }

        var presetName = (NewOptionsPresetName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(presetName))
        {
            Greeting = "Zadej název presetu options.";
            return;
        }

        try
        {
            var modpackPath = _launcherService.GetModpackPath(CurrentModpack.Name);
            var optionsPath = Path.Combine(modpackPath, "options.txt");

            if (!File.Exists(optionsPath))
            {
                Greeting = "V modpacku nebyl nalezen options.txt.";
                return;
            }

            var content = File.ReadAllText(optionsPath);
            Config.OptionsPresets[presetName] = content;
            _launcherService.SaveConfig(Config);

            NewOptionsPresetName = "";
            ReloadOptionsPresets();
            SelectedOptionsPresetName = presetName;
            Greeting = $"Preset '{presetName}' uložen.";
        }
        catch (Exception ex)
        {
            LogService.Error("[SaveOptionsPreset] Failed", ex);
            Greeting = "Nepodařilo se uložit preset options.";
        }
    }

    [RelayCommand]
    public void LoadOptionsPresetToCurrentModpack()
    {
        if (CurrentModpack == null)
        {
            Greeting = "Nejdříve vyber modpack.";
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedOptionsPresetName))
        {
            Greeting = "Vyber preset options pro načtení.";
            return;
        }

        if (!Config.OptionsPresets.TryGetValue(SelectedOptionsPresetName, out var content))
        {
            Greeting = "Vybraný preset už neexistuje.";
            ReloadOptionsPresets();
            return;
        }

        try
        {
            var modpackPath = _launcherService.GetModpackPath(CurrentModpack.Name);
            Directory.CreateDirectory(modpackPath);
            var optionsPath = Path.Combine(modpackPath, "options.txt");
            File.WriteAllText(optionsPath, content);

            Greeting = $"Preset '{SelectedOptionsPresetName}' načten do modpacku {CurrentModpack.DisplayLabel}.";
        }
        catch (Exception ex)
        {
            LogService.Error("[LoadOptionsPresetToCurrentModpack] Failed", ex);
            Greeting = "Nepodařilo se načíst preset options do modpacku.";
        }
    }

    [RelayCommand]
    public void DeleteOptionsPreset()
    {
        if (string.IsNullOrWhiteSpace(SelectedOptionsPresetName))
        {
            Greeting = "Vyber preset, který chceš smazat.";
            return;
        }

        var presetName = SelectedOptionsPresetName;
        if (!Config.OptionsPresets.Remove(presetName))
        {
            Greeting = "Preset nebyl nalezen.";
            ReloadOptionsPresets();
            return;
        }

        try
        {
            _launcherService.SaveConfig(Config);
            ReloadOptionsPresets();
            Greeting = $"Preset '{presetName}' byl smazán.";
        }
        catch (Exception ex)
        {
            LogService.Error("[DeleteOptionsPreset] Failed", ex);
            Greeting = "Nepodařilo se smazat preset.";
        }
    }

    [RelayCommand]
    public void SaveModpackSettings()
    {
        if (CurrentModpackConfig == null) return;

        Config.InstanceOverrides[CurrentModpackConfig.ModpackName] = CurrentModpackConfig;
        _launcherService.SaveConfig(Config);
        Greeting = $"Nastavení pro {CurrentModpackConfig.ModpackName} uloženo.";
    }

    [RelayCommand]
    public void OpenPotatoConfig()
    {
        if (CurrentModpack == null) return;
        
        var modpackDir = _launcherService.GetModpackPath(CurrentModpack.Name);
        ModUtils.GetPotatoModList(modpackDir);
        
        var vm = new PotatoModsViewModel(modpackDir);
        var window = new VoidCraftLauncher.Views.PotatoModsWindow { DataContext = vm };
        vm.RequestClose += window.Close;
        
        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            window.ShowDialog(desktop.MainWindow);
        }
    }
}
