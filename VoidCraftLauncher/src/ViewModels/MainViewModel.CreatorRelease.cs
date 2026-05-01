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

    public ObservableCollection<VoidRegistryVersionEntry> CreatorRegistryVersions { get; } = new();

    [ObservableProperty]
    private string _creatorReleaseChangelogContent = string.Empty;

    [ObservableProperty]
    private string _creatorPublishStatus = "Publikace připravená. VOID Registry release zápis ještě neběžel.";

    [ObservableProperty]
    private bool _isCreatorPublishRunning;

    [ObservableProperty]
    private string _creatorLastPublishedReleaseUrl = string.Empty;

    [ObservableProperty]
    private string _creatorLastPublishedAssetUrl = string.Empty;

    [ObservableProperty]
    private bool _hasCreatorPublishWorkflow;

    [ObservableProperty]
    private VoidRegistryProjectSummary? _creatorRegistryReleaseProject;

    [ObservableProperty]
    private string _creatorRegistryReleaseStatus = "Release governance čeká na VOID ID a slug projektu.";

    [ObservableProperty]
    private bool _isCreatorRegistryReleaseLoading;

    [ObservableProperty]
    private bool _isCreatorRegistryReleaseMutationRunning;

    public bool HasCreatorReleasePipeline => CreatorReleasePipeline.Count > 0;

    public bool HasCreatorValidationChecks => CreatorValidationChecks.Count > 0;

    public bool HasCreatorExportProfiles => CreatorExportProfiles.Count > 0;

    public bool HasCreatorReleaseHistory => CreatorReleaseHistory.Count > 0;

    public bool HasCreatorRegistryVersionHistory => CreatorRegistryVersions.Count > 0;

    public int CreatorPendingPublicReleaseCount => CreatorRegistryReleaseProject?.PendingPublicReleaseCount > 0
        ? CreatorRegistryReleaseProject.PendingPublicReleaseCount
        : CreatorRegistryVersions.Count(version => version.IsPendingPublicApproval);

    public bool HasCreatorPendingPublicReleases => CreatorPendingPublicReleaseCount > 0;

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

    public bool CanRefreshCreatorRegistryReleaseHistory =>
        !IsCreatorRegistryReleaseLoading &&
        !IsCreatorRegistryReleaseMutationRunning &&
        HasVoidIdSession &&
        !string.IsNullOrWhiteSpace(GetCreatorRegistrySlug());

    public bool CanManageCreatorRegistryReleases =>
        !IsCreatorRegistryReleaseLoading &&
        !IsCreatorRegistryReleaseMutationRunning &&
        CreatorRegistryReleaseProject?.CanPublish == true;

    public bool ShowCreatorRegistryReleaseReadonlyWarning => CreatorRegistryReleaseProject != null && !CanManageCreatorRegistryReleases;

    public string CreatorPublishActionLabel => IsCreatorPublishRunning
        ? "Publikuji..."
        : "Zapsat release do VOID Registry";

    public string CreatorGitHubRepositoryLabel => !string.IsNullOrWhiteSpace(CreatorGitState.RemoteUrl)
        ? CreatorGitState.RemoteUrl
        : CurrentModpackCreatorManifest?.RepositoryUrl ?? CurrentModpackCreatorManifest?.BrandProfile?.GitHub ?? "GitHub remote není nastavený";

    public string CreatorRegistryReleaseProjectLabel => CreatorRegistryReleaseProject != null
        ? $"Projekt: {CreatorRegistryReleaseProject.Name} ({CreatorRegistryReleaseProject.Slug})"
        : string.IsNullOrWhiteSpace(GetCreatorRegistrySlug())
            ? "Slug projektu zatím chybí"
            : $"Projekt: {GetCreatorRegistrySlug()}";

    public string CreatorRegistryReleaseAccessLabel => CreatorRegistryReleaseProject?.CanPublish == true
        ? $"Může publish: {CreatorRegistryReleaseProject.MembershipRoleLabel}"
        : CreatorRegistryReleaseProject != null
            ? $"Jen čtení: {CreatorRegistryReleaseProject.MembershipRoleLabel}"
            : "Přístup se načte z backendu";

    public string CreatorRegistryReleaseActionLabel => IsCreatorRegistryReleaseLoading
        ? "Načítám historii..."
        : "Obnovit release historii";

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

            return "Ready: commit, push, tag, GitHub release a zápis do VOID Registry. Public viditelnost verze se řeší zvlášť.";
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

    partial void OnCreatorRegistryReleaseProjectChanged(VoidRegistryProjectSummary? value) => NotifyCreatorReleaseStateChanged();

    partial void OnIsCreatorRegistryReleaseLoadingChanged(bool value) => NotifyCreatorReleaseStateChanged();

    partial void OnIsCreatorRegistryReleaseMutationRunningChanged(bool value) => NotifyCreatorReleaseStateChanged();

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

        QueueRefreshCreatorReleaseGovernance("Načítám release governance z VOID Registry...");
    }

    [RelayCommand]
    private async Task RefreshCreatorRegistryReleaseHistory()
    {
        await RefreshCreatorRegistryReleaseGovernanceCoreAsync(true);
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
            CreatorPublishStatus = $"Publikováno: {publishResult.Slug} v{publishResult.VersionNumber} je zapsaný ve VOID Registry. Public release se schvaluje samostatně.";

            ShowToast("Publish", $"{manifest.PackName} v{manifest.Version} je zapsaný ve VOID Registry.", ToastSeverity.Success, 4200);
            TrackCreatorActivity($"Publish do VOID Registry: {manifest.Slug} v{manifest.Version}", "Release");

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
    private async Task ToggleCreatorRegistryVersionVisibility(VoidRegistryVersionEntry? version)
    {
        if (version == null)
        {
            return;
        }

        if (version.IsYanked)
        {
            ShowToast("Registry", "Yanked release nejde znovu schválit ani vracet mezi interní bez nové verze.", ToastSeverity.Warning, 3600);
            return;
        }

        if (!CanManageCreatorRegistryReleases)
        {
            ShowToast("Registry", "Visibility release může měnit jen owner nebo maintainer projektu.", ToastSeverity.Warning, 3200);
            return;
        }

        var slug = GetCreatorRegistrySlug();
        var accessToken = await GetCreatorRegistryReleaseAccessTokenAsync(true);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return;
        }

        IsCreatorRegistryReleaseMutationRunning = true;

        try
        {
            var successMessage = version.IsPublicRelease
                ? $"Verze {version.VersionNumber} je znovu interní a veřejný web ji přestal nabízet."
                : $"Verze {version.VersionNumber} je schválená pro veřejný web.";

            CreatorRegistryReleaseStatus = version.IsPublicRelease
                ? $"Stahuji {version.VersionNumber} z veřejného webu..."
                : $"Schvaluji {version.VersionNumber} pro veřejný web...";

            await _voidRegistryService.SetVersionVisibilityAsync(accessToken, slug, version.VersionNumber, version.VisibilityActionTarget);
            await RefreshCreatorRegistryReleaseGovernanceCoreAsync(false, successMessage);
            ShowToast("Registry", successMessage, ToastSeverity.Success, 3200);
            TrackCreatorActivity($"Změněna visibility verze {version.VersionNumber} v {slug} na {version.VisibilityActionTarget}.", "Release");
        }
        catch (Exception ex)
        {
            LogService.Error("Creator release visibility mutation failed", ex);
            CreatorRegistryReleaseStatus = $"Změna visibility selhala: {ex.Message}";
            ShowToast("Registry", ex.Message, ToastSeverity.Error, 4200);
        }
        finally
        {
            IsCreatorRegistryReleaseMutationRunning = false;
        }
    }

    [RelayCommand]
    private async Task YankCreatorRegistryVersion(VoidRegistryVersionEntry? version)
    {
        if (version == null)
        {
            return;
        }

        if (version.IsYanked)
        {
            return;
        }

        if (!CanManageCreatorRegistryReleases)
        {
            ShowToast("Registry", "Release může stáhnout z katalogu jen owner nebo maintainer projektu.", ToastSeverity.Warning, 3200);
            return;
        }

        var slug = GetCreatorRegistrySlug();
        var accessToken = await GetCreatorRegistryReleaseAccessTokenAsync(true);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return;
        }

        IsCreatorRegistryReleaseMutationRunning = true;

        try
        {
            var successMessage = $"Verze {version.VersionNumber} byla stažená z katalogu VOID Registry.";
            CreatorRegistryReleaseStatus = $"Stahuji verzi {version.VersionNumber} z katalogu...";

            await _voidRegistryService.YankVersionAsync(accessToken, slug, version.VersionNumber);
            await RefreshCreatorRegistryReleaseGovernanceCoreAsync(false, successMessage);
            ShowToast("Registry", successMessage, ToastSeverity.Success, 3200);
            TrackCreatorActivity($"Stažena verze {version.VersionNumber} z katalogu projektu {slug}.", "Release");
        }
        catch (Exception ex)
        {
            LogService.Error("Creator release yank failed", ex);
            CreatorRegistryReleaseStatus = $"Stažení verze z katalogu selhalo: {ex.Message}";
            ShowToast("Registry", ex.Message, ToastSeverity.Error, 4200);
        }
        finally
        {
            IsCreatorRegistryReleaseMutationRunning = false;
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
        OnPropertyChanged(nameof(HasCreatorRegistryVersionHistory));
        OnPropertyChanged(nameof(CreatorPendingPublicReleaseCount));
        OnPropertyChanged(nameof(HasCreatorPendingPublicReleases));
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
        OnPropertyChanged(nameof(CanRefreshCreatorRegistryReleaseHistory));
        OnPropertyChanged(nameof(CanManageCreatorRegistryReleases));
        OnPropertyChanged(nameof(ShowCreatorRegistryReleaseReadonlyWarning));
        OnPropertyChanged(nameof(CreatorPublishActionLabel));
        OnPropertyChanged(nameof(CreatorGitHubRepositoryLabel));
        OnPropertyChanged(nameof(CreatorPublishReadinessLabel));
        OnPropertyChanged(nameof(CreatorRegistryReleaseProjectLabel));
        OnPropertyChanged(nameof(CreatorRegistryReleaseAccessLabel));
        OnPropertyChanged(nameof(CreatorRegistryReleaseActionLabel));
        NotifyCreatorCollaboratorStateChanged();
    }

    private async Task<string?> GetCreatorRegistryReleaseAccessTokenAsync(bool showToast)
    {
        if (string.IsNullOrWhiteSpace(GetCreatorRegistrySlug()))
        {
            CreatorRegistryReleaseStatus = "Manifest ještě nemá slug projektu pro VOID Registry.";
            if (showToast)
            {
                ShowToast("Registry", "Nejdřív doplň creator manifest se slugem projektu.", ToastSeverity.Warning, 3200);
            }

            return null;
        }

        if (!await EnsureFreshVoidIdSessionAsync(TimeSpan.FromMinutes(2)))
        {
            CreatorRegistryReleaseStatus = "Release governance vyžaduje aktivní relaci VOID ID.";
            if (showToast)
            {
                ShowToast("Registry", "Nejdřív přihlas creatora přes VOID ID.", ToastSeverity.Warning, 3200);
            }

            return null;
        }

        return CreatorVoidIdSession?.AccessToken;
    }

    private async Task ResetCreatorRegistryReleaseStateAsync(string status)
    {
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            CreatorRegistryVersions.Clear();
            CreatorRegistryReleaseProject = null;
            CreatorRegistryReleaseStatus = status;
            NotifyCreatorReleaseStateChanged();
        });
    }

    private async Task ApplyCreatorRegistryVersionsBundleAsync(VoidRegistryProjectVersionsBundle bundle, string status)
    {
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            CreatorRegistryVersions.Clear();
            foreach (var version in bundle.Data)
            {
                CreatorRegistryVersions.Add(version);
            }

            if (bundle.Project != null && bundle.Project.PendingPublicReleaseCount <= 0)
            {
                bundle.Project.PendingPublicReleaseCount = bundle.Data.Count(version => version.IsPendingPublicApproval);
            }

            CreatorRegistryReleaseProject = bundle.Project;
            CreatorRegistryReleaseStatus = status;
            NotifyCreatorReleaseStateChanged();
        });
    }

    private async Task RefreshCreatorRegistryReleaseGovernanceCoreAsync(bool showToast, string? successStatus = null)
    {
        var slug = GetCreatorRegistrySlug();
        var accessToken = await GetCreatorRegistryReleaseAccessTokenAsync(showToast);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            await ResetCreatorRegistryReleaseStateAsync(string.IsNullOrWhiteSpace(slug)
                ? "Release governance čeká na slug projektu."
                : "Release governance čeká na aktivní relaci VOID ID.");

            return;
        }

        IsCreatorRegistryReleaseLoading = true;

        try
        {
            var bundle = await _voidRegistryService.GetProjectVersionsAsync(accessToken, slug);
            var resolvedStatus = successStatus ?? (bundle.Data.Count > 0
                ? $"Načtena historie {bundle.Data.Count} verzí pro {slug}."
                : $"Projekt {slug} zatím nemá žádný release v registry historii.");

            await ApplyCreatorRegistryVersionsBundleAsync(bundle, resolvedStatus);
        }
        catch (Exception ex)
        {
            LogService.Error("Creator registry release history refresh failed", ex);
            CreatorRegistryReleaseStatus = $"Načtení release historie selhalo: {ex.Message}";
            if (showToast)
            {
                ShowToast("Registry", ex.Message, ToastSeverity.Error, 4200);
            }
        }
        finally
        {
            IsCreatorRegistryReleaseLoading = false;
        }
    }

    private void QueueRefreshCreatorReleaseGovernance(string status)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await ResetCreatorRegistryReleaseStateAsync(status);
                if (HasVoidIdSession && !string.IsNullOrWhiteSpace(GetCreatorRegistrySlug()))
                {
                    await RefreshCreatorRegistryReleaseGovernanceCoreAsync(false);
                }
            }
            catch (Exception ex)
            {
                LogService.Error("Creator release governance queued refresh failed", ex);
            }
        });
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
