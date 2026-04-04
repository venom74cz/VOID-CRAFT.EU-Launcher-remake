using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using VoidCraftLauncher.Models;
using VoidCraftLauncher.Models.CreatorStudio;

namespace VoidCraftLauncher.ViewModels;

public partial class MainViewModel
{
    // ──── Release State ────
    public ObservableCollection<CreatorReleaseStep> CreatorReleasePipeline { get; } = new();

    public ObservableCollection<CreatorValidationCheck> CreatorValidationChecks { get; } = new();

    public ObservableCollection<CreatorExportProfile> CreatorExportProfiles { get; } = new();

    public ObservableCollection<CreatorReleaseHistoryEntry> CreatorReleaseHistory { get; } = new();

    [ObservableProperty]
    private string _creatorReleaseChangelogContent = string.Empty;

    public bool HasCreatorReleasePipeline => CreatorReleasePipeline.Count > 0;

    public bool HasCreatorValidationChecks => CreatorValidationChecks.Count > 0;

    public bool HasCreatorExportProfiles => CreatorExportProfiles.Count > 0;

    public bool HasCreatorReleaseHistory => CreatorReleaseHistory.Count > 0;

    public string CreatorCurrentPipelineStep
    {
        get
        {
            var active = CreatorReleasePipeline.FirstOrDefault(s => s.IsActive);
            return active != null ? $"Aktualni krok: {active.Label}" : "Pipeline neni aktivni";
        }
    }

    public string CreatorPipelineSummary => CreatorReleasePipeline.Count > 0
        ? string.Join(" → ", CreatorReleasePipeline.Select(s => s.IsCompleted ? $"[{s.Label}]" : s.IsActive ? $"({s.Label})" : s.Label))
        : "Pipeline neni inicializovana";

    public int CreatorValidationPassedCount => CreatorValidationChecks.Count(c => c.Passed);

    public int CreatorValidationTotalCount => CreatorValidationChecks.Count;

    public string CreatorValidationSummary => CreatorValidationChecks.Count > 0
        ? $"{CreatorValidationPassedCount}/{CreatorValidationTotalCount} kontrol proslo"
        : "Zatim zadne kontroly";

    [RelayCommand]
    private async Task RefreshCreatorReleasePipeline()
    {
        var workspacePath = SkinStudioSelectedInstancePath;
        if (string.IsNullOrWhiteSpace(workspacePath)) return;

        var manifest = CurrentModpackCreatorManifest;

        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            // Pipeline steps
            CreatorReleasePipeline.Clear();
            foreach (var step in _creatorReleaseService.BuildPipeline(manifest, workspacePath))
                CreatorReleasePipeline.Add(step);

            // Validation
            CreatorValidationChecks.Clear();
            foreach (var check in _creatorReleaseService.RunValidation(manifest, workspacePath))
                CreatorValidationChecks.Add(check);

            // Export profiles
            CreatorExportProfiles.Clear();
            foreach (var profile in _creatorReleaseService.GetExportProfiles(manifest))
                CreatorExportProfiles.Add(profile);

            // Release history
            CreatorReleaseHistory.Clear();
            foreach (var entry in _creatorReleaseService.GetReleaseHistory(workspacePath))
                CreatorReleaseHistory.Add(entry);

            NotifyCreatorReleaseStateChanged();
        });
    }

    [RelayCommand]
    private async Task CreatorExportVoidpack()
    {
        ShowToast("Release", "Export .voidpack zahajen...", ToastSeverity.Info, 3000);
        TrackCreatorActivity("Export .voidpack zahajen.");
        // Uses existing export pipeline from InstanceExportService
        await RefreshCreatorReleasePipeline();
    }

    [RelayCommand]
    private async Task ExportByProfile(object? parameter)
    {
        if (parameter is not Models.CreatorStudio.CreatorExportProfile profile)
            return;

        ShowToast("Release", $"Exportuji {profile.Format}...", ToastSeverity.Info, 3000);
        TrackCreatorActivity($"Export {profile.Format} ({profile.Label}) zahájen.");

        switch (profile.Format?.ToLowerInvariant())
        {
            case ".voidpack":
                await CreatorExportVoidpack();
                break;
            case "instance":
            case "zip":
                await ExportInstance();
                break;
            default:
                ShowToast("Release", $"Neznámý formát exportu: {profile.Format}", ToastSeverity.Warning, 3000);
                break;
        }
    }

    private void NotifyCreatorReleaseStateChanged()
    {
        OnPropertyChanged(nameof(HasCreatorReleasePipeline));
        OnPropertyChanged(nameof(HasCreatorValidationChecks));
        OnPropertyChanged(nameof(HasCreatorExportProfiles));
        OnPropertyChanged(nameof(HasCreatorReleaseHistory));
        OnPropertyChanged(nameof(CreatorCurrentPipelineStep));
        OnPropertyChanged(nameof(CreatorPipelineSummary));
        OnPropertyChanged(nameof(CreatorValidationPassedCount));
        OnPropertyChanged(nameof(CreatorValidationTotalCount));
        OnPropertyChanged(nameof(CreatorValidationSummary));
    }
}
