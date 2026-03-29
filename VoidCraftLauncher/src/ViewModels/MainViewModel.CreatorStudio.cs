using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
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
    [ObservableProperty]
    private CreatorWorkspaceContext _creatorWorkspaceContext = new();

    [ObservableProperty]
    private string _creatorMetadataPackName = string.Empty;

    [ObservableProperty]
    private string _creatorMetadataSlug = string.Empty;

    [ObservableProperty]
    private string _creatorMetadataSummary = string.Empty;

    [ObservableProperty]
    private string _creatorMetadataAuthors = string.Empty;

    [ObservableProperty]
    private string _creatorMetadataVersion = string.Empty;

    [ObservableProperty]
    private string _creatorMetadataReleaseChannel = "alpha";

    [ObservableProperty]
    private string _creatorMetadataPrimaryServer = string.Empty;

    [ObservableProperty]
    private string _creatorMetadataRecommendedRamMb = "12288";

    [ObservableProperty]
    private string _creatorBrandAccentColor = "#3AA0FF";

    [ObservableProperty]
    private string _creatorBrandLauncherCardTitle = string.Empty;

    [ObservableProperty]
    private string _creatorBrandOneLiner = string.Empty;

    [ObservableProperty]
    private string _creatorBrandWebsite = string.Empty;

    [ObservableProperty]
    private string _creatorBrandDiscord = string.Empty;

    [ObservableProperty]
    private string _creatorBrandGitHub = string.Empty;

    [ObservableProperty]
    private string _creatorBrandSupportLink = string.Empty;

    [ObservableProperty]
    private bool _isCreatorMetadataDirty;

    [ObservableProperty]
    private bool _isCreatorMetadataSaving;

    [ObservableProperty]
    private CreatorManifest? _currentModpackCreatorManifest;

    partial void OnCurrentModpackCreatorManifestChanged(CreatorManifest? value)
    {
        OnPropertyChanged(nameof(HasCurrentWorkspaceCreatorMetadata));
        OnPropertyChanged(nameof(HasCurrentWorkspaceDescription));
        OnPropertyChanged(nameof(HasCurrentWorkspacePrimaryServer));
        OnPropertyChanged(nameof(CurrentWorkspaceDisplayName));
        OnPropertyChanged(nameof(CurrentWorkspaceDescription));
        OnPropertyChanged(nameof(CurrentWorkspaceAuthorLabel));
        OnPropertyChanged(nameof(CurrentWorkspaceMetadataSummary));
        OnPropertyChanged(nameof(CurrentWorkspacePrimaryServerLabel));
        OnPropertyChanged(nameof(CurrentWorkspaceRecommendedRamLabel));
    }

    private bool _isSyncingCreatorMetadataEditor;
    private string _creatorMetadataWorkspaceId = string.Empty;

    private CreatorStudioPreferences CreatorPreferences
    {
        get
        {
            Config.CreatorStudio ??= new CreatorStudioPreferences();
            Config.CreatorStudio.RecentWorkspaces ??= new List<CreatorRecentWorkspace>();
            Config.CreatorStudio.ShellState ??= new CreatorShellState();
            Config.CreatorStudio.ShellState.SelectedScope ??= new CreatorScope();
            return Config.CreatorStudio;
        }
    }

    private CreatorShellState CreatorShellState => CreatorPreferences.ShellState;

    public bool HasCreatorWorkspaceContext => CreatorWorkspaceContext.HasWorkspace;

    public int CreatorSelectedTabIndex
    {
        get => CreatorTabToIndex(CreatorShellState.SelectedTab);
        set => SetCreatorSelectedTab(IndexToCreatorTab(value));
    }

    public string CreatorCurrentTabLabel => GetCreatorTabLabel(CreatorShellState.SelectedTab);

    public string CreatorActiveScopeSummary => CreatorWorkspaceContext.ActiveScope.Summary;

    public string CreatorManifestStatus => CreatorWorkspaceContext.HasCreatorManifest
        ? IsCreatorMetadataDirty
            ? "creator_manifest.json ma lokalni zmeny"
            : "creator_manifest.json je dostupny"
        : IsCreatorMetadataDirty
            ? "creator_manifest.json je pripraven k vytvoreni"
            : "creator_manifest.json zatim chybi";

    public string CreatorWorkspaceFoldersSummary => CreatorWorkspaceContext.WorkspaceFoldersSummary;

    public string CreatorGitStatusSummary => CreatorWorkspaceContext.GitStatusLabel;

    public string CreatorReleaseStatusSummary => $"{CreatorWorkspaceContext.SnapshotCount} snapshotu • {CreatorWorkspaceContext.ExportArtifactCount} exportu";

    public string CreatorLastActivityLabel => CreatorWorkspaceContext.LastActivityLabel;

    public bool IsCreatorCopilotDeskVisible => IsStreamingToolsView && CreatorShellState.RightDockMode == CreatorRightDockMode.CopilotDesk;

    public bool IsStandardContextDockVisible => !IsStreamingToolsView || CreatorShellState.RightDockMode == CreatorRightDockMode.ContextDock;

    public bool IsCreatorNotesDrawerOpen => IsStreamingToolsView && CreatorShellState.SecondaryDrawerMode == CreatorSecondaryDrawerMode.Notes;

    public bool HasCreatorMissingFolders => CreatorWorkspaceContext.MissingStandardFolders.Count > 0;

    public bool HasCreatorRecentWorkspaces => CreatorWorkspaceContext.RecentWorkspaces.Count > 0;

    public bool HasCurrentWorkspaceCreatorMetadata => CurrentModpackCreatorManifest != null;

    public bool HasCurrentWorkspaceDescription => !string.IsNullOrWhiteSpace(CurrentWorkspaceDescription);

    public bool HasCurrentWorkspacePrimaryServer => !string.IsNullOrWhiteSpace(CurrentModpackCreatorManifest?.PrimaryServer);

    public string CurrentWorkspaceDisplayName => !string.IsNullOrWhiteSpace(CurrentModpackCreatorManifest?.PackName)
        ? CurrentModpackCreatorManifest.PackName
        : CurrentModpack?.Name ?? "Bez instance";

    public string CurrentWorkspaceDescription => !string.IsNullOrWhiteSpace(CurrentModpackCreatorManifest?.Summary)
        ? CurrentModpackCreatorManifest.Summary
        : CurrentModpack?.Description ?? string.Empty;

    public string CurrentWorkspaceAuthorLabel => CurrentModpackCreatorManifest?.Authors.Count > 0
        ? string.Join(", ", CurrentModpackCreatorManifest.Authors)
        : !string.IsNullOrWhiteSpace(CurrentModpack?.Author)
            ? CurrentModpack.Author
            : "Neznámý";

    public string CurrentWorkspaceMetadataSummary => CurrentModpackCreatorManifest == null
        ? CurrentModpack?.VersionTransitionText ?? "-"
        : $"{CurrentModpackCreatorManifest.Slug} • v{CurrentModpackCreatorManifest.Version} • {CurrentModpackCreatorManifest.ReleaseChannel}";

    public string CurrentWorkspacePrimaryServerLabel => CurrentModpackCreatorManifest?.PrimaryServer ?? string.Empty;

    public string CurrentWorkspaceRecommendedRamLabel => CurrentModpackCreatorManifest == null
        ? string.Empty
        : $"{CurrentModpackCreatorManifest.RecommendedRamMb} MB RAM";

    public string CreatorNotesWorkspaceStatus => CreatorWorkspaceContext.HasNotesWorkspace
        ? "Notes workspace je pripraven pro rychle poznamky a drawer handoff."
        : "Notes slozka zatim neexistuje. Drawer ji umi otevrit nebo vytvorit pri prvni praci.";

    public string CreatorDockHeadline => HasCreatorWorkspaceContext
        ? CreatorWorkspaceContext.WorkspaceLabel
        : "Creator context ceka na workspace";

    public string CreatorDockSubtitle => HasCreatorWorkspaceContext
        ? $"{CreatorCurrentTabLabel} • {CreatorActiveScopeSummary}"
        : "Vyber instanci v Creator Studiu a pravy dock prejde na sdileny workspace context.";

    public bool CanSaveCreatorMetadata => HasCreatorWorkspaceContext && !IsCreatorMetadataSaving && ValidateCreatorMetadata(out _);

    public string CreatorMetadataActionLabel => CreatorWorkspaceContext.HasCreatorManifest
        ? "Ulozit creator manifest"
        : IsExistingImportedPack
            ? "Vygenerovat z existujícího"
            : "Vytvorit creator manifest";

    private bool IsExistingImportedPack
    {
        get
        {
            var modpack = GetCreatorStudioSelectedModpack();
            if (modpack == null)
            {
                return false;
            }

            return modpack.Source == "CurseForge" || modpack.Source == "Modrinth" || modpack.Source == "VOID";
        }
    }

    public string CreatorMetadataValidationMessage
    {
        get
        {
            if (!HasCreatorWorkspaceContext)
            {
                return "Nejdřív vyber pracovní workspace, jinak metadata nemají kam zapisovat.";
            }

            if (!ValidateCreatorMetadata(out var error))
            {
                return error;
            }

            return IsCreatorMetadataDirty
                ? "Metadata mají lokální změny připravené k zápisu do creator_manifest.json."
                : "Metadata jsou synchronní s aktuálním workspace manifestem.";
        }
    }

    public string CreatorMetadataAuthorsPreview => string.IsNullOrWhiteSpace(CreatorMetadataAuthors)
        ? "Autor zatím není vyplněn."
        : CreatorMetadataAuthors;

    public string BrandStoragePath => HasCreatorWorkspaceContext && !string.IsNullOrWhiteSpace(CreatorWorkspaceContext.WorkspacePath)
        ? Path.Combine(CreatorWorkspaceContext.WorkspacePath, "assets", "branding")
        : string.Empty;

    public string BrandStorageStatus
    {
        get
        {
            if (string.IsNullOrWhiteSpace(BrandStoragePath))
            {
                return "Brand storage ceka na workspace";
            }

            var assetCount = 0;
            if (Directory.Exists(BrandStoragePath))
            {
                assetCount = Directory.GetFiles(BrandStoragePath, "*.*", SearchOption.AllDirectories).Length;
            }

            return assetCount == 0
                ? "0/5 asset slotu pripraveno"
                : $"{assetCount}/5 asset slotu pripraveno";
        }
    }

    public string LauncherPreviewTitle => !string.IsNullOrWhiteSpace(CreatorBrandLauncherCardTitle)
        ? CreatorBrandLauncherCardTitle
        : !string.IsNullOrWhiteSpace(CreatorMetadataPackName)
            ? CreatorMetadataPackName
            : "VOID-BOX2";

    public string LauncherPreviewDescription => !string.IsNullOrWhiteSpace(CreatorBrandOneLiner)
        ? CreatorBrandOneLiner
        : !string.IsNullOrWhiteSpace(CreatorMetadataSummary)
            ? CreatorMetadataSummary
            : "Adventure, Technology, Magic and many QUESTS! All within one single package with the best possible optimization!";

    public string LauncherPreviewAssetStatus => BrandStorageStatus;

    public string LauncherPreviewChannel => !string.IsNullOrWhiteSpace(CreatorMetadataReleaseChannel)
        ? CreatorMetadataReleaseChannel
        : "alpha";

    partial void OnCreatorMetadataPackNameChanged(string value) => OnCreatorMetadataEditorChanged();

    partial void OnCreatorMetadataSlugChanged(string value) => OnCreatorMetadataEditorChanged();

    partial void OnCreatorMetadataSummaryChanged(string value) => OnCreatorMetadataEditorChanged();

    partial void OnCreatorMetadataAuthorsChanged(string value) => OnCreatorMetadataEditorChanged();

    partial void OnCreatorMetadataVersionChanged(string value) => OnCreatorMetadataEditorChanged();

    partial void OnCreatorMetadataReleaseChannelChanged(string value) => OnCreatorMetadataEditorChanged();

    partial void OnCreatorMetadataPrimaryServerChanged(string value) => OnCreatorMetadataEditorChanged();

    partial void OnCreatorMetadataRecommendedRamMbChanged(string value) => OnCreatorMetadataEditorChanged();

    partial void OnCreatorBrandAccentColorChanged(string value) => OnCreatorMetadataEditorChanged();

    partial void OnCreatorBrandLauncherCardTitleChanged(string value) => OnCreatorMetadataEditorChanged();

    partial void OnCreatorBrandOneLinerChanged(string value) => OnCreatorMetadataEditorChanged();

    partial void OnCreatorBrandWebsiteChanged(string value) => OnCreatorMetadataEditorChanged();

    partial void OnCreatorBrandDiscordChanged(string value) => OnCreatorMetadataEditorChanged();

    partial void OnCreatorBrandGitHubChanged(string value) => OnCreatorMetadataEditorChanged();

    partial void OnCreatorBrandSupportLinkChanged(string value) => OnCreatorMetadataEditorChanged();

    partial void OnIsCreatorMetadataDirtyChanged(bool value) => NotifyCreatorShellVisualStateChanged();

    partial void OnIsCreatorMetadataSavingChanged(bool value) => NotifyCreatorShellVisualStateChanged();

    private void InitializeCreatorStudioShell()
    {
        _ = CreatorPreferences;
        RefreshCurrentModpackCreatorManifest();
        RefreshCreatorWorkspaceContext();
        NotifyCreatorShellVisualStateChanged();
    }

    private void RefreshCurrentModpackCreatorManifest()
    {
        if (CurrentModpack == null || string.IsNullOrWhiteSpace(CurrentModpack.Name))
        {
            CurrentModpackCreatorManifest = null;
            return;
        }

        CurrentModpackCreatorManifest = _creatorManifestService.LoadManifest(_launcherService.GetModpackPath(CurrentModpack.Name));
    }

    private void NotifyCreatorShellVisualStateChanged()
    {
        OnPropertyChanged(nameof(HasCreatorWorkspaceContext));
        OnPropertyChanged(nameof(CreatorSelectedTabIndex));
        OnPropertyChanged(nameof(CreatorCurrentTabLabel));
        OnPropertyChanged(nameof(CreatorActiveScopeSummary));
        OnPropertyChanged(nameof(CreatorManifestStatus));
        OnPropertyChanged(nameof(CreatorWorkspaceFoldersSummary));
        OnPropertyChanged(nameof(CreatorGitStatusSummary));
        OnPropertyChanged(nameof(CreatorReleaseStatusSummary));
        OnPropertyChanged(nameof(CreatorLastActivityLabel));
        OnPropertyChanged(nameof(IsCreatorCopilotDeskVisible));
        OnPropertyChanged(nameof(IsStandardContextDockVisible));
        OnPropertyChanged(nameof(IsCreatorNotesDrawerOpen));
        OnPropertyChanged(nameof(HasCreatorMissingFolders));
        OnPropertyChanged(nameof(HasCreatorRecentWorkspaces));
        OnPropertyChanged(nameof(CreatorNotesWorkspaceStatus));
        OnPropertyChanged(nameof(CreatorDockHeadline));
        OnPropertyChanged(nameof(CreatorDockSubtitle));
        OnPropertyChanged(nameof(CanSaveCreatorMetadata));
        OnPropertyChanged(nameof(CreatorMetadataActionLabel));
        OnPropertyChanged(nameof(CreatorMetadataValidationMessage));
        OnPropertyChanged(nameof(CreatorMetadataAuthorsPreview));
        OnPropertyChanged(nameof(BrandStoragePath));
        OnPropertyChanged(nameof(BrandStorageStatus));
        OnPropertyChanged(nameof(LauncherPreviewTitle));
        OnPropertyChanged(nameof(LauncherPreviewDescription));
        OnPropertyChanged(nameof(LauncherPreviewAssetStatus));
        OnPropertyChanged(nameof(LauncherPreviewChannel));
    }

    private void RefreshCreatorWorkspaceContext()
    {
        var previousWorkspaceId = CreatorWorkspaceContext.WorkspaceId;
        var modpack = GetCreatorStudioSelectedModpack();
        var manifest = GetCreatorStudioSelectedManifest();
        var workspaceId = SelectedSkinStudioInstance?.Id ?? CreatorPreferences.SelectedWorkspaceId;
        var workspacePath = string.IsNullOrWhiteSpace(workspaceId)
            ? null
            : _launcherService.GetModpackPath(workspaceId);
        var linkedServers = modpack == null
            ? 0
            : Servers.Count(server =>
                server.LinkedModpackProjectId == modpack.ProjectId ||
                ArePackNamesEquivalent(server.LinkedModpackName, modpack.Name));

        CreatorWorkspaceContext = _creatorWorkspaceService.LoadContext(
            workspaceId,
            workspacePath,
            modpack,
            manifest,
            CreatorShellState,
            SelectedCreatorWorkbenchFile,
            CreatorWorkbenchFiles,
            linkedServers,
            CreatorPreferences.RecentWorkspaces,
            CreatorPreferences.LastActivityUtc,
            CreatorPreferences.LastActivitySummary);

        CreatorShellState.HasWorkspaceChanges = CreatorWorkspaceContext.HasDirtyWorkingTree == true;
        CreatorShellState.HasReleaseWarnings = !CreatorWorkspaceContext.HasCreatorManifest || CreatorWorkspaceContext.MissingStandardFolders.Count > 0;
        CreatorShellState.HasUnsavedFileChanges = CanSaveCreatorWorkbenchFile || IsCreatorMetadataDirty;
        SyncCreatorMetadataEditor(previousWorkspaceId, modpack);
        RefreshBrandingPreviews();
        NotifyCreatorShellVisualStateChanged();
    }

    private void SetCreatorSelectedTab(CreatorShellTab tab)
    {
        if (CreatorShellState.SelectedTab == tab)
        {
            return;
        }

        CreatorShellState.SelectedTab = tab;
        CreatorShellState.LastOpenWorkspaceSection = tab.ToString().ToLowerInvariant();
        CreatorShellState.SelectedSubview = tab == CreatorShellTab.Files ? "editor" : "summary";

        if (tab == CreatorShellTab.Mods)
        {
            LoadInstalledMods();
        }

        TrackCreatorActivity($"Otevrena creator zalozka {GetCreatorTabLabel(tab)}.");
        RefreshCreatorWorkspaceContext();
    }

    private void SetCreatorDockMode(CreatorRightDockMode dockMode, bool persist = true)
    {
        if (CreatorShellState.RightDockMode == dockMode)
        {
            return;
        }

        CreatorShellState.RightDockMode = dockMode;
        if (persist)
        {
            PersistCreatorPreferences();
        }

        NotifyCreatorShellVisualStateChanged();
    }

    private void SetCreatorDrawerMode(CreatorSecondaryDrawerMode drawerMode, bool persist = true)
    {
        if (CreatorShellState.SecondaryDrawerMode == drawerMode)
        {
            return;
        }

        CreatorShellState.SecondaryDrawerMode = drawerMode;
        if (persist)
        {
            PersistCreatorPreferences();
        }

        NotifyCreatorShellVisualStateChanged();
    }

    private void PersistCreatorPreferences()
    {
        _launcherService.SaveConfig(Config);
    }

    private void TrackCreatorActivity(string summary)
    {
        CreatorPreferences.LastActivityUtc = DateTimeOffset.UtcNow;
        CreatorPreferences.LastActivitySummary = summary;

        if (SelectedSkinStudioInstance != null)
        {
            RememberRecentWorkspace(SelectedSkinStudioInstance.Id, SelectedSkinStudioInstance.Label);
        }

        PersistCreatorPreferences();
    }

    private void RememberRecentWorkspace(string workspaceId, string label)
    {
        var recent = CreatorPreferences.RecentWorkspaces;
        var existing = recent.FirstOrDefault(item => string.Equals(item.WorkspaceId, workspaceId, StringComparison.OrdinalIgnoreCase));
        if (existing == null)
        {
            recent.Add(new CreatorRecentWorkspace
            {
                WorkspaceId = workspaceId,
                Label = label,
                LastAccessedUtc = DateTimeOffset.UtcNow
            });
        }
        else
        {
            existing.Label = label;
            existing.LastAccessedUtc = DateTimeOffset.UtcNow;
        }

        CreatorPreferences.RecentWorkspaces = recent
            .OrderByDescending(item => item.LastAccessedUtc)
            .Take(6)
            .ToList();
    }

    private void OnCreatorWorkspaceSelectionChanged(SelectionOption? value)
    {
        CreatorPreferences.SelectedWorkspaceId = value?.Id;
        if (value != null)
        {
            RememberRecentWorkspace(value.Id, value.Label);
            TrackCreatorActivity($"Vybran workspace {value.Label}.");
        }
        else
        {
            PersistCreatorPreferences();
        }

        RefreshCreatorWorkspaceContext();
    }

    private void SyncCreatorWorkbenchFocus(CreatorWorkbenchFile? file)
    {
        CreatorShellState.SelectedScope = file == null
            ? new CreatorScope
            {
                Kind = CreatorScopeKind.Workspace,
                Label = "Workspace",
                ItemCount = CreatorWorkbenchFiles.Count
            }
            : new CreatorScope
            {
                Kind = CreatorScopeKind.SingleFile,
                Label = file.RelativePath,
                RelativePath = file.RelativePath,
                ItemCount = 1
            };

        CreatorShellState.LastOpenWorkspaceSection = file == null ? CreatorShellState.LastOpenWorkspaceSection : "files";
        CreatorShellState.SelectedSubview = file == null ? "summary" : "editor";

        if (file != null)
        {
            TrackCreatorActivity($"Otevren soubor {file.RelativePath}.");
        }

        RefreshCreatorWorkspaceContext();
    }

    private void UpdateCreatorDirtyIndicators()
    {
        CreatorShellState.HasUnsavedFileChanges = CanSaveCreatorWorkbenchFile || IsCreatorMetadataDirty;
        CreatorShellState.HasWorkspaceChanges = CreatorWorkspaceContext.HasDirtyWorkingTree == true || CanSaveCreatorWorkbenchFile || IsCreatorMetadataDirty;
        CreatorShellState.HasReleaseWarnings = !CreatorWorkspaceContext.HasCreatorManifest || CreatorWorkspaceContext.MissingStandardFolders.Count > 0;
        NotifyCreatorShellVisualStateChanged();
    }

    private void OnCreatorMetadataEditorChanged()
    {
        if (_isSyncingCreatorMetadataEditor)
        {
            return;
        }

        IsCreatorMetadataDirty = true;
        UpdateCreatorDirtyIndicators();
    }

    private void SyncCreatorMetadataEditor(string previousWorkspaceId, ModpackInfo? modpack, bool force = false)
    {
        if (!HasCreatorWorkspaceContext || string.IsNullOrWhiteSpace(CreatorWorkspaceContext.WorkspacePath))
        {
            ResetCreatorMetadataEditor();
            return;
        }

        var workspaceChanged = force ||
            !string.Equals(previousWorkspaceId, CreatorWorkspaceContext.WorkspaceId, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(_creatorMetadataWorkspaceId, CreatorWorkspaceContext.WorkspaceId, StringComparison.OrdinalIgnoreCase);

        if (!workspaceChanged && IsCreatorMetadataDirty)
        {
            return;
        }

        if (!workspaceChanged && !string.IsNullOrWhiteSpace(CreatorMetadataPackName))
        {
            return;
        }

        var loaderVersion = modpack?.IsCustomProfile == true
            ? modpack.CustomModLoaderVersion
            : string.Empty;
        var creatorManifest = _creatorManifestService.LoadManifest(CreatorWorkspaceContext.WorkspacePath)
            ?? _creatorManifestService.CreateFallbackManifest(
                modpack,
                CreatorWorkspaceContext.MinecraftVersion,
                CreatorWorkspaceContext.LoaderLabel,
                loaderVersion ?? string.Empty);

        ApplyCreatorMetadata(creatorManifest);
        
        // Preview branding from modpack logo even before manifest exists
        if (modpack != null && !string.IsNullOrWhiteSpace(modpack.LogoUrl) && !CreatorWorkspaceContext.HasCreatorManifest)
        {
            BrandingLogoPreview = modpack.LogoUrl;
        }
        
        _creatorMetadataWorkspaceId = CreatorWorkspaceContext.WorkspaceId;
    }

    private void ApplyCreatorMetadata(CreatorManifest manifest)
    {
        _isSyncingCreatorMetadataEditor = true;
        CreatorMetadataPackName = manifest.PackName;
        CreatorMetadataSlug = manifest.Slug;
        CreatorMetadataSummary = manifest.Summary;
        CreatorMetadataAuthors = string.Join(", ", manifest.Authors);
        CreatorMetadataVersion = manifest.Version;
        CreatorMetadataReleaseChannel = string.IsNullOrWhiteSpace(manifest.ReleaseChannel) ? "alpha" : manifest.ReleaseChannel;
        CreatorMetadataPrimaryServer = manifest.PrimaryServer;
        CreatorMetadataRecommendedRamMb = manifest.RecommendedRamMb.ToString();
        
        // Brand profile
        CreatorBrandAccentColor = manifest.BrandProfile?.AccentColor ?? "#3AA0FF";
        CreatorBrandLauncherCardTitle = manifest.BrandProfile?.LauncherCardTitle ?? string.Empty;
        CreatorBrandOneLiner = manifest.BrandProfile?.OneLiner ?? string.Empty;
        CreatorBrandWebsite = manifest.BrandProfile?.Website ?? string.Empty;
        CreatorBrandDiscord = manifest.BrandProfile?.Discord ?? string.Empty;
        CreatorBrandGitHub = manifest.BrandProfile?.GitHub ?? string.Empty;
        CreatorBrandSupportLink = manifest.BrandProfile?.SupportLink ?? string.Empty;
        
        IsCreatorMetadataDirty = false;
        _isSyncingCreatorMetadataEditor = false;
        UpdateCreatorDirtyIndicators();
    }

    private void ResetCreatorMetadataEditor()
    {
        _isSyncingCreatorMetadataEditor = true;
        CreatorMetadataPackName = string.Empty;
        CreatorMetadataSlug = string.Empty;
        CreatorMetadataSummary = string.Empty;
        CreatorMetadataAuthors = string.Empty;
        CreatorMetadataVersion = string.Empty;
        CreatorMetadataReleaseChannel = "alpha";
        CreatorMetadataPrimaryServer = string.Empty;
        CreatorMetadataRecommendedRamMb = "12288";
        
        // Brand profile
        CreatorBrandAccentColor = "#3AA0FF";
        CreatorBrandLauncherCardTitle = string.Empty;
        CreatorBrandOneLiner = string.Empty;
        CreatorBrandWebsite = string.Empty;
        CreatorBrandDiscord = string.Empty;
        CreatorBrandGitHub = string.Empty;
        CreatorBrandSupportLink = string.Empty;
        
        IsCreatorMetadataDirty = false;
        _creatorMetadataWorkspaceId = string.Empty;
        _isSyncingCreatorMetadataEditor = false;
        UpdateCreatorDirtyIndicators();
    }

    private bool ValidateCreatorMetadata(out string error)
    {
        if (string.IsNullOrWhiteSpace(CreatorMetadataPackName))
        {
            error = "Pack name nesmí zůstat prázdný.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(CreatorMetadataSlug))
        {
            error = "Slug je povinný kvůli budoucím exportům a release identitě.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(CreatorMetadataVersion))
        {
            error = "Version je povinná pro creator manifest.";
            return false;
        }

        if (!int.TryParse(CreatorMetadataRecommendedRamMb, out var recommendedRamMb) || recommendedRamMb < 2048)
        {
            error = "Recommended RAM musí být číslo alespoň 2048 MB.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private async Task<bool> ConfirmSnapshotBeforeCreatorApplyAsync(string reason, CreatorScopeKind scopeKind, IEnumerable<string>? relativePaths = null)
    {
        var modpack = GetCreatorStudioSelectedModpack();
        if (modpack == null || string.IsNullOrWhiteSpace(CreatorWorkspaceContext.WorkspacePath))
        {
            return true;
        }

        var shouldRequireSnapshot = CreatorWorkspaceContext.RequiresSnapshotBeforeApply &&
            (scopeKind == CreatorScopeKind.MultiFile || scopeKind == CreatorScopeKind.Workspace || scopeKind == CreatorScopeKind.ReleaseBoard);

        if (!shouldRequireSnapshot)
        {
            return true;
        }

        var decision = await ShowBackupPrompt(modpack.Name);
        switch (decision)
        {
            case BackupPromptDecision.Cancel:
                ShowToast("Creator Studio", "Apply akce byla zrušena před zápisem do workspace.", ToastSeverity.Warning, 2200);
                return false;
            case BackupPromptDecision.ContinueWithoutBackup:
                ShowToast("Creator Studio", "Větší creator změna pokračuje bez snapshotu.", ToastSeverity.Warning, 2600);
                return true;
            case BackupPromptDecision.BackupAndContinue:
                try
                {
                    await CreateCreatorWorkspaceSnapshotAsync(modpack, reason, relativePaths);
                    return true;
                }
                catch (Exception ex)
                {
                    LogService.Error($"Creator snapshot failed before {reason}", ex);
                    ShowToast("Creator snapshot selhal", ex.Message, ToastSeverity.Error, 4000);
                    return false;
                }
            default:
                return false;
        }
    }

    private async Task<string?> CreateCreatorWorkspaceSnapshotAsync(ModpackInfo modpack, string reason, IEnumerable<string>? relativePaths = null)
    {
        var instancePath = _launcherService.GetModpackPath(modpack.Name);
        if (!Directory.Exists(instancePath))
        {
            return null;
        }

        var backupPath = ModpackInstaller.BackupUserConfigs(instancePath, GetPersistentBackupRoot(modpack.Name));
        foreach (var relativePath in BuildCreatorSnapshotEntries(relativePaths))
        {
            var sourcePath = Path.Combine(instancePath, relativePath);
            var targetPath = Path.Combine(backupPath, relativePath);

            if (File.Exists(sourcePath))
            {
                var parent = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrWhiteSpace(parent))
                {
                    Directory.CreateDirectory(parent);
                }

                File.Copy(sourcePath, targetPath, true);
            }
            else if (Directory.Exists(sourcePath))
            {
                CopyDirectoryRecursive(sourcePath, targetPath);
            }
        }

        await RefreshCurrentModpackWorkspaceDataAsync();
        RefreshCreatorWorkspaceContext();

        StructuredLog.Event("CreatorStudio", "Creator snapshot created", new
        {
            Modpack = modpack.Name,
            Reason = reason,
            BackupPath = backupPath
        });

        ShowToast("Creator snapshot vytvořen", Path.GetFileName(backupPath), ToastSeverity.Success, 3000);
        TrackCreatorActivity($"Vytvoren snapshot pred apply: {reason}.");
        return backupPath;
    }

    private static IReadOnlyList<string> BuildCreatorSnapshotEntries(IEnumerable<string>? relativePaths)
    {
        var defaults = new[]
        {
            "config",
            "defaultconfigs",
            "scripts",
            "kubejs",
            "docs",
            "notes",
            "qa",
            "quests",
            CreatorWorkspaceContext.CreatorManifestFileName,
            "options.txt",
            "servers.dat"
        };

        return (relativePaths ?? Array.Empty<string>())
            .Concat(defaults)
            .Select(path => path.Replace('/', Path.DirectorySeparatorChar).Trim())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void CopyDirectoryRecursive(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)), true);
        }

        foreach (var directory in Directory.GetDirectories(sourceDir))
        {
            CopyDirectoryRecursive(directory, Path.Combine(targetDir, Path.GetFileName(directory)));
        }
    }

    [RelayCommand]
    private void UseCreatorCopilotDesk()
    {
        SetCreatorDockMode(CreatorRightDockMode.CopilotDesk);
        TrackCreatorActivity("Zapnuty Creator Copilot Desk.");
    }

    [RelayCommand]
    private void UseDefaultContextDock()
    {
        SetCreatorDockMode(CreatorRightDockMode.ContextDock);
        TrackCreatorActivity("Obnoven standardni Context Dock.");
    }

    [RelayCommand]
    private void OpenCreatorNotesDrawer()
    {
        SetCreatorDockMode(CreatorRightDockMode.CopilotDesk, false);
        SetCreatorDrawerMode(CreatorSecondaryDrawerMode.Notes);
        TrackCreatorActivity("Otevren Notes drawer.");
    }

    [RelayCommand]
    private void CloseCreatorNotesDrawer()
    {
        SetCreatorDrawerMode(CreatorSecondaryDrawerMode.None);
        TrackCreatorActivity("Zavren Notes drawer.");
    }

    [RelayCommand]
    private void OpenCreatorNotesWorkspace()
    {
        if (!HasCreatorWorkspaceContext || string.IsNullOrWhiteSpace(CreatorWorkspaceContext.NotesPath))
        {
            ShowToast("Creator Studio", "Nejdřív vyber pracovní instanci.", ToastSeverity.Warning);
            return;
        }

        Directory.CreateDirectory(CreatorWorkspaceContext.NotesPath);
        OpenFolder(CreatorWorkspaceContext.NotesPath);
        TrackCreatorActivity("Otevren notes workspace.");
        RefreshCreatorWorkspaceContext();
    }

    [RelayCommand]
    private void OpenCreatorExportsWorkspace()
    {
        if (!HasCreatorWorkspaceContext || string.IsNullOrWhiteSpace(CreatorWorkspaceContext.WorkspacePath))
        {
            ShowToast("Creator Studio", "Nejdřív vyber pracovní instanci.", ToastSeverity.Warning);
            return;
        }

        var exportsPath = Path.Combine(CreatorWorkspaceContext.WorkspacePath, "exports");
        Directory.CreateDirectory(exportsPath);
        OpenFolder(exportsPath);
        TrackCreatorActivity("Otevrena export slozka.");
        RefreshCreatorWorkspaceContext();
    }

    [RelayCommand]
    private async Task SaveCreatorMetadata()
    {
        if (!HasCreatorWorkspaceContext || string.IsNullOrWhiteSpace(CreatorWorkspaceContext.WorkspacePath))
        {
            ShowToast("Creator Studio", "Nejdřív vyber pracovní workspace.", ToastSeverity.Warning);
            return;
        }

        if (!CreatorWorkspaceContext.HasCreatorManifest && IsExistingImportedPack)
        {
            await GenerateManifestFromExisting();
            return;
        }

        if (!ValidateCreatorMetadata(out var validationError))
        {
            ShowToast("Creator Studio", validationError, ToastSeverity.Warning, 2800);
            return;
        }

        if (!await ConfirmSnapshotBeforeCreatorApplyAsync(
                "creator-manifest-apply",
                CreatorScopeKind.Workspace,
                new[] { CreatorWorkspaceContext.CreatorManifestFileName, "docs", "notes", "qa", "quests" }))
        {
            return;
        }

        IsCreatorMetadataSaving = true;
        try
        {
            var existingManifest = _creatorManifestService.LoadManifest(CreatorWorkspaceContext.WorkspacePath);
            var manifest = _creatorManifestService.CreateManifest(
                CreatorMetadataPackName,
                CreatorMetadataSlug,
                CreatorMetadataSummary,
                CreatorMetadataAuthors.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries),
                CreatorMetadataVersion,
                CreatorWorkspaceContext.MinecraftVersion,
                CreatorWorkspaceContext.LoaderLabel,
                GetCreatorStudioSelectedModpack()?.CustomModLoaderVersion ?? string.Empty,
                int.Parse(CreatorMetadataRecommendedRamMb),
                CreatorMetadataPrimaryServer,
                CreatorMetadataReleaseChannel,
                existingManifest?.CreatedAtUtc);

            // Apply brand profile
            manifest.BrandProfile = new CreatorBrandProfile
            {
                AccentColor = CreatorBrandAccentColor,
                LauncherCardTitle = CreatorBrandLauncherCardTitle,
                OneLiner = CreatorBrandOneLiner,
                Website = CreatorBrandWebsite,
                Discord = CreatorBrandDiscord,
                GitHub = CreatorBrandGitHub,
                SupportLink = CreatorBrandSupportLink
            };

            var savedManifest = await _creatorManifestService.SaveManifestAsync(CreatorWorkspaceContext.WorkspacePath, manifest);
            ApplyCreatorMetadata(savedManifest);
            SyncInstalledModpackFromCreatorManifest(CreatorWorkspaceContext.WorkspaceId, savedManifest);
            TrackCreatorActivity("Ulozen creator_manifest.json a metadata workspace.");
            ShowToast("Creator Studio", "Metadata byla zapsána do creator_manifest.json.", ToastSeverity.Success, 2600);
            RefreshCreatorWorkspaceContext();
            SyncCreatorMetadataEditor(CreatorWorkspaceContext.WorkspaceId, GetCreatorStudioSelectedModpack(), force: true);
        }
        catch (Exception ex)
        {
            LogService.Error("Creator metadata save failed", ex);
            ShowToast("Creator Studio", "Metadata se nepodařilo uložit.", ToastSeverity.Error, 3200);
        }
        finally
        {
            IsCreatorMetadataSaving = false;
        }
    }

    private static int CreatorTabToIndex(CreatorShellTab tab) => tab switch
    {
        CreatorShellTab.Overview => 0,
        CreatorShellTab.Metadata => 1,
        CreatorShellTab.Mods => 2,
        CreatorShellTab.Files => 3,
        CreatorShellTab.Notes => 4,
        CreatorShellTab.Git => 5,
        CreatorShellTab.Release => 6,
        _ => 0
    };

    private static CreatorShellTab IndexToCreatorTab(int index) => index switch
    {
        1 => CreatorShellTab.Metadata,
        2 => CreatorShellTab.Mods,
        3 => CreatorShellTab.Files,
        4 => CreatorShellTab.Notes,
        5 => CreatorShellTab.Git,
        6 => CreatorShellTab.Release,
        _ => CreatorShellTab.Overview
    };

    private static string GetCreatorTabLabel(CreatorShellTab tab) => tab switch
    {
        CreatorShellTab.Overview => "Overview",
        CreatorShellTab.Metadata => "Metadata",
        CreatorShellTab.Mods => "Mods",
        CreatorShellTab.Files => "Files",
        CreatorShellTab.Notes => "Notes",
        CreatorShellTab.Git => "Git",
        CreatorShellTab.Release => "Release",
        _ => "Overview"
    };
}