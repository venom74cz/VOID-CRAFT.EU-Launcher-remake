using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;
using VoidCraftLauncher.Models;
using VoidCraftLauncher.Services;

namespace VoidCraftLauncher.ViewModels;

public partial class MainViewModel
{
    private bool _isUpdatingMotionPreferenceSelection;

    public ObservableCollection<ThemeInfo> ThemeOptions { get; } = new();

    public ObservableCollection<SelectionOption> MotionPreferenceOptions { get; } = new();

    [ObservableProperty]
    private SelectionOption? _selectedMotionPreferenceOption;

    public ThemeInfo CurrentTheme => ThemeOptions.FirstOrDefault(theme => theme.IsActive)
        ?? ThemeOptions.FirstOrDefault()
        ?? new ThemeInfo
        {
            Id = "obsidian",
            Name = "Obsidian",
            Description = "Výchozí motiv launcheru."
        };

    public int BuiltInThemeCount => ThemeOptions.Count;

    public string MotionPreferenceCardTitle => "Motion a dostupnost";

    public string MotionPreferenceCardHint => "Reduced-motion režim vypne launcher animace a zklidní přechody napříč shellem i overlayi.";

    public string MotionPreferenceStatus => _themeEngine.IsReducedMotionActive
        ? "Reduced motion je aktivní"
        : "Plný motion systém je aktivní";

    public string MotionPreferenceSystemStatus => ThemeEngine.IsSystemReducedMotionEnabled()
        ? "Systém právě požaduje omezené animace. Volba System to převezme automaticky."
        : "Systémové preference aktuálně animace povolují. Volba System ponechá launcher v plném motion režimu.";

    private void InitializeThemeSurface()
    {
        if (!_themeEngine.ApplyTheme(Config.CurrentThemeId))
        {
            Config.CurrentThemeId = "obsidian";
            _themeEngine.ApplyTheme("obsidian");
            _launcherService.SaveConfig(Config);
        }

        InitializeMotionSurface();
        RefreshThemeOptions();
    }

    partial void OnSelectedMotionPreferenceOptionChanged(SelectionOption? value)
    {
        if (_isUpdatingMotionPreferenceSelection || value == null)
            return;

        var mainWindow = (Avalonia.Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (!_themeEngine.ApplyMotionPreference(value.Id, mainWindow))
            return;

        Config.MotionPreference = value.Id;
        _launcherService.SaveConfig(Config);
        NotifyMotionPreferenceStateChanged();
        ShowToast("Motion", MotionPreferenceStatus, ToastSeverity.Success, 2200);
    }

    private void RefreshThemeOptions()
    {
        ThemeOptions.Clear();
        foreach (var theme in ThemeEngine.AvailableThemes)
        {
            ThemeOptions.Add(new ThemeInfo
            {
                Id = theme.Id,
                Name = theme.Name,
                Description = theme.Description,
                IsBuiltIn = theme.IsBuiltIn,
                IsActive = theme.IsActive,
                ResourceUri = theme.ResourceUri,
                PreviewColors = theme.PreviewColors.ToArray()
            });
        }

        OnPropertyChanged(nameof(CurrentTheme));
        OnPropertyChanged(nameof(BuiltInThemeCount));
    }

    private void InitializeMotionSurface()
    {
        RebuildMotionPreferenceOptions();
        SyncSelectedMotionPreference();

        var mainWindow = (Avalonia.Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        _themeEngine.ApplyMotionPreference(Config.MotionPreference, mainWindow);
        NotifyMotionPreferenceStateChanged();
    }

    private void RebuildMotionPreferenceOptions()
    {
        var selectedPreferenceId = SelectedMotionPreferenceOption?.Id ?? Config.MotionPreference;

        MotionPreferenceOptions.Clear();
        MotionPreferenceOptions.Add(new SelectionOption { Id = ThemeEngine.MotionPreferenceSystem, Label = "System" });
        MotionPreferenceOptions.Add(new SelectionOption { Id = ThemeEngine.MotionPreferenceFull, Label = "Plný motion" });
        MotionPreferenceOptions.Add(new SelectionOption { Id = ThemeEngine.MotionPreferenceReduced, Label = "Reduced motion" });

        _isUpdatingMotionPreferenceSelection = true;
        SelectedMotionPreferenceOption = MotionPreferenceOptions.FirstOrDefault(option => option.Id == selectedPreferenceId)
            ?? MotionPreferenceOptions.FirstOrDefault(option => option.Id == ThemeEngine.MotionPreferenceSystem);
        _isUpdatingMotionPreferenceSelection = false;
    }

    private void SyncSelectedMotionPreference()
    {
        _isUpdatingMotionPreferenceSelection = true;
        SelectedMotionPreferenceOption = MotionPreferenceOptions.FirstOrDefault(option => option.Id == Config.MotionPreference)
            ?? MotionPreferenceOptions.FirstOrDefault(option => option.Id == ThemeEngine.MotionPreferenceSystem);
        _isUpdatingMotionPreferenceSelection = false;
    }

    private void NotifyMotionPreferenceStateChanged()
    {
        OnPropertyChanged(nameof(MotionPreferenceCardTitle));
        OnPropertyChanged(nameof(MotionPreferenceCardHint));
        OnPropertyChanged(nameof(MotionPreferenceStatus));
        OnPropertyChanged(nameof(MotionPreferenceSystemStatus));
    }

    [RelayCommand]
    private void ApplyTheme(string? themeId)
    {
        if (string.IsNullOrWhiteSpace(themeId))
            return;

        if (!_themeEngine.ApplyTheme(themeId))
        {
            ShowToast("Motivy", "Vybraný motiv se nepodařilo aktivovat.", ToastSeverity.Error);
            return;
        }

        Config.CurrentThemeId = themeId;
        _launcherService.SaveConfig(Config);
        RefreshThemeOptions();
        ShowToast("Motivy", $"Aktivní motiv: {CurrentTheme.Name}", ToastSeverity.Success, 2200);
    }
}