using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VoidCraftLauncher.Models;
using VoidCraftLauncher.Models.CreatorStudio;
using VoidCraftLauncher.Services;
using VoidCraftLauncher.Services.CreatorStudio;

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

    [ObservableProperty]
    private string _creatorPublishStatus = "Publikace připravená. VOID Launcher release zatím nebyl spuštěný.";

    [ObservableProperty]
    private bool _isCreatorPublishRunning;

    [ObservableProperty]
    private string _creatorLastPublishedReleaseUrl = string.Empty;

    [ObservableProperty]
    private string _creatorLastPublishedAssetUrl = string.Empty;

    [ObservableProperty]
    private bool _hasCreatorPublishWorkflow;

    public bool HasCreatorReleasePipeline => CreatorReleasePipeline.Count > 0;

    public bool HasCreatorValidationChecks => CreatorValidationChecks.Count > 0;

    public bool HasCreatorExportProfiles => CreatorExportProfiles.Count > 0;

    public bool HasCreatorReleaseHistory => CreatorReleaseHistory.Count > 0;

    public bool HasCreatorPublishedReleaseLinks =>
        !string.IsNullOrWhiteSpace(CreatorLastPublishedReleaseUrl) ||
        !string.IsNullOrWhiteSpace(CreatorLastPublishedAssetUrl);

    public bool HasCreatorPublishedReleaseUrl => !string.IsNullOrWhiteSpace(CreatorLastPublishedReleaseUrl);

    public bool HasCreatorPublishedAssetUrl => !string.IsNullOrWhiteSpace(CreatorLastPublishedAssetUrl);

    public bool CanBootstrapCreatorPublishWorkflow =>
        !IsCreatorPublishRunning &&
        HasCreatorGitRepository &&
        CreatorGitState.HasRemote;

    public bool CanCreatorPublishRelease =>
        !IsCreatorPublishRunning &&
        HasVoidIdSession &&
        HasCreatorGitRepository &&
        CreatorGitState.HasRemote &&
        CurrentModpackCreatorManifest != null &&
        !IsCreatorMetadataSaving;

    public string CreatorPublishActionLabel => IsCreatorPublishRunning
        ? "Publikuji..."
        : "Publish do VOID Launcher listu";

    public string CreatorGitHubRepositoryLabel => !string.IsNullOrWhiteSpace(CreatorGitState.RemoteUrl)
        ? CreatorGitState.RemoteUrl
        : CurrentModpackCreatorManifest?.RepositoryUrl ?? CurrentModpackCreatorManifest?.BrandProfile?.GitHub ?? "GitHub remote není nastavený";

    public string CreatorPublishReadinessLabel
    {
        get
        {
            if (!HasVoidIdSession)
            {
                return "Creator musí být přihlášený přes VOID ID.";
            }

            if (!HasCreatorGitRepository)
            {
                return "Workspace ještě není Git repository.";
            }

            if (!CreatorGitState.HasRemote)
            {
                return "Chybí GitHub origin remote.";
            }

            if (CurrentModpackCreatorManifest == null)
            {
                return "Chybí creator manifest.";
            }

            if (!HasCreatorPublishWorkflow)
            {
                return "Workflow pro GitHub release ještě není bootstrapnutý.";
            }

            return "Ready: commit, push, tag, GitHub release a zápis do VOID Registry.";
        }
    }

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

            HasCreatorPublishWorkflow = _gitHubReleaseService.HasWorkflowFiles(workspacePath);

            if (manifest != null && !string.IsNullOrWhiteSpace(manifest.Version) &&
                string.Equals(manifest.LastPublishedVersion, manifest.Version, StringComparison.OrdinalIgnoreCase))
            {
                CreatorPublishStatus = $"Poslední publish verze: v{manifest.LastPublishedVersion}";
            }

            NotifyCreatorReleaseStateChanged();
        });
    }

    [RelayCommand]
    private async Task CreatorExportVoidpack()
    {
        var workspacePath = SkinStudioSelectedInstancePath;
        if (string.IsNullOrWhiteSpace(workspacePath) || _instanceExportService == null)
        {
            ShowToast("Release", "Chybí workspace nebo creator manifest pro export.", ToastSeverity.Warning, 3200);
            return;
        }

        var manifest = await EnsureCreatorManifestForPublishAsync(workspacePath, showToast: true);
        if (manifest == null)
        {
            return;
        }

        try
        {
            var outputPath = await ExportCreatorVoidpackCoreAsync(workspacePath, manifest, showToast: true);
            CreatorPublishStatus = $"Lokální export hotový: {Path.GetFileName(outputPath)}";
            await RefreshCreatorReleasePipeline();
        }
        catch (Exception ex)
        {
            LogService.Error("Creator .voidpack export failed", ex);
            CreatorPublishStatus = $"Export .voidpack selhal: {ex.Message}";
            ShowToast("Release", $"Export .voidpack selhal: {ex.Message}", ToastSeverity.Error, 4200);
        }
    }

    private async Task<string> ExportCreatorVoidpackCoreAsync(string workspacePath, CreatorManifest manifest, bool showToast)
    {
        var assetName = _gitHubReleaseService.BuildAssetFileName(manifest);
        var exportsPath = Path.Combine(workspacePath, "exports");
        Directory.CreateDirectory(exportsPath);
        var outputPath = Path.Combine(exportsPath, assetName);

        CreatorPublishStatus = $"Exportuji lokální {assetName}...";
        if (showToast)
        {
            ShowToast("Release", $"Export .voidpack do {assetName} zahájen...", ToastSeverity.Info, 3000);
        }

        TrackCreatorActivity($"Export .voidpack: {assetName}", "Release");
        await _instanceExportService.ExportAsync(workspacePath, manifest.PackName, outputPath);
        return outputPath;
    }

    [RelayCommand]
    private async Task BootstrapCreatorPublishWorkflow()
    {
        var workspacePath = SkinStudioSelectedInstancePath;
        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            ShowToast("Publish", "Chybí workspace nebo creator manifest.", ToastSeverity.Warning, 2800);
            return;
        }

        var manifest = await EnsureCreatorManifestForPublishAsync(workspacePath, showToast: true);
        if (manifest == null)
        {
            return;
        }

        if (!TryResolveCreatorRepository(manifest, out var repository))
        {
            ShowToast("Publish", "Nejde určit GitHub repo z origin remotu nebo brand GitHub URL.", ToastSeverity.Warning, 4200);
            return;
        }

        await _gitHubReleaseService.EnsureVoidpackWorkflowAsync(workspacePath);

        manifest.RepositoryUrl = repository.WebUrl;
        manifest.BrandProfile ??= BuildCurrentBrandProfile();
        if (string.IsNullOrWhiteSpace(manifest.BrandProfile.GitHub))
        {
            manifest.BrandProfile.GitHub = repository.WebUrl;
        }

        await _creatorManifestService.SaveManifestAsync(workspacePath, manifest);
        HasCreatorPublishWorkflow = true;
        CreatorPublishStatus = "GitHub Actions workflow pro VOIDPACK release je připravený.";

        await RefreshCreatorGitStatus();
        await RefreshCreatorReleasePipeline();
        ShowToast("Publish", "Workflow pro GitHub release byl vytvořen / aktualizován.", ToastSeverity.Success, 3200);
        TrackCreatorActivity("Bootstrap GitHub VOIDPACK workflow dokončen.", "Release");
    }

    [RelayCommand]
    private async Task PublishCreatorRelease()
    {
        if (IsCreatorPublishRunning)
        {
            return;
        }

        var workspacePath = SkinStudioSelectedInstancePath;
        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            ShowToast("Publish", "Nejdřív vyber workspace pro publish.", ToastSeverity.Warning, 2600);
            return;
        }

        if (!await EnsureFreshVoidIdSessionAsync(TimeSpan.FromMinutes(10)))
        {
            ShowToast("Publish", "Nejdřív přihlas creatora přes VOID ID.", ToastSeverity.Warning, 3200);
            return;
        }

        var manifest = await EnsureCreatorManifestForPublishAsync(workspacePath, showToast: true);
        if (manifest == null)
        {
            return;
        }

        if (!TryResolveCreatorRepository(manifest, out var repository))
        {
            ShowToast("Publish", "Chybí validní GitHub remote pro release publish.", ToastSeverity.Warning, 3600);
            return;
        }

        IsCreatorPublishRunning = true;
        try
        {
            CreatorPublishStatus = "Kontroluji GitHub workflow...";
            if (!_gitHubReleaseService.HasWorkflowFiles(workspacePath))
            {
                await BootstrapCreatorPublishWorkflow();
                manifest = _creatorManifestService.LoadManifest(workspacePath) ?? manifest;
            }

            CreatorPublishStatus = "Exportuji lokální release archiv do exports/...";
            await ExportCreatorVoidpackCoreAsync(workspacePath, manifest, showToast: false);

            CreatorPublishStatus = "Staguji publish payload a workflow...";
            var publishTrackedPaths = await GetCreatorPublishTrackedPathsAsync(workspacePath);
            if (!await _creatorGitService.StagePathsAsync(workspacePath, publishTrackedPaths))
            {
                throw new InvalidOperationException("Nepodařilo se nastageovat publish payload.");
            }

            var gitStatus = await GetCreatorPublishGitStatusAsync(workspacePath);
            var stagedChanges = gitStatus.Changes
                .Where(change => change.IsStaged)
                .ToList();

            if (stagedChanges.Count > 0)
            {
                var commitMessage = string.IsNullOrWhiteSpace(CreatorGitCommitMessage)
                    ? $"release: {manifest.Slug} v{manifest.Version}"
                    : CreatorGitCommitMessage.Trim();

                CreatorPublishStatus = $"Commituji změny: {commitMessage}";
                var commitOk = await _creatorGitService.CommitAsync(
                    workspacePath,
                    commitMessage,
                    stagedChanges.Select(change => change.FilePath).ToList());
                if (!commitOk)
                {
                    throw new InvalidOperationException("Git commit selhal. Zkontroluj working tree a commit message.");
                }

                CreatorGitCommitMessage = string.Empty;
            }

            CreatorPublishStatus = "Pushuji repo na GitHub...";
            if (!await _creatorGitService.PushAsync(workspacePath, CreatorGitHubSession?.AccessToken))
            {
                throw new InvalidOperationException("Git push selhal. Ověř remote a přístup do repa.");
            }

            var tagName = _gitHubReleaseService.BuildTagName(manifest.Version);
            CreatorPublishStatus = $"Vytvářím release tag {tagName}...";
            if (!await _creatorGitService.CreateOrUpdateTagAsync(workspacePath, tagName, $"Release {manifest.PackName} {tagName}"))
            {
                throw new InvalidOperationException("Nepodařilo se vytvořit release tag.");
            }

            CreatorPublishStatus = $"Pushuji tag {tagName} na GitHub...";
            if (!await _creatorGitService.PushTagAsync(workspacePath, tagName, CreatorGitHubSession?.AccessToken))
            {
                throw new InvalidOperationException("Nepodařilo se pushnout release tag na GitHub.");
            }

            var assetName = _gitHubReleaseService.BuildAssetFileName(manifest);
            CreatorPublishStatus = $"Čekám na GitHub Actions release asset {assetName}...";
            var assetInfo = await _gitHubReleaseService.WaitForReleaseAssetAsync(repository, tagName, assetName, status =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => CreatorPublishStatus = status);
            });

            if (assetInfo == null)
            {
                throw new TimeoutException("GitHub release asset se neobjevil včas. Workflow ještě běží, selhal, nebo release zůstal jako draft.");
            }

            if (!await EnsureFreshVoidIdSessionAsync(TimeSpan.FromMinutes(2)))
            {
                throw new InvalidOperationException("VOID ID session expirovala před zápisem do registru. Přihlas se znovu.");
            }

            var publishedAsset = assetInfo with { ModCount = CountWorkspaceMods(workspacePath) };
            CreatorPublishStatus = "Zapisuji projekt a verzi do VOID Registry...";
            var publishResult = await _voidRegistryService.RegisterGitHubReleaseAsync(
                CreatorVoidIdSession!.AccessToken,
                manifest,
                repository,
                publishedAsset,
                CreatorReleaseChangelogContent.Trim());

            manifest.RegistryProjectId = publishResult.ProjectId;
            manifest.RepositoryUrl = repository.WebUrl;
            manifest.LastPublishedVersion = manifest.Version;
            manifest.LastPublishedAtUtc = DateTimeOffset.UtcNow;
            manifest.BrandProfile ??= BuildCurrentBrandProfile();
            if (string.IsNullOrWhiteSpace(manifest.BrandProfile.GitHub))
            {
                manifest.BrandProfile.GitHub = repository.WebUrl;
            }

            var savedManifest = await _creatorManifestService.SaveManifestAsync(workspacePath, manifest);
            ApplyCreatorMetadata(savedManifest);
            RefreshCurrentModpackCreatorManifest();

            CreatorLastPublishedReleaseUrl = publishedAsset.ReleasePageUrl;
            CreatorLastPublishedAssetUrl = publishedAsset.DownloadUrl;
            CreatorPublishStatus = $"Publikováno: {publishResult.Slug} v{publishResult.VersionNumber} je zapsaný ve VOID Launcher listu.";

            ShowToast("Publish", $"{manifest.PackName} v{manifest.Version} je publikovaný a zalistovaný.", ToastSeverity.Success, 4200);
            TrackCreatorActivity($"Publish do VOID Launcher listu: {manifest.Slug} v{manifest.Version}", "Release");

            await RefreshCreatorGitStatus();
            await RefreshCreatorReleasePipeline();
        }
        catch (Exception ex)
        {
            LogService.Error("Creator publish failed", ex);
            CreatorPublishStatus = $"Publish selhal: {ex.Message}";
            ShowToast("Publish", ex.Message, ToastSeverity.Error, 5200);
        }
        finally
        {
            IsCreatorPublishRunning = false;
            NotifyCreatorReleaseStateChanged();
        }
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
            case "void launcher":
                await PublishCreatorRelease();
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
        OnPropertyChanged(nameof(HasCreatorPublishedReleaseLinks));
        OnPropertyChanged(nameof(HasCreatorPublishedReleaseUrl));
        OnPropertyChanged(nameof(HasCreatorPublishedAssetUrl));
        OnPropertyChanged(nameof(CanBootstrapCreatorPublishWorkflow));
        OnPropertyChanged(nameof(CanCreatorPublishRelease));
        OnPropertyChanged(nameof(CreatorPublishActionLabel));
        OnPropertyChanged(nameof(CreatorGitHubRepositoryLabel));
        OnPropertyChanged(nameof(CreatorPublishReadinessLabel));
        NotifyCreatorCollaboratorStateChanged();
    }

    private bool TryResolveCreatorRepository(CreatorManifest manifest, out GitHubRepositoryReference repository)
    {
        if (_gitHubReleaseService.TryParseRepository(CreatorGitState.RemoteUrl, out repository))
        {
            return true;
        }

        if (_gitHubReleaseService.TryParseRepository(manifest.RepositoryUrl, out repository))
        {
            return true;
        }

        return _gitHubReleaseService.TryParseRepository(manifest.BrandProfile?.GitHub, out repository);
    }

    private async Task<IReadOnlyList<string>> GetCreatorPublishTrackedPathsAsync(string workspacePath)
    {
        var trackedFiles = await _creatorGitService.GetTrackedFilesAsync(workspacePath);
        return await Task.Run(() => _gitHubReleaseService.GetPublishTrackedPaths(workspacePath, trackedFiles));
    }

    private async Task<CreatorManifest?> EnsureCreatorManifestForPublishAsync(string workspacePath, bool showToast)
    {
        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            return null;
        }

        CreatorManifest? manifest;

        if (IsCreatorWorkspaceEditable && IsCreatorMetadataDirty && ValidateCreatorMetadata(out _))
        {
            var existingManifest = _creatorManifestService.LoadManifest(workspacePath);
            manifest = BuildCreatorManifestFromEditor(existingManifest);
        }
        else
        {
            manifest = CurrentModpackCreatorManifest ?? _creatorManifestService.LoadManifest(workspacePath);
            if (manifest == null)
            {
                var modpack = GetCreatorStudioSelectedModpack();
                manifest = CreateCreatorFallbackManifest(modpack, null, modpack?.CustomModLoaderVersion);
            }
        }

        if (manifest == null)
        {
            if (showToast)
            {
                ShowToast("Publish", "creator_manifest.json se nepodařilo připravit pro publish flow.", ToastSeverity.Warning, 3200);
            }

            return null;
        }

        var importedPublicBranding = await EnsureCreatorPublicBrandingForPublishAsync(workspacePath);

        var savedManifest = await _creatorManifestService.SaveManifestAsync(workspacePath, manifest);
        ApplyCreatorMetadata(savedManifest);
        CurrentModpackCreatorManifest = savedManifest;

        if (HasCreatorWorkspaceContext)
        {
            SyncInstalledModpackFromCreatorManifest(CreatorWorkspaceContext.WorkspaceId, savedManifest);
        }

        if (showToast && !CreatorWorkspaceContext.HasCreatorManifest)
        {
            ShowToast("Publish", "Fallback creator_manifest.json byl vygenerovaný automaticky pro export/publish.", ToastSeverity.Info, 3200);
        }

        if (showToast && importedPublicBranding)
        {
            ShowToast("Publish", "Veřejné logo packu bylo automaticky dotažené do assets/branding pro export a publish.", ToastSeverity.Info, 3200);
        }

        RefreshCreatorWorkspaceContext();
        return savedManifest;
    }

    private async Task<bool> EnsureCreatorPublicBrandingForPublishAsync(string workspacePath)
    {
        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(_creatorAssetsService.GetAssetPath(workspacePath, BrandingAssetSlot.Logo)))
        {
            return false;
        }

        var modpack = GetCreatorStudioSelectedModpack();
        if (modpack == null)
        {
            return false;
        }

        await TryRefreshModpackSourceMetadataAsync(modpack);

        if (string.IsNullOrWhiteSpace(modpack.LogoUrl))
        {
            return false;
        }

        var imported = await _creatorManifestService.TryImportPublicBrandingAsync(workspacePath, modpack);
        if (!imported)
        {
            return false;
        }

        RefreshBrandingPreviews();
        return true;
    }

    private static int CountWorkspaceMods(string workspacePath)
    {
        var modsPath = Path.Combine(workspacePath, "mods");
        if (!Directory.Exists(modsPath))
        {
            return 0;
        }

        return Directory.EnumerateFiles(modsPath, "*.jar", SearchOption.TopDirectoryOnly).Count() +
               Directory.EnumerateFiles(modsPath, "*.jar.disabled", SearchOption.TopDirectoryOnly).Count();
    }
}
