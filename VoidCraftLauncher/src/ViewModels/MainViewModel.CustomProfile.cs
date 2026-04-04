using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Platform.Storage;
using VoidCraftLauncher.Models;
using VoidCraftLauncher.Models.CreatorStudio;
using VoidCraftLauncher.Services;
using VoidCraftLauncher.Services.CreatorStudio;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace VoidCraftLauncher.ViewModels;

/// <summary>
/// Custom profile creation, MC/mod loader version fetching, mod search and management for custom profiles.
/// </summary>
public partial class MainViewModel
{
    private const string DefaultCreateProfileMcVersion = "1.21.1";
    private const string DefaultCreateProfileModLoader = "fabric";
    private const string DefaultCreateProfileVersion = "0.1.0";
    private const string DefaultCreateProfileReleaseChannel = "alpha";
    private const string DefaultCreateProfileRecommendedRamMb = "12288";
    private const string DefaultCreateProfileGitBranch = "main";
    private const string CreateProfileBootstrapBlankId = "blank";
    private const string CreateProfileBootstrapTemplateId = "template";
    private const string CreateProfileBootstrapImportCfId = "import-cf";
    private const string CreateProfileBootstrapImportMrId = "import-mr";
    private const string CreateProfileBootstrapCloneGitId = "clone-git";
    private const string CreateProfileBootstrapRestoreSnapshotId = "restore-snapshot";
    private const string CreateProfileTemplateFabricLiteId = "fabric-lite";
    private const string CreateProfileTemplateForgeServerId = "forge-server";
    private const string CreateProfileTemplateNeoForgeAdventureId = "neoforge-adventure";
    private bool _isCreateProfileSlugAutoSync = true;
    private bool _isUpdatingCreateProfileSlug;

    // ===== CUSTOM PROFILE STATE =====

    [ObservableProperty]
    private bool _isCreateProfileModalVisible = false;

    [ObservableProperty]
    private string _newProfileName = "";

    partial void OnNewProfileNameChanged(string value)
    {
        SyncCreateProfileSlugFromName(value);
        NotifyCreateProfileWizardStateChanged();
    }

    [ObservableProperty]
    private string _newProfileSlug = "";

    partial void OnNewProfileSlugChanged(string value)
    {
        if (!_isUpdatingCreateProfileSlug)
        {
            _isCreateProfileSlugAutoSync = string.IsNullOrWhiteSpace(value) ||
                string.Equals(value, BuildCreateProfileSlug(NewProfileName), StringComparison.OrdinalIgnoreCase);
        }

        NotifyCreateProfileWizardStateChanged();
    }

    [ObservableProperty]
    private string _newProfileSummary = "";

    partial void OnNewProfileSummaryChanged(string value)
    {
        NotifyCreateProfileWizardStateChanged();
    }

    [ObservableProperty]
    private string _newProfileAuthors = "";

    partial void OnNewProfileAuthorsChanged(string value)
    {
        NotifyCreateProfileWizardStateChanged();
    }

    [ObservableProperty]
    private string _newProfileVersion = DefaultCreateProfileVersion;

    partial void OnNewProfileVersionChanged(string value)
    {
        NotifyCreateProfileWizardStateChanged();
    }

    [ObservableProperty]
    private string _newProfileReleaseChannel = DefaultCreateProfileReleaseChannel;

    partial void OnNewProfileReleaseChannelChanged(string value)
    {
        NotifyCreateProfileWizardStateChanged();
    }

    [ObservableProperty]
    private string _newProfilePrimaryServer = "";

    partial void OnNewProfilePrimaryServerChanged(string value)
    {
        NotifyCreateProfileWizardStateChanged();
    }

    [ObservableProperty]
    private string _newProfileRecommendedRamMb = DefaultCreateProfileRecommendedRamMb;

    partial void OnNewProfileRecommendedRamMbChanged(string value)
    {
        NotifyCreateProfileWizardStateChanged();
    }

    [ObservableProperty]
    private string _newProfileMcVersion = DefaultCreateProfileMcVersion;

    partial void OnNewProfileMcVersionChanged(string value)
    {
        NotifyCreateProfileWizardStateChanged();
        _ = FetchModLoaderVersionsAsync();
    }

    [ObservableProperty]
    private string _newProfileModLoader = DefaultCreateProfileModLoader;

    partial void OnNewProfileModLoaderChanged(string value)
    {
        NotifyCreateProfileWizardStateChanged();
        _ = FetchModLoaderVersionsAsync();
    }

    [ObservableProperty]
    private string _newProfileModLoaderVersion = "";

    partial void OnNewProfileModLoaderVersionChanged(string value)
    {
        NotifyCreateProfileWizardStateChanged();
    }

    [ObservableProperty]
    private int _createProfileStepIndex;

    partial void OnCreateProfileStepIndexChanged(int value)
    {
        NotifyCreateProfileWizardStateChanged();
    }

    [ObservableProperty]
    private bool _isCreateProfileMcVersionsLoading;

    partial void OnIsCreateProfileMcVersionsLoadingChanged(bool value)
    {
        NotifyCreateProfileWizardStateChanged();
    }

    [ObservableProperty]
    private bool _isCreateProfileLoaderVersionsLoading;

    partial void OnIsCreateProfileLoaderVersionsLoadingChanged(bool value)
    {
        NotifyCreateProfileWizardStateChanged();
    }

    [ObservableProperty]
    private ObservableCollection<string> _availableMcVersions = new();

    [ObservableProperty]
    private ObservableCollection<string> _availableModLoaderVersions = new();

    public ObservableCollection<SelectionOption> CreateProfileBootstrapOptions { get; } = new();

    public ObservableCollection<SelectionOption> CreateProfileTemplateOptions { get; } = new();

    public ObservableCollection<SelectionOption> CreateProfileSnapshotOptions { get; } = new();

    [ObservableProperty]
    private SelectionOption? _selectedCreateProfileBootstrapOption;

    partial void OnSelectedCreateProfileBootstrapOptionChanged(SelectionOption? value)
    {
        if (string.Equals(value?.Id, CreateProfileBootstrapTemplateId, StringComparison.OrdinalIgnoreCase))
        {
            SelectedCreateProfileTemplateOption ??= CreateProfileTemplateOptions.FirstOrDefault();
            if (SelectedCreateProfileTemplateOption != null)
            {
                ApplyCreateProfileTemplateDefaults(SelectedCreateProfileTemplateOption.Id);
            }
        }

        if (string.Equals(value?.Id, CreateProfileBootstrapRestoreSnapshotId, StringComparison.OrdinalIgnoreCase))
        {
            RebuildCreateProfileSnapshotOptions();
            SelectedCreateProfileSnapshotOption ??= CreateProfileSnapshotOptions.FirstOrDefault();
        }

        NotifyCreateProfileWizardStateChanged();
    }

    [ObservableProperty]
    private SelectionOption? _selectedCreateProfileTemplateOption;

    partial void OnSelectedCreateProfileTemplateOptionChanged(SelectionOption? value)
    {
        if (value != null && IsCreateProfileTemplateBootstrap)
        {
            ApplyCreateProfileTemplateDefaults(value.Id);
        }

        NotifyCreateProfileWizardStateChanged();
    }

    [ObservableProperty]
    private SelectionOption? _selectedCreateProfileSnapshotOption;

    partial void OnSelectedCreateProfileSnapshotOptionChanged(SelectionOption? value)
    {
        NotifyCreateProfileWizardStateChanged();
    }

    [ObservableProperty]
    private string _newProfileBootstrapArchivePath = "";

    partial void OnNewProfileBootstrapArchivePathChanged(string value)
    {
        NotifyCreateProfileWizardStateChanged();
    }

    [ObservableProperty]
    private string _newProfileBootstrapGitUrl = "";

    partial void OnNewProfileBootstrapGitUrlChanged(string value)
    {
        NotifyCreateProfileWizardStateChanged();
    }

    [ObservableProperty]
    private string _newProfileBootstrapGitBranch = DefaultCreateProfileGitBranch;

    partial void OnNewProfileBootstrapGitBranchChanged(string value)
    {
        NotifyCreateProfileWizardStateChanged();
    }

    [ObservableProperty]
    private bool _isCreateProfileBootstrapBusy;

    partial void OnIsCreateProfileBootstrapBusyChanged(bool value)
    {
        NotifyCreateProfileWizardStateChanged();
    }

    public bool IsCreateProfileIdentityStep => CreateProfileStepIndex == 0;

    public bool IsCreateProfileRuntimeStep => CreateProfileStepIndex == 1;

    public bool IsCreateProfileReviewStep => CreateProfileStepIndex == 2;

    public bool CanGoBackCreateProfileStep => CreateProfileStepIndex > 0;

    public bool CanAdvanceCreateProfileStep =>
        IsCreateProfileIdentityStep
            ? ValidateCreateProfileName(out _) && ValidateCreateProfileMetadata(out _) && ValidateCreateProfileBootstrap(out _)
            : IsCreateProfileRuntimeStep && ValidateCreateProfileRuntime(out _);

    public bool CanCreateCustomProfile =>
        ValidateCreateProfileName(out _) &&
        ValidateCreateProfileMetadata(out _) &&
        ValidateCreateProfileBootstrap(out _) &&
        !IsCreateProfileBootstrapBusy &&
        ValidateCreateProfileRuntime(out _);

    public bool IsCreateProfileBlankBootstrap => IsCreateProfileBootstrapMode(CreateProfileBootstrapBlankId);

    public bool IsCreateProfileTemplateBootstrap => IsCreateProfileBootstrapMode(CreateProfileBootstrapTemplateId);

    public bool IsCreateProfileCurseForgeImportBootstrap => IsCreateProfileBootstrapMode(CreateProfileBootstrapImportCfId);

    public bool IsCreateProfileModrinthImportBootstrap => IsCreateProfileBootstrapMode(CreateProfileBootstrapImportMrId);

    public bool IsCreateProfileArchiveBootstrap =>
        IsCreateProfileCurseForgeImportBootstrap ||
        IsCreateProfileModrinthImportBootstrap;

    public bool IsCreateProfileCloneGitBootstrap => IsCreateProfileBootstrapMode(CreateProfileBootstrapCloneGitId);

    public bool IsCreateProfileRestoreSnapshotBootstrap => IsCreateProfileBootstrapMode(CreateProfileBootstrapRestoreSnapshotId);

    public bool HasCreateProfileBootstrapArchivePath => !string.IsNullOrWhiteSpace(NewProfileBootstrapArchivePath);

    public bool HasCreateProfileSnapshotOptions => CreateProfileSnapshotOptions.Count > 0;

    public bool HasCreateProfileSelectedSnapshot => SelectedCreateProfileSnapshotOption != null;

    public string CreateProfileBootstrapModeLabel => SelectedCreateProfileBootstrapOption?.Label ?? "Blank";

    public string CreateProfileDirectoryName => BuildCreateProfileDirectoryName(NewProfileName);

    public string CreateProfilePathPreview => Path.Combine(_launcherService.InstancesPath, CreateProfileDirectoryName);

    public string CreateProfileSelectedLoaderLabel =>
        string.IsNullOrWhiteSpace(NewProfileModLoader)
            ? "Nevybráno"
            : char.ToUpperInvariant(NewProfileModLoader[0]) + NewProfileModLoader[1..];

    public string CreateProfileStepTitle => CreateProfileStepIndex switch
    {
        0 => "Základ instance",
        1 => "Runtime a loader",
        _ => "Kontrola před vytvořením"
    };

    public string CreateProfileStepSubtitle => CreateProfileStepIndex switch
    {
        0 => "Vyber identitu packu, bootstrap variantu a základní release metadata.",
        1 => IsCreateProfileArchiveBootstrap
            ? "Archiv může runtime přepsat podle vlastního manifestu. Ruční volba slouží jako fallback a pro nové metadata."
            : "Navol Minecraft verzi a kompatibilní mod loader.",
        _ => "Potvrď bootstrap workspace, manifest a runtime nové creator instance."
    };

    public string CreateProfileRuntimeSummary =>
        $"Minecraft {NewProfileMcVersion} • {CreateProfileSelectedLoaderLabel} {NewProfileModLoaderVersion}";

    public string CreateProfileMetadataSummary =>
        $"{NewProfileSlug} • v{NewProfileVersion} • {NewProfileReleaseChannel}";

    public string CreateProfileBootstrapSummary => CreateProfileBootstrapModeLabel switch
    {
        "Template" => string.IsNullOrWhiteSpace(SelectedCreateProfileTemplateOption?.Label)
            ? "Template bootstrap připraví přednastavený creator workspace s výchozími dokumenty."
            : $"Template {SelectedCreateProfileTemplateOption.Label} přidá curated docs, notes a release baseline.",
        "Import CF" => HasCreateProfileBootstrapArchivePath
            ? $"Lokální CurseForge archiv se rozbalí přes existující installer do nového workspace."
            : "Vyber lokální CurseForge zip. Installer z něj vezme overrides a runtime metadata.",
        "Import MR" => HasCreateProfileBootstrapArchivePath
            ? "Lokální Modrinth archive bootstrap použije .mrpack manifest a doplní runtime podle balíčku."
            : "Vyber lokální .mrpack nebo .zip z Modrinthu.",
        "Clone Git" => string.IsNullOrWhiteSpace(NewProfileBootstrapGitUrl)
            ? "Git bootstrap naklonuje repozitář přímo do instance a zachová creator workflow nad pracovním stromem."
            : $"Git clone vezme zdroj {NewProfileBootstrapGitUrl.Trim()} a připraví workspace pro další práci.",
        "Restore Snapshot" => SelectedCreateProfileSnapshotOption == null
            ? "Restore snapshot obnoví existující config, docs a další workspace data do nové instance."
            : $"Snapshot restore převezme obsah z {SelectedCreateProfileSnapshotOption.Label}.",
        _ => "Blank bootstrap založí čistý creator workspace s manifestem a standardní strukturou složek."
    };

    public string CreateProfileBootstrapSourceSummary => CreateProfileBootstrapModeId switch
    {
        CreateProfileBootstrapTemplateId => SelectedCreateProfileTemplateOption?.Label ?? "Vyber template",
        CreateProfileBootstrapImportCfId or CreateProfileBootstrapImportMrId => string.IsNullOrWhiteSpace(NewProfileBootstrapArchivePath)
            ? "Žádný archiv"
            : NewProfileBootstrapArchivePath,
        CreateProfileBootstrapCloneGitId => string.IsNullOrWhiteSpace(NewProfileBootstrapGitUrl)
            ? "Žádný repozitář"
            : string.IsNullOrWhiteSpace(NewProfileBootstrapGitBranch)
                ? NewProfileBootstrapGitUrl.Trim()
                : $"{NewProfileBootstrapGitUrl.Trim()} • branch {NewProfileBootstrapGitBranch.Trim()}",
        CreateProfileBootstrapRestoreSnapshotId => SelectedCreateProfileSnapshotOption?.Label ?? "Žádný snapshot",
        _ => "Nový čistý workspace"
    };

    public string CreateProfileBootstrapPickerTitle => CreateProfileBootstrapModeId switch
    {
        CreateProfileBootstrapImportMrId => "Vyber Modrinth .mrpack nebo .zip archiv",
        _ => "Vyber CurseForge .zip archiv"
    };

    public string CreateProfileRuntimeBootstrapNote => CreateProfileBootstrapModeId switch
    {
        CreateProfileBootstrapImportCfId or CreateProfileBootstrapImportMrId => "Archiv může po importu přepsat Minecraft verzi a loader podle vlastního manifestu. Ruční volba se použije jen jako fallback, pokud balíček runtime neposkytne.",
        CreateProfileBootstrapCloneGitId => "Git clone zachová repozitářový obsah. Pokud repozitář už obsahuje creator_manifest.json, runtime a metadata se po bootstrapu sloučí s wizard hodnotami.",
        CreateProfileBootstrapRestoreSnapshotId => "Snapshot restore vrací configy, notes a další uložený stav. Runtime nové instance zůstává pod kontrolou wizardu.",
        _ => "Runtime nové instance se zapíše do creator_manifest.json hned při bootstrapu."
    };

    public string CreateProfileRuntimeStatus
    {
        get
        {
            if (IsCreateProfileArchiveBootstrap && HasCreateProfileBootstrapArchivePath)
            {
                return "Vybraný archiv má vlastní manifest. Po bootstrapu z něj převezmu runtime a ruční volba zůstane jen jako fallback.";
            }

            if (IsCreateProfileMcVersionsLoading)
            {
                return "Načítám dostupné Minecraft release verze...";
            }

            if (IsCreateProfileLoaderVersionsLoading)
            {
                return $"Načítám kompatibilní {CreateProfileSelectedLoaderLabel} buildy...";
            }

            if (AvailableMcVersions.Count == 0)
            {
                return "Minecraft verze se zatím nepodařilo načíst. Flow zůstává blokovaný, dokud nejsou k dispozici.";
            }

            if (AvailableModLoaderVersions.Count == 0)
            {
                return $"Pro kombinaci {NewProfileMcVersion} + {CreateProfileSelectedLoaderLabel} zatím nemám kompatibilní verzi loaderu.";
            }

            return $"Připraveno {AvailableModLoaderVersions.Count} kompatibilních verzí loaderu pro {NewProfileMcVersion}.";
        }
    }

    public string CreateProfileWizardHint
    {
        get
        {
            if (IsCreateProfileIdentityStep)
            {
                if (!ValidateCreateProfileName(out var identityError))
                {
                    return identityError;
                }

                if (!ValidateCreateProfileMetadata(out var metadataError))
                {
                    return metadataError;
                }

                if (!ValidateCreateProfileBootstrap(out var bootstrapError))
                {
                    return bootstrapError;
                }

                return CreateProfileBootstrapSummary;
            }

            if (IsCreateProfileRuntimeStep)
            {
                return ValidateCreateProfileRuntime(out var runtimeError)
                    ? CreateProfileRuntimeBootstrapNote
                    : runtimeError;
            }

            return CanCreateCustomProfile
                ? $"{CreateProfileBootstrapModeLabel} bootstrap založí release-ready creator workspace, zapíše creator_manifest.json a otevře detail nové instance."
                : "Wizard ještě není ve stavu, kdy může bezpečně založit instanci.";
        }
    }

    // ===== MOD MANAGEMENT FOR CUSTOM PROFILES =====

    [ObservableProperty]
    private ObservableCollection<ModpackItem> _profileModSearchResults = new();

    [ObservableProperty]
    private ObservableCollection<ModpackItem> _installedMods = new();

    [ObservableProperty]
    private ObservableCollection<ModpackItem> _filteredInstalledMods = new();

    [ObservableProperty]
    private string _installedModsSearchQuery = "";

    partial void OnInstalledModsSearchQueryChanged(string value)
    {
        RefreshFilteredInstalledMods();
    }

    [ObservableProperty]
    private string _profileModSearchQuery = "";

    [ObservableProperty]
    private bool _isProfileModSearchLoading;

    [ObservableProperty]
    private bool _profileModSearchUsedFallback;

    [ObservableProperty]
    private string _profileModSearchRuntimeLabel = string.Empty;

    // Pagination state
    private int _profileModSearchOffset;
    private string _profileModSearchLastQuery = string.Empty;
    private string _profileModSearchLastMcVersion = string.Empty;
    private string _profileModSearchLastModLoader = string.Empty;

    [ObservableProperty]
    private bool _canLoadMoreMods;

    [ObservableProperty]
    private bool _isLoadingMoreMods;

    public bool HasFilteredInstalledMods => FilteredInstalledMods.Count > 0;

    public bool HasProfileModSearchResults => ProfileModSearchResults.Count > 0;

    public int SelectedInstalledModsCount => InstalledMods.Count(mod => mod.IsSelected);

    public int SelectedProfileSearchModsCount => ProfileModSearchResults.Count(mod => mod.IsSelected && !mod.IsInstalled);

    public bool HasSelectedInstalledMods => SelectedInstalledModsCount > 0;

    public bool HasSelectedProfileSearchMods => SelectedProfileSearchModsCount > 0;

    public string InstalledModsSelectionSummary => HasSelectedInstalledMods
        ? $"Vybráno {SelectedInstalledModsCount} modů pro hromadnou akci."
        : "Vyber mody, které chceš zapnout, vypnout nebo odstranit najednou.";

    public string ProfileModSearchSelectionSummary => HasSelectedProfileSearchMods
        ? $"Vybráno {SelectedProfileSearchModsCount} výsledků k instalaci."
        : "Vyber výsledky a přidej je do workspace jedním krokem.";

    partial void OnProfileModSearchQueryChanged(string value)
    {
        OnPropertyChanged(nameof(CreatorModsSearchResultsSummary));
        OnPropertyChanged(nameof(ShowCreatorModsSearchEmptyState));
    }

    partial void OnIsProfileModSearchLoadingChanged(bool value)
    {
        NotifyProfileModSearchStateChanged();
    }

    partial void OnProfileModSearchUsedFallbackChanged(bool value)
    {
        NotifyProfileModSearchStateChanged();
    }

    partial void OnProfileModSearchRuntimeLabelChanged(string value)
    {
        NotifyProfileModSearchStateChanged();
    }

    public string InstalledModsEmptyTitle => string.IsNullOrWhiteSpace(InstalledModsSearchQuery)
        ? "Zatím žádné mody"
        : "Nic neodpovídá filtru";

    public string InstalledModsEmptySubtitle => string.IsNullOrWhiteSpace(InstalledModsSearchQuery)
        ? "Jakmile bude mít instance vlastní obsah, uvidíš ho tady."
        : $"Pro filtr \"{InstalledModsSearchQuery.Trim()}\" se nenašel žádný nainstalovaný mod.";

    private string GetDefaultCreatorAuthorName()
    {
        if (!string.IsNullOrWhiteSpace(UserSession?.Username) &&
            !string.Equals(UserSession.Username, "Guest", StringComparison.OrdinalIgnoreCase))
        {
            return UserSession.Username.Trim();
        }

        if (!string.IsNullOrWhiteSpace(ActiveAccount?.DisplayName))
        {
            return ActiveAccount.DisplayName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(Config.LastOfflineUsername))
        {
            return Config.LastOfflineUsername.Trim();
        }

        return string.Empty;
    }

    private CreatorManifest? LoadCreatorManifestForTargetModpack(ModpackInfo? targetModpack)
    {
        if (targetModpack == null)
        {
            return null;
        }

        if (CurrentModpackCreatorManifest != null &&
            string.Equals(_creatorMetadataWorkspaceId, targetModpack.Name, StringComparison.OrdinalIgnoreCase))
        {
            return CurrentModpackCreatorManifest;
        }

        var workspacePath = _launcherService.GetModpackPath(targetModpack.Name);
        return Directory.Exists(workspacePath)
            ? _creatorManifestService.LoadManifest(workspacePath)
            : null;
    }

    private string ResolveModManagementTargetMinecraftVersion(ModpackInfo targetModpack)
    {
        var creatorManifest = LoadCreatorManifestForTargetModpack(targetModpack);
        var installedManifest = TryLoadManifestInfo(targetModpack);

        return FirstNonEmpty(
            creatorManifest?.MinecraftVersion,
            installedManifest?.MinecraftVersion,
            targetModpack.CustomMcVersion);
    }

    private string ResolveModManagementTargetModLoader(ModpackInfo targetModpack)
    {
        var creatorManifest = LoadCreatorManifestForTargetModpack(targetModpack);
        var installedManifest = TryLoadManifestInfo(targetModpack);

        return FirstNonEmpty(
            creatorManifest?.ModLoader,
            installedManifest?.ModLoaderType,
            targetModpack.CustomModLoader);
    }

    private string BuildModSearchRuntimeLabel(ModpackInfo? targetModpack)
    {
        if (targetModpack == null)
        {
            return "Bez vybraného runtime.";
        }

        var minecraftVersion = ResolveModManagementTargetMinecraftVersion(targetModpack);
        var modLoader = ResolveModManagementTargetModLoader(targetModpack);

        if (string.IsNullOrWhiteSpace(minecraftVersion) && string.IsNullOrWhiteSpace(modLoader))
        {
            return "Bez runtime filtru. Vyhledávání poběží nad širším katalogem.";
        }

        return string.IsNullOrWhiteSpace(modLoader)
            ? $"Kompatibilita: Minecraft {minecraftVersion}"
            : $"Kompatibilita: Minecraft {minecraftVersion} • {modLoader}";
    }

    private async Task<List<ModpackItem>> SearchModsAcrossSourcesAsync(string query, string? gameVersion, string? modLoader, int offset = 0)
    {
        var mergedResults = new Dictionary<string, ModpackItem>(StringComparer.OrdinalIgnoreCase);

        await AppendCurseForgeSearchResultsAsync(mergedResults, query, gameVersion, modLoader, offset);
        await AppendModrinthSearchResultsAsync(mergedResults, query, gameVersion, modLoader, offset);

        return mergedResults.Values
            .OrderByDescending(mod => mod.DownloadCount)
            .ThenBy(mod => mod.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task AppendCurseForgeSearchResultsAsync(
        IDictionary<string, ModpackItem> results,
        string query,
        string? gameVersion,
        string? modLoader,
        int offset = 0)
    {
        try
        {
            var cfJson = await _curseForgeApi.SearchModsAsync(query, gameVersion, modLoader, offset);
            var cfData = JsonNode.Parse(cfJson)?["data"]?.AsArray();
            if (cfData == null)
            {
                return;
            }

            foreach (var mod in cfData)
            {
                var id = mod?["id"]?.ToString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                results[$"CurseForge:{id}"] = new ModpackItem
                {
                    Id = id,
                    Name = mod?["name"]?.ToString() ?? string.Empty,
                    Description = mod?["summary"]?.ToString() ?? string.Empty,
                    IconUrl = mod?["logo"]?["url"]?.ToString() ?? string.Empty,
                    Author = mod?["authors"]?.AsArray()?.FirstOrDefault()?["name"]?.ToString() ?? string.Empty,
                    Slug = mod?["slug"]?.ToString() ?? string.Empty,
                    DownloadCount = mod?["downloadCount"]?.GetValue<long>() ?? 0,
                    WebLink = mod?["links"]?["websiteUrl"]?.ToString() ?? string.Empty,
                    Source = "CurseForge"
                };
            }
        }
        catch (Exception ex)
        {
            LogService.Error("[SearchModsForProfile] CurseForge search failed", ex);
        }
    }

    private async Task AppendModrinthSearchResultsAsync(
        IDictionary<string, ModpackItem> results,
        string query,
        string? gameVersion,
        string? modLoader,
        int offset = 0)
    {
        try
        {
            var mrJson = await _modrinthApi.SearchModsAsync(query, gameVersion, modLoader, offset);
            var mrData = JsonNode.Parse(mrJson)?["hits"]?.AsArray();
            if (mrData == null)
            {
                return;
            }

            foreach (var mod in mrData)
            {
                var id = mod?["project_id"]?.ToString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                var slug = mod?["slug"]?.ToString() ?? string.Empty;
                results[$"Modrinth:{id}"] = new ModpackItem
                {
                    Id = id,
                    Name = mod?["title"]?.ToString() ?? string.Empty,
                    Description = mod?["description"]?.ToString() ?? string.Empty,
                    IconUrl = mod?["icon_url"]?.ToString() ?? string.Empty,
                    Author = mod?["author"]?.ToString() ?? string.Empty,
                    Slug = slug,
                    WebLink = string.IsNullOrWhiteSpace(slug)
                        ? string.Empty
                        : $"https://modrinth.com/mod/{slug}",
                    DownloadCount = mod?["downloads"]?.GetValue<long>() ?? 0,
                    Source = "Modrinth"
                };
            }
        }
        catch (Exception ex)
        {
            LogService.Error("[SearchModsForProfile] Modrinth search failed", ex);
        }
    }

    private ModpackInfo? ResolveModManagementTargetModpack()
    {
        return IsStreamingToolsView
            ? GetCreatorStudioSelectedModpack() ?? CurrentModpack
            : CurrentModpack;
    }

    private string? ResolveModManagementTargetPath()
    {
        var modpack = ResolveModManagementTargetModpack();
        return modpack == null ? null : _launcherService.GetModpackPath(modpack.Name);
    }

    private void NotifyInstalledModsStateChanged()
    {
        OnPropertyChanged(nameof(HasFilteredInstalledMods));
        OnPropertyChanged(nameof(SelectedInstalledModsCount));
        OnPropertyChanged(nameof(HasSelectedInstalledMods));
        OnPropertyChanged(nameof(InstalledModsSelectionSummary));
        OnPropertyChanged(nameof(InstalledModsEmptyTitle));
        OnPropertyChanged(nameof(InstalledModsEmptySubtitle));
        OnPropertyChanged(nameof(CreatorInstalledModsSummary));
        OnPropertyChanged(nameof(CreatorStudioModCountLabel));
        OnPropertyChanged(nameof(CanManageCreatorModsInPlace));
        OnPropertyChanged(nameof(CreatorModsWorkspaceStatus));
        OnPropertyChanged(nameof(CreatorModsFolderPath));
    }

    private void NotifyProfileModSearchStateChanged()
    {
        OnPropertyChanged(nameof(HasProfileModSearchResults));
        OnPropertyChanged(nameof(SelectedProfileSearchModsCount));
        OnPropertyChanged(nameof(HasSelectedProfileSearchMods));
        OnPropertyChanged(nameof(ProfileModSearchSelectionSummary));
        OnPropertyChanged(nameof(CreatorModsRuntimeLabel));
        OnPropertyChanged(nameof(CreatorModsCatalogModeLabel));
        OnPropertyChanged(nameof(CreatorModsSearchActionLabel));
        OnPropertyChanged(nameof(CreatorModsSearchResultsSummary));
        OnPropertyChanged(nameof(ShowCreatorModsSearchEmptyState));
    }

    private void ResetProfileModSearch()
    {
        if (!string.IsNullOrWhiteSpace(ProfileModSearchQuery))
        {
            ProfileModSearchQuery = string.Empty;
        }

        IsProfileModSearchLoading = false;
        ProfileModSearchUsedFallback = false;
        ProfileModSearchRuntimeLabel = string.Empty;
        IsLoadingMoreMods = false;
        CanLoadMoreMods = false;

        DetachModItemHandlers(ProfileModSearchResults, OnProfileSearchResultPropertyChanged);
        ProfileModSearchResults.Clear();

        NotifyProfileModSearchStateChanged();
    }

    private void ReplaceProfileModSearchResults(IEnumerable<ModpackItem> items)
    {
        DetachModItemHandlers(ProfileModSearchResults, OnProfileSearchResultPropertyChanged);
        ProfileModSearchResults.Clear();

        foreach (var item in items)
        {
            item.PropertyChanged += OnProfileSearchResultPropertyChanged;
            ProfileModSearchResults.Add(item);
        }

        NotifyProfileModSearchStateChanged();
    }

    private void ReplaceInstalledMods(IEnumerable<ModpackItem> items)
    {
        DetachModItemHandlers(InstalledMods, OnInstalledModPropertyChanged);
        InstalledMods.Clear();

        foreach (var item in items)
        {
            item.PropertyChanged += OnInstalledModPropertyChanged;
            InstalledMods.Add(item);
        }

        RefreshFilteredInstalledMods();
        SyncProfileSearchResultsWithInstalledMods();
    }

    private static void DetachModItemHandlers(IEnumerable<ModpackItem> items, PropertyChangedEventHandler handler)
    {
        foreach (var item in items)
        {
            item.PropertyChanged -= handler;
        }
    }

    private void OnInstalledModPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ModpackItem.IsSelected) || e.PropertyName == nameof(ModpackItem.IsEnabled))
        {
            NotifyInstalledModsStateChanged();
        }
    }

    private void OnProfileSearchResultPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ModpackItem.IsSelected) || e.PropertyName == nameof(ModpackItem.IsInstalled))
        {
            NotifyProfileModSearchStateChanged();
        }
    }

    private void SyncProfileSearchResultsWithInstalledMods()
    {
        foreach (var mod in ProfileModSearchResults)
        {
            var installedMatch = FindInstalledModMatch(mod);
            mod.IsInstalled = installedMatch != null;
            mod.InstalledFileName = installedMatch?.InstalledFileName ?? string.Empty;
            mod.IsEnabled = installedMatch?.IsEnabled ?? true;

            if (installedMatch != null)
            {
                mod.IsSelected = false;
            }
        }

        NotifyProfileModSearchStateChanged();
    }

    private ModpackItem? FindInstalledModMatch(ModpackItem candidate)
    {
        var exactMatch = InstalledMods.FirstOrDefault(installed =>
            !string.IsNullOrWhiteSpace(candidate.Id) &&
            string.Equals(installed.Id, candidate.Id, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(installed.Source, candidate.Source, StringComparison.OrdinalIgnoreCase));

        if (exactMatch != null)
        {
            return exactMatch;
        }

        var normalizedCandidateName = NormalizeModDisplayToken(candidate.Name);
        if (string.IsNullOrWhiteSpace(normalizedCandidateName))
        {
            return null;
        }

        return InstalledMods.FirstOrDefault(installed =>
            NormalizeModDisplayToken(installed.Name).Contains(normalizedCandidateName, StringComparison.OrdinalIgnoreCase) ||
            NormalizeModDisplayToken(installed.InstalledFileName).Contains(normalizedCandidateName, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeInstalledModFileName(string fileName)
    {
        return fileName.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase)
            ? fileName[..^".disabled".Length]
            : fileName;
    }

    private static string NormalizeModDisplayToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    }

    private static bool IsManagedModFile(string filePath)
    {
        return filePath.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) ||
               filePath.EndsWith(".jar.disabled", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetInstalledModMetadataPath(string modsDir, string fileName)
    {
        var normalizedFileName = NormalizeInstalledModFileName(fileName);
        return Path.Combine(modsDir, ".mod_metadata", normalizedFileName + ".json");
    }

    private static ModMetadata CreateInstalledModMetadata(ModpackItem mod, string fileName, bool isEnabled)
    {
        return new ModMetadata
        {
            FileName = NormalizeInstalledModFileName(fileName),
            Name = mod.Name ?? string.Empty,
            Slug = mod.Slug ?? string.Empty,
            ProjectId = mod.Id ?? string.Empty,
            FileId = mod.FileId ?? string.Empty,
            VersionId = mod.VersionId ?? string.Empty,
            Source = mod.Source ?? string.Empty,
            Summary = mod.Description ?? string.Empty,
            Author = mod.Author ?? string.Empty,
            IconUrl = mod.IconUrl ?? string.Empty,
            WebLink = mod.WebLink ?? string.Empty,
            DownloadUrl = mod.DownloadUrl ?? string.Empty,
            IsEnabled = isEnabled,
            InstalledAtUtc = DateTime.UtcNow
        };
    }

    // ===== CUSTOM PROFILE COMMANDS =====

    [RelayCommand]
    public void OpenCreateProfileModal()
    {
        InitializeCreateProfileBootstrapOptions();
        RebuildCreateProfileSnapshotOptions();
        var defaultAuthor = GetDefaultCreatorAuthorName();
        NewProfileName = "";
        _isCreateProfileSlugAutoSync = true;
        NewProfileSlug = "";
        NewProfileSummary = "";
        NewProfileAuthors = defaultAuthor;
        NewProfileVersion = DefaultCreateProfileVersion;
        NewProfileReleaseChannel = DefaultCreateProfileReleaseChannel;
        NewProfilePrimaryServer = "";
        NewProfileRecommendedRamMb = DefaultCreateProfileRecommendedRamMb;
        NewProfileBootstrapArchivePath = "";
        NewProfileBootstrapGitUrl = "";
        NewProfileBootstrapGitBranch = DefaultCreateProfileGitBranch;
        SelectedCreateProfileTemplateOption = CreateProfileTemplateOptions.FirstOrDefault();
        SelectedCreateProfileSnapshotOption = CreateProfileSnapshotOptions.FirstOrDefault();
        SelectedCreateProfileBootstrapOption = CreateProfileBootstrapOptions.FirstOrDefault(option => string.Equals(option.Id, CreateProfileBootstrapBlankId, StringComparison.OrdinalIgnoreCase))
            ?? CreateProfileBootstrapOptions.FirstOrDefault();
        NewProfileMcVersion = DefaultCreateProfileMcVersion;
        NewProfileModLoader = DefaultCreateProfileModLoader;
        NewProfileModLoaderVersion = string.Empty;
        CreateProfileStepIndex = 0;
        IsCreateProfileModalVisible = true;

        if (AvailableMcVersions.Count == 0)
        {
            _ = FetchMcVersionsAsync();
        }
        else
        {
            _ = FetchModLoaderVersionsAsync();
        }
    }

    [RelayCommand]
    public void CloseCreateProfileModal()
    {
        CreateProfileStepIndex = 0;
        IsCreateProfileModalVisible = false;
    }

    [RelayCommand]
    public void NextCreateProfileStep()
    {
        if (IsCreateProfileIdentityStep)
        {
            if (!ValidateCreateProfileName(out var identityError))
            {
                Greeting = identityError;
                NotifyCreateProfileWizardStateChanged();
                return;
            }

            if (!ValidateCreateProfileMetadata(out var metadataError))
            {
                Greeting = metadataError;
                NotifyCreateProfileWizardStateChanged();
                return;
            }

            if (!ValidateCreateProfileBootstrap(out var bootstrapError))
            {
                Greeting = bootstrapError;
                NotifyCreateProfileWizardStateChanged();
                return;
            }

            CreateProfileStepIndex = 1;
            return;
        }

        if (!IsCreateProfileRuntimeStep)
        {
            return;
        }

        if (!ValidateCreateProfileRuntime(out var runtimeError))
        {
            Greeting = runtimeError;
            NotifyCreateProfileWizardStateChanged();
            return;
        }

        CreateProfileStepIndex = 2;
    }

    [RelayCommand]
    public void PreviousCreateProfileStep()
    {
        if (CreateProfileStepIndex > 0)
        {
            CreateProfileStepIndex--;
        }
    }

    [RelayCommand]
    public async Task CreateCustomProfile()
    {
        if (!ValidateCreateProfileName(out var identityError))
        {
            Greeting = identityError;
            CreateProfileStepIndex = 0;
            return;
        }

        if (!ValidateCreateProfileMetadata(out var metadataError))
        {
            Greeting = metadataError;
            CreateProfileStepIndex = 0;
            return;
        }

        if (!ValidateCreateProfileBootstrap(out var bootstrapError))
        {
            Greeting = bootstrapError;
            CreateProfileStepIndex = 0;
            return;
        }

        if (!ValidateCreateProfileRuntime(out var runtimeError))
        {
            Greeting = runtimeError;
            CreateProfileStepIndex = 1;
            return;
        }

        var sanitizedName = CreateProfileDirectoryName;
        var instancePath = CreateProfilePathPreview;

        IsCreateProfileBootstrapBusy = true;
        try
        {
            Directory.CreateDirectory(instancePath);

            var bootstrapManifestInfo = await ApplyCreateProfileBootstrapAsync(instancePath);

            _creatorManifestService.EnsureWorkspaceStructure(instancePath);
            Directory.CreateDirectory(Path.Combine(instancePath, "mods"));

            var existingManifest = _creatorManifestService.LoadManifest(instancePath);
            var creatorManifest = BuildCreateProfileManifest(existingManifest, bootstrapManifestInfo);
            await _creatorManifestService.SaveManifestAsync(instancePath, creatorManifest);

            if (IsCreateProfileTemplateBootstrap && SelectedCreateProfileTemplateOption != null)
            {
                await ApplyCreateProfileTemplateFilesAsync(instancePath, SelectedCreateProfileTemplateOption.Id, creatorManifest);
            }

            var newProfile = new ModpackInfo
            {
                Name = sanitizedName,
                DisplayName = creatorManifest.PackName,
                Source = "Custom",
                IsCustomProfile = true,
                IsDeletable = true,
                CurrentVersion = new ModpackVersion
                {
                    Name = creatorManifest.Version,
                    FileId = bootstrapManifestInfo?.FileId > 0
                        ? bootstrapManifestInfo.FileId.ToString()
                        : $"custom-{CreateProfileBootstrapModeId}"
                }
            };

            ApplyCreatorManifestToModpack(newProfile, creatorManifest);
            UpsertInstalledCustomProfile(newProfile);

            Greeting = $"Vlastní profil '{sanitizedName}' vytvořen přes {CreateProfileBootstrapModeLabel} bootstrap.";
            ShowToast("Creator Studio", $"{CreateProfileBootstrapModeLabel} bootstrap dokončen pro {sanitizedName}.", ToastSeverity.Success, 3200);
            CloseCreateProfileModal();

            CurrentModpack = newProfile;
            GoToInstanceDetail(newProfile);
        }
        catch (Exception ex)
        {
            LogService.Error("[CreateCustomProfile] Bootstrap failed", ex);
            Greeting = $"Bootstrap nové instance selhal: {ex.Message}";
            ShowToast("Creator Studio", ex.Message, ToastSeverity.Error, 4200);
            try
            {
                if (Directory.Exists(instancePath))
                {
                    Directory.Delete(instancePath, true);
                }
            }
            catch
            {
                // Keep the partially created workspace for manual inspection if cleanup fails.
            }

            CreateProfileStepIndex = 0;
        }
        finally
        {
            IsCreateProfileBootstrapBusy = false;
        }
    }
    
    // ===== VERSION FETCHING =====

    private async Task FetchMcVersionsAsync()
    {
        IsCreateProfileMcVersionsLoading = true;
        try
        {
            using var client = new System.Net.Http.HttpClient();
            var json = await client.GetStringAsync("https://launchermeta.mojang.com/mc/game/version_manifest.json");
            var versions = JsonNode.Parse(json)?["versions"]?.AsArray();
            if (versions != null)
            {
                var releases = versions.Where(v => v?["type"]?.ToString() == "release")
                                       .Select(v => v?["id"]?.ToString())
                                       .Where(v => v != null)
                                       .ToList();
                
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    AvailableMcVersions.Clear();
                    foreach(var ver in releases.Take(50))
                    {
                        AvailableMcVersions.Add(ver!);
                    }
                    if (!AvailableMcVersions.Contains(NewProfileMcVersion))
                        NewProfileMcVersion = AvailableMcVersions.FirstOrDefault() ?? DefaultCreateProfileMcVersion;

                    NotifyCreateProfileWizardStateChanged();
                });
            }
        }
        catch (Exception ex)
        {
            LogService.Error("Failed to fetch MC versions", ex);
        }
        finally
        {
            IsCreateProfileMcVersionsLoading = false;
        }
    }

    private async Task FetchModLoaderVersionsAsync()
    {
        if (string.IsNullOrEmpty(NewProfileMcVersion) || string.IsNullOrEmpty(NewProfileModLoader)) return;

        var mcVer = NewProfileMcVersion;
        var loader = NewProfileModLoader.ToLower();

        IsCreateProfileLoaderVersionsLoading = true;
        try
        {
            using var client = new System.Net.Http.HttpClient();
            System.Collections.Generic.List<string> versions = new();
            
            if (loader == "fabric")
            {
                var json = await client.GetStringAsync($"https://meta.fabricmc.net/v2/versions/loader/{mcVer}");
                var array = JsonArray.Parse(json)?.AsArray();
                if (array != null)
                {
                    versions = array.Select(x => x?["loader"]?["version"]?.ToString())
                                    .Where(x => x != null)
                                    .Cast<string>()
                                    .ToList();
                }
            }
            else if (loader == "neoforge")
            {
                var json = await client.GetStringAsync($"https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/neoforge");
                var obj = JsonNode.Parse(json);
                var allVersions = obj?["versions"]?.AsArray();
                if (allVersions != null)
                {
                    var shortMcVer = mcVer.StartsWith("1.") ? mcVer.Substring(2) : mcVer;
                    var precisePrefix = shortMcVer + ".";
                    
                    versions = allVersions.Select(x => x?.ToString())
                                          .Where(x => x != null && (x.StartsWith(precisePrefix) || x == shortMcVer))
                                          .Cast<string>()
                                          .Reverse()
                                          .ToList();
                }
            }
            else if (loader == "forge")
            {
                var json = await client.GetStringAsync("https://files.minecraftforge.net/net/minecraftforge/forge/promotions_slim.json");
                var obj = JsonNode.Parse(json);
                var promos = obj?["promos"]?.AsObject();
                if (promos != null)
                {
                    var latest = promos[$"{mcVer}-latest"]?.ToString();
                    var recommended = promos[$"{mcVer}-recommended"]?.ToString();
                    
                    if (recommended != null) versions.Add(recommended);
                    if (latest != null && latest != recommended) versions.Add(latest);
                }
            }
            
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                AvailableModLoaderVersions.Clear();
                foreach(var v in versions.Take(20))
                {
                    AvailableModLoaderVersions.Add(v);
                }
                
                if (AvailableModLoaderVersions.Count > 0)
                {
                    NewProfileModLoaderVersion = AvailableModLoaderVersions.First();
                }
                else
                {
                    NewProfileModLoaderVersion = string.Empty;
                }

                NotifyCreateProfileWizardStateChanged();
            });
        }
        catch (Exception ex)
        {
            LogService.Error($"Failed to fetch {loader} versions for {mcVer}", ex);
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                AvailableModLoaderVersions.Clear();
                NewProfileModLoaderVersion = "";
                NotifyCreateProfileWizardStateChanged();
            });
        }
        finally
        {
            IsCreateProfileLoaderVersionsLoading = false;
        }
    }

    private static string BuildCreateProfileDirectoryName(string profileName)
    {
        var source = string.IsNullOrWhiteSpace(profileName) ? "voidcraft-instance" : profileName.Trim();
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitizedChars = source
            .Select(character => invalidChars.Contains(character) ? '-' : character)
            .ToArray();

        var sanitized = new string(sanitizedChars)
            .Replace(' ', '-')
            .Trim('.', ' ', '-', '_');

        return string.IsNullOrWhiteSpace(sanitized) ? "voidcraft-instance" : sanitized;
    }

    private void SyncCreateProfileSlugFromName(string profileName)
    {
        if (!_isCreateProfileSlugAutoSync && !string.IsNullOrWhiteSpace(NewProfileSlug))
        {
            return;
        }

        _isUpdatingCreateProfileSlug = true;
        NewProfileSlug = BuildCreateProfileSlug(profileName);
        _isUpdatingCreateProfileSlug = false;
    }

    private static string BuildCreateProfileSlug(string source)
    {
        return CreatorManifestService.BuildSlug(source);
    }

    private bool ValidateCreateProfileName(out string error)
    {
        if (string.IsNullOrWhiteSpace(NewProfileName))
        {
            error = "Zadej název instance, než přejdeš dál.";
            return false;
        }

        var instancePath = CreateProfilePathPreview;
        if (Directory.Exists(instancePath) && Directory.EnumerateFileSystemEntries(instancePath).Any())
        {
            error = "Instance s tímto názvem už v hlavním launcher workspace existuje.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private bool ValidateCreateProfileMetadata(out string error)
    {
        if (string.IsNullOrWhiteSpace(NewProfileSlug))
        {
            error = "Slug je povinný, aby bootstrap rovnou vytvořil stabilní identitu packu.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(NewProfileVersion))
        {
            error = "Version je povinná pro první creator_manifest.json.";
            return false;
        }

        if (!int.TryParse(NewProfileRecommendedRamMb, out var recommendedRamMb) || recommendedRamMb < 2048)
        {
            error = "Recommended RAM musí být číslo alespoň 2048 MB.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private bool ValidateCreateProfileBootstrap(out string error)
    {
        if (SelectedCreateProfileBootstrapOption == null)
        {
            error = "Vyber bootstrap variantu nové instance.";
            return false;
        }

        switch (CreateProfileBootstrapModeId)
        {
            case CreateProfileBootstrapTemplateId:
                if (SelectedCreateProfileTemplateOption == null)
                {
                    error = "Template bootstrap potřebuje vybraný preset workspace.";
                    return false;
                }

                break;
            case CreateProfileBootstrapImportCfId:
                if (!ValidateCreateProfileArchivePath(new[] { ".zip" }, out error))
                {
                    return false;
                }

                return true;
            case CreateProfileBootstrapImportMrId:
                if (!ValidateCreateProfileArchivePath(new[] { ".mrpack", ".zip" }, out error))
                {
                    return false;
                }

                return true;
            case CreateProfileBootstrapCloneGitId:
                if (string.IsNullOrWhiteSpace(NewProfileBootstrapGitUrl))
                {
                    error = "Git bootstrap potřebuje URL repozitáře nebo SSH remote.";
                    return false;
                }

                if (!IsLikelyGitRemote(NewProfileBootstrapGitUrl))
                {
                    error = "Git remote nevypadá jako platná HTTPS/SSH adresa repozitáře.";
                    return false;
                }

                break;
            case CreateProfileBootstrapRestoreSnapshotId:
                if (SelectedCreateProfileSnapshotOption == null || !Directory.Exists(SelectedCreateProfileSnapshotOption.Id))
                {
                    error = "Obnovení vyžaduje existující zálohu instance.";
                    return false;
                }

                break;
        }

        error = string.Empty;
        return true;
    }

    private IEnumerable<string> SplitCreateProfileAuthors()
    {
        return NewProfileAuthors.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(author => author.Trim())
            .Where(author => !string.IsNullOrWhiteSpace(author));
    }

    private bool ValidateCreateProfileRuntime(out string error)
    {
        if (IsCreateProfileArchiveBootstrap && HasCreateProfileBootstrapArchivePath)
        {
            error = string.Empty;
            return true;
        }

        if (IsCreateProfileMcVersionsLoading || IsCreateProfileLoaderVersionsLoading)
        {
            error = "Počkej, až wizard dokončí načtení verzí a loader buildu.";
            return false;
        }

        if (AvailableMcVersions.Count == 0 || string.IsNullOrWhiteSpace(NewProfileMcVersion))
        {
            error = "Minecraft verze ještě nejsou připravené. Bez nich wizard instanci nezaloží.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(NewProfileModLoader))
        {
            error = "Vyber mod loader pro novou instanci.";
            return false;
        }

        if (AvailableModLoaderVersions.Count == 0 || string.IsNullOrWhiteSpace(NewProfileModLoaderVersion))
        {
            error = $"Pro kombinaci {NewProfileMcVersion} + {CreateProfileSelectedLoaderLabel} není připravená kompatibilní verze loaderu.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private void NotifyCreateProfileWizardStateChanged()
    {
        OnPropertyChanged(nameof(IsCreateProfileIdentityStep));
        OnPropertyChanged(nameof(IsCreateProfileRuntimeStep));
        OnPropertyChanged(nameof(IsCreateProfileReviewStep));
        OnPropertyChanged(nameof(CanGoBackCreateProfileStep));
        OnPropertyChanged(nameof(CanAdvanceCreateProfileStep));
        OnPropertyChanged(nameof(CanCreateCustomProfile));
        OnPropertyChanged(nameof(IsCreateProfileBlankBootstrap));
        OnPropertyChanged(nameof(IsCreateProfileTemplateBootstrap));
        OnPropertyChanged(nameof(IsCreateProfileCurseForgeImportBootstrap));
        OnPropertyChanged(nameof(IsCreateProfileModrinthImportBootstrap));
        OnPropertyChanged(nameof(IsCreateProfileArchiveBootstrap));
        OnPropertyChanged(nameof(IsCreateProfileCloneGitBootstrap));
        OnPropertyChanged(nameof(IsCreateProfileRestoreSnapshotBootstrap));
        OnPropertyChanged(nameof(HasCreateProfileBootstrapArchivePath));
        OnPropertyChanged(nameof(HasCreateProfileSnapshotOptions));
        OnPropertyChanged(nameof(HasCreateProfileSelectedSnapshot));
        OnPropertyChanged(nameof(CreateProfileBootstrapModeLabel));
        OnPropertyChanged(nameof(CreateProfileBootstrapSummary));
        OnPropertyChanged(nameof(CreateProfileBootstrapSourceSummary));
        OnPropertyChanged(nameof(CreateProfileBootstrapPickerTitle));
        OnPropertyChanged(nameof(CreateProfileRuntimeBootstrapNote));
        OnPropertyChanged(nameof(CreateProfileDirectoryName));
        OnPropertyChanged(nameof(CreateProfilePathPreview));
        OnPropertyChanged(nameof(CreateProfileSelectedLoaderLabel));
        OnPropertyChanged(nameof(CreateProfileStepTitle));
        OnPropertyChanged(nameof(CreateProfileStepSubtitle));
        OnPropertyChanged(nameof(CreateProfileMetadataSummary));
        OnPropertyChanged(nameof(CreateProfileRuntimeSummary));
        OnPropertyChanged(nameof(CreateProfileRuntimeStatus));
        OnPropertyChanged(nameof(CreateProfileWizardHint));
    }

    [RelayCommand]
    private async Task BrowseCreateProfileBootstrapArchive()
    {
        var storageProvider = (Avalonia.Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)
            ?.MainWindow?.StorageProvider;

        if (storageProvider == null)
        {
            ShowToast("Creator Studio", "Souborový picker není v tomto režimu dostupný.", ToastSeverity.Warning);
            return;
        }

        var fileTypes = IsCreateProfileModrinthImportBootstrap
            ? new[]
            {
                new Avalonia.Platform.Storage.FilePickerFileType("Modrinth Pack")
                {
                    Patterns = new[] { "*.mrpack", "*.zip" }
                }
            }
            : new[]
            {
                new Avalonia.Platform.Storage.FilePickerFileType("CurseForge Pack")
                {
                    Patterns = new[] { "*.zip" }
                }
            };

        var files = await storageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = CreateProfileBootstrapPickerTitle,
            AllowMultiple = false,
            FileTypeFilter = fileTypes
        });

        var selectedPath = files.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(selectedPath))
        {
            NewProfileBootstrapArchivePath = selectedPath;
        }
    }

    [RelayCommand]
    private void ClearCreateProfileBootstrapArchive()
    {
        NewProfileBootstrapArchivePath = string.Empty;
    }

    [RelayCommand]
    private void RefreshCreateProfileBootstrapSnapshots()
    {
        RebuildCreateProfileSnapshotOptions();
        SelectedCreateProfileSnapshotOption ??= CreateProfileSnapshotOptions.FirstOrDefault();
        ShowToast("Creator Studio", "Seznam snapshotů pro bootstrap byl obnoven.", ToastSeverity.Success, 2200);
    }

    private void InitializeCreateProfileBootstrapOptions()
    {
        if (CreateProfileBootstrapOptions.Count == 0)
        {
            CreateProfileBootstrapOptions.Add(new SelectionOption { Id = CreateProfileBootstrapBlankId, Label = "Blank" });
            CreateProfileBootstrapOptions.Add(new SelectionOption { Id = CreateProfileBootstrapTemplateId, Label = "Template" });
            CreateProfileBootstrapOptions.Add(new SelectionOption { Id = CreateProfileBootstrapImportCfId, Label = "Import CF" });
            CreateProfileBootstrapOptions.Add(new SelectionOption { Id = CreateProfileBootstrapImportMrId, Label = "Import MR" });
            CreateProfileBootstrapOptions.Add(new SelectionOption { Id = CreateProfileBootstrapCloneGitId, Label = "Clone Git" });
            CreateProfileBootstrapOptions.Add(new SelectionOption { Id = CreateProfileBootstrapRestoreSnapshotId, Label = "Restore Snapshot" });
        }

        if (CreateProfileTemplateOptions.Count == 0)
        {
            CreateProfileTemplateOptions.Add(new SelectionOption { Id = CreateProfileTemplateFabricLiteId, Label = "Fabric Lite Base" });
            CreateProfileTemplateOptions.Add(new SelectionOption { Id = CreateProfileTemplateForgeServerId, Label = "Forge Server First" });
            CreateProfileTemplateOptions.Add(new SelectionOption { Id = CreateProfileTemplateNeoForgeAdventureId, Label = "NeoForge Adventure" });
        }
    }

    private void RebuildCreateProfileSnapshotOptions()
    {
        CreateProfileSnapshotOptions.Clear();

        foreach (var modpack in InstalledModpacks.OrderBy(modpack => modpack.Name, StringComparer.OrdinalIgnoreCase))
        {
            var modpackPath = _launcherService.GetModpackPath(modpack.Name);
            foreach (var snapshotPath in GetBackupSnapshotDirectories(modpack.Name, modpackPath)
                .OrderByDescending(path => Directory.GetCreationTime(path)))
            {
                var snapshot = CreateBackupSnapshot(snapshotPath);
                CreateProfileSnapshotOptions.Add(new SelectionOption
                {
                    Id = snapshot.FullPath,
                    Label = $"{modpack.Name} • {snapshot.Name} • {snapshot.CreatedAt:dd.MM.yyyy HH:mm}"
                });
            }
        }
    }

    private bool IsCreateProfileBootstrapMode(string id)
    {
        return string.Equals(CreateProfileBootstrapModeId, id, StringComparison.OrdinalIgnoreCase);
    }

    private string CreateProfileBootstrapModeId => SelectedCreateProfileBootstrapOption?.Id ?? CreateProfileBootstrapBlankId;

    private void ApplyCreateProfileTemplateDefaults(string templateId)
    {
        switch (templateId)
        {
            case CreateProfileTemplateFabricLiteId:
                NewProfileModLoader = "fabric";
                NewProfileReleaseChannel = "alpha";
                NewProfileRecommendedRamMb = "8192";
                if (string.IsNullOrWhiteSpace(NewProfileSummary))
                {
                    NewProfileSummary = "Lehký Fabric workspace pro rychlé iterace, utility mody a čistý onboarding do prvního release.";
                }

                break;
            case CreateProfileTemplateForgeServerId:
                NewProfileModLoader = "forge";
                NewProfileReleaseChannel = "playtest";
                NewProfileRecommendedRamMb = "10240";
                if (string.IsNullOrWhiteSpace(NewProfileSummary))
                {
                    NewProfileSummary = "Server-first Forge základ s důrazem na stabilitu, kooperaci a jasné provozní guardraily.";
                }

                break;
            case CreateProfileTemplateNeoForgeAdventureId:
                NewProfileModLoader = "neoforge";
                NewProfileReleaseChannel = "alpha";
                NewProfileRecommendedRamMb = "12288";
                if (string.IsNullOrWhiteSpace(NewProfileSummary))
                {
                    NewProfileSummary = "NeoForge foundation pro progression-heavy pack s delším objevováním, gatingem a release rytmem.";
                }

                break;
        }
    }

    private async Task<ModpackManifestInfo?> ApplyCreateProfileBootstrapAsync(string instancePath)
    {
        switch (CreateProfileBootstrapModeId)
        {
            case CreateProfileBootstrapImportCfId:
            case CreateProfileBootstrapImportMrId:
                ShowToast("Creator Studio", $"Importuji {CreateProfileBootstrapModeLabel} archiv do nového workspace.", ToastSeverity.Info, 2200);
                return await _modpackInstaller.InstallOrUpdateAsync(NewProfileBootstrapArchivePath.Trim(), instancePath);
            case CreateProfileBootstrapCloneGitId:
                ShowToast("Creator Studio", "Klonuji git workspace do nové instance.", ToastSeverity.Info, 2200);
                await CloneGitWorkspaceAsync(instancePath, NewProfileBootstrapGitUrl.Trim(), NewProfileBootstrapGitBranch.Trim());
                return null;
            case CreateProfileBootstrapRestoreSnapshotId:
                ShowToast("Creator Studio", "Obnovuji zvolený snapshot do nové instance.", ToastSeverity.Info, 2200);
                await RestoreSnapshotWorkspaceAsync(instancePath, SelectedCreateProfileSnapshotOption?.Id ?? string.Empty);
                return null;
            default:
                return null;
        }
    }

    private CreatorManifest BuildCreateProfileManifest(CreatorManifest? existingManifest, ModpackManifestInfo? bootstrapManifestInfo)
    {
        var authors = SplitCreateProfileAuthors().ToArray();
        if (authors.Length == 0 && existingManifest?.Authors.Count > 0)
        {
            authors = existingManifest.Authors.ToArray();
        }
        else if (authors.Length == 0 && !string.IsNullOrWhiteSpace(bootstrapManifestInfo?.Author))
        {
            authors = new[] { bootstrapManifestInfo.Author.Trim() };
        }
        else if (authors.Length == 0)
        {
            var defaultAuthor = GetDefaultCreatorAuthorName();
            if (!string.IsNullOrWhiteSpace(defaultAuthor))
            {
                authors = new[] { defaultAuthor };
            }
        }

        var version = string.IsNullOrWhiteSpace(NewProfileVersion)
            ? existingManifest?.Version ?? DefaultCreateProfileVersion
            : NewProfileVersion.Trim();

        if (string.Equals(version, DefaultCreateProfileVersion, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(bootstrapManifestInfo?.Version))
        {
            version = bootstrapManifestInfo.Version.Trim();
        }

        var minecraftVersion = !string.IsNullOrWhiteSpace(bootstrapManifestInfo?.MinecraftVersion)
            ? bootstrapManifestInfo.MinecraftVersion.Trim()
            : !string.IsNullOrWhiteSpace(existingManifest?.MinecraftVersion)
                ? existingManifest.MinecraftVersion.Trim()
                : NewProfileMcVersion;

        var modLoader = !string.IsNullOrWhiteSpace(bootstrapManifestInfo?.ModLoaderType)
            ? bootstrapManifestInfo.ModLoaderType.Trim()
            : !string.IsNullOrWhiteSpace(existingManifest?.ModLoader)
                ? existingManifest.ModLoader.Trim()
                : NewProfileModLoader;

        var modLoaderVersion = ResolveCreateProfileModLoaderVersion(bootstrapManifestInfo, existingManifest, modLoader);

        return _creatorManifestService.CreateManifest(
            NewProfileName.Trim(),
            string.IsNullOrWhiteSpace(NewProfileSlug) ? existingManifest?.Slug ?? BuildCreateProfileSlug(NewProfileName) : NewProfileSlug.Trim(),
            string.IsNullOrWhiteSpace(NewProfileSummary) ? existingManifest?.Summary ?? string.Empty : NewProfileSummary,
            authors,
            version,
            minecraftVersion,
            modLoader,
            modLoaderVersion,
            int.Parse(NewProfileRecommendedRamMb),
            string.IsNullOrWhiteSpace(NewProfilePrimaryServer) ? existingManifest?.PrimaryServer ?? string.Empty : NewProfilePrimaryServer,
            string.IsNullOrWhiteSpace(NewProfileReleaseChannel) ? existingManifest?.ReleaseChannel ?? DefaultCreateProfileReleaseChannel : NewProfileReleaseChannel,
            existingManifest?.CreatedAtUtc);
    }

    private string ResolveCreateProfileModLoaderVersion(ModpackManifestInfo? bootstrapManifestInfo, CreatorManifest? existingManifest, string modLoader)
    {
        if (!string.IsNullOrWhiteSpace(bootstrapManifestInfo?.ModLoaderId))
        {
            var prefix = string.IsNullOrWhiteSpace(modLoader) ? string.Empty : modLoader.Trim() + "-";
            if (!string.IsNullOrWhiteSpace(prefix) && bootstrapManifestInfo.ModLoaderId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return bootstrapManifestInfo.ModLoaderId[prefix.Length..];
            }

            return bootstrapManifestInfo.ModLoaderId.Trim();
        }

        return !string.IsNullOrWhiteSpace(existingManifest?.ModLoaderVersion)
            ? existingManifest.ModLoaderVersion.Trim()
            : NewProfileModLoaderVersion;
    }

    private async Task CloneGitWorkspaceAsync(string instancePath, string remoteUrl, string branch)
    {
        var parentDirectory = Path.GetDirectoryName(instancePath) ?? _launcherService.InstancesPath;
        Directory.CreateDirectory(parentDirectory);

        var arguments = new List<string> { "clone", "--recursive" };
        if (!string.IsNullOrWhiteSpace(branch))
        {
            arguments.Add("--branch");
            arguments.Add(branch);
        }

        arguments.Add(remoteUrl);
        arguments.Add(instancePath);

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "git",
            Arguments = string.Join(" ", arguments.Select(QuoteCommandLineArgument)),
            WorkingDirectory = parentDirectory,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        }) ?? throw new InvalidOperationException("Git proces se nepodařilo spustit.");

        var stdOutTask = process.StandardOutput.ReadToEndAsync();
        var stdErrTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        var stdErr = (await stdErrTask).Trim();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(stdErr)
                ? $"git clone skončil kódem {process.ExitCode}."
                : stdErr);
        }

        await stdOutTask;
    }

    private async Task RestoreSnapshotWorkspaceAsync(string instancePath, string snapshotPath)
    {
        if (string.IsNullOrWhiteSpace(snapshotPath) || !Directory.Exists(snapshotPath))
        {
            throw new InvalidOperationException("Vybraný snapshot už neexistuje.");
        }

        await Task.Run(() => CopyDirectoryContents(snapshotPath, instancePath));
    }

    private async Task ApplyCreateProfileTemplateFilesAsync(string instancePath, string templateId, CreatorManifest manifest)
    {
        var files = BuildCreateProfileTemplateFiles(templateId, manifest);
        foreach (var entry in files)
        {
            var fullPath = Path.Combine(instancePath, entry.Key.Replace('/', Path.DirectorySeparatorChar));
            var parent = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(parent))
            {
                Directory.CreateDirectory(parent);
            }

            await File.WriteAllTextAsync(fullPath, entry.Value);
        }
    }

    private static IReadOnlyDictionary<string, string> BuildCreateProfileTemplateFiles(string templateId, CreatorManifest manifest)
    {
        var authorLabel = manifest.Authors.Count > 0 ? string.Join(", ", manifest.Authors) : "VOID-CRAFT creator";
        return templateId switch
        {
            CreateProfileTemplateFabricLiteId => new Dictionary<string, string>
            {
                ["docs/workspace-brief.md"] = $"# {manifest.PackName}\n\nLehký Fabric workspace připravený pro rychlé iterace, utility obsah a první veřejné playtesty.\n\n## Identita\n- Slug: {manifest.Slug}\n- Release channel: {manifest.ReleaseChannel}\n- Runtime: Minecraft {manifest.MinecraftVersion} / {manifest.ModLoader} {manifest.ModLoaderVersion}\n\n## Kurátorský rámec\n- onboarding musí zůstat krátký a čitelný\n- utility obsah má být stabilní i na serveru\n- první release má zůstat malý a snadno testovatelný\n\n## Vlastník\n{authorLabel}\n",
                ["notes/design-pillars.md"] = "# Design Pillars\n\n1. Čitelný survival loop bez hlučných systémů.\n2. Menší počet módů s jasnou rolí místo širokého katalogu.\n3. Každá nová feature musí zlepšit první dvě hodiny hry.\n",
                ["qa/release-gate.md"] = "# Release Gate\n\n- klient naběhne bez chybového logu\n- nový hráč dostane srozumitelný první cíl\n- server start a join workflow zůstávají stabilní\n- změny v configu mají snapshot a stručný changelog\n"
            },
            CreateProfileTemplateForgeServerId => new Dictionary<string, string>
            {
                ["docs/workspace-brief.md"] = $"# {manifest.PackName}\n\nForge server-first workspace pro dlouhodobější kooperaci a čistý provozní režim.\n\n## Identita\n- Slug: {manifest.Slug}\n- Release channel: {manifest.ReleaseChannel}\n- Runtime: Minecraft {manifest.MinecraftVersion} / {manifest.ModLoader} {manifest.ModLoaderVersion}\n\n## Provozní pravidla\n- server má prioritu před klientskými experimenty\n- nové systémy se validují na dedikovaném save\n- každá změna balancu musí mít krátký zápis v notes\n\n## Vlastník\n{authorLabel}\n",
                ["notes/server-ops.md"] = "# Server Ops\n\nDrž stabilní whitelist feature, sleduj výkon při generaci chunků a každou síťově citlivou změnu validuj na čistém restartu serveru.\n",
                ["qa/playtest-matrix.md"] = "# Playtest Matrix\n\n- clean join bez resourcepack edge casů\n- běžný coop loop pro 2-4 hráče\n- synchronizace questů a serverových permission flow\n- obnova po restartu serveru bez ztráty postupu\n"
            },
            _ => new Dictionary<string, string>
            {
                ["docs/workspace-brief.md"] = $"# {manifest.PackName}\n\nNeoForge adventure workspace pro progression, gating a delší release cadence.\n\n## Identita\n- Slug: {manifest.Slug}\n- Release channel: {manifest.ReleaseChannel}\n- Runtime: Minecraft {manifest.MinecraftVersion} / {manifest.ModLoader} {manifest.ModLoaderVersion}\n\n## Produktový směr\n- každý tier musí otevřít novou vrstvu obsahu\n- questy mají vysvětlovat směr, ne suplovat wiki\n- release musí mít čitelný changelog a rollback snapshot\n\n## Vlastník\n{authorLabel}\n",
                ["quests/progression-outline.md"] = "# Progression Outline\n\n1. Základní ekonomika a první craft loop.\n2. Technologický tier s novými cíli pro server i solo play.\n3. Midgame expanze s novým biomem nebo dimenzí.\n4. Release candidate vrstva s finálním gatingem a QA kontrolou.\n",
                ["qa/release-gate.md"] = "# Release Gate\n\n- nový build projde čistým startem klienta\n- questline má uzavřený hlavní tok bez slepých míst\n- snapshot před config apply existuje a je čitelně označený\n- export obsahuje jen soubory, které patří do release\n"
            }
        };
    }

    private void ApplyCreatorManifestToModpack(ModpackInfo modpack, CreatorManifest manifest)
    {
        modpack.DisplayName = manifest.PackName;
        modpack.Author = manifest.Authors.Count > 0 ? string.Join(", ", manifest.Authors) : modpack.Author;
        modpack.Description = manifest.Summary;
        modpack.CustomMcVersion = manifest.MinecraftVersion;
        modpack.CustomModLoader = manifest.ModLoader;
        modpack.CustomModLoaderVersion = manifest.ModLoaderVersion;
        modpack.IsCustomProfile = true;
        modpack.CurrentVersion ??= new ModpackVersion();
        modpack.CurrentVersion.Name = manifest.Version;
    }

    private void UpsertInstalledCustomProfile(ModpackInfo modpack)
    {
        var existing = InstalledModpacks.FirstOrDefault(candidate => string.Equals(candidate.Name, modpack.Name, StringComparison.OrdinalIgnoreCase));
        if (existing == null)
        {
            InstalledModpacks.Add(modpack);
        }
        else
        {
            existing.Source = modpack.Source;
            existing.DisplayName = modpack.DisplayName;
            existing.IsCustomProfile = modpack.IsCustomProfile;
            existing.IsDeletable = modpack.IsDeletable;
            existing.Author = modpack.Author;
            existing.Description = modpack.Description;
            existing.CustomMcVersion = modpack.CustomMcVersion;
            existing.CustomModLoader = modpack.CustomModLoader;
            existing.CustomModLoaderVersion = modpack.CustomModLoaderVersion;
            existing.CurrentVersion = modpack.CurrentVersion;
        }

        SaveModpacks();
        RefreshCurrentModpackCreatorManifest();
    }

    private void SyncInstalledModpackFromCreatorManifest(string workspaceId, CreatorManifest manifest)
    {
        var modpack = InstalledModpacks.FirstOrDefault(candidate => string.Equals(candidate.Name, workspaceId, StringComparison.OrdinalIgnoreCase));
        if (modpack == null)
        {
            return;
        }

        ApplyCreatorManifestToModpack(modpack, manifest);

        var workspacePath = _launcherService.GetModpackPath(workspaceId);
        var localLogoPath = _creatorAssetsService.GetAssetPath(workspacePath, BrandingAssetSlot.Logo);
        if (!string.IsNullOrWhiteSpace(localLogoPath))
        {
            modpack.LogoUrl = localLogoPath;
        }

        SaveModpacks();

        if (CurrentModpack != null && string.Equals(CurrentModpack.Name, workspaceId, StringComparison.OrdinalIgnoreCase))
        {
            RefreshCurrentModpackCreatorManifest();
        }
    }

    private bool ValidateCreateProfileArchivePath(IEnumerable<string> allowedExtensions, out string error)
    {
        if (string.IsNullOrWhiteSpace(NewProfileBootstrapArchivePath))
        {
            error = "Bootstrap archiv zatím není vybraný.";
            return false;
        }

        if (!File.Exists(NewProfileBootstrapArchivePath))
        {
            error = "Vybraný bootstrap archiv na disku neexistuje.";
            return false;
        }

        var extension = Path.GetExtension(NewProfileBootstrapArchivePath);
        if (!allowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            error = $"Vybraný archiv musí mít příponu {string.Join(", ", allowedExtensions)}.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool IsLikelyGitRemote(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (value.StartsWith("git@", StringComparison.OrdinalIgnoreCase))
        {
            return value.Contains(':');
        }

        return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == "ssh");
    }

    private static string QuoteCommandLineArgument(string value)
    {
        return $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
    }

    private static void CopyDirectoryContents(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        foreach (var directory in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDir, directory);
            Directory.CreateDirectory(Path.Combine(targetDir, relativePath));
        }

        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDir, file);
            var destination = Path.Combine(targetDir, relativePath);
            var parent = Path.GetDirectoryName(destination);
            if (!string.IsNullOrWhiteSpace(parent))
            {
                Directory.CreateDirectory(parent);
            }

            File.Copy(file, destination, true);
        }
    }

    // ===== MOD SEARCH & MANAGEMENT =====

    public async Task LoadPopularModsForProfile()
    {
        if (ProfileModSearchResults.Count > 0) return; // don't overwrite active search results
        var savedQuery = ProfileModSearchQuery;
        ProfileModSearchQuery = string.Empty;
        await SearchModsForProfile();
        ProfileModSearchQuery = savedQuery ?? string.Empty;
    }

    [RelayCommand]
    public async Task SearchModsForProfile()
    {
        var targetModpack = ResolveModManagementTargetModpack();
        if (targetModpack == null || !targetModpack.IsCustomProfile)
        {
            ResetProfileModSearch();
            return;
        }

        var query = ProfileModSearchQuery?.Trim() ?? string.Empty;
        var isBrowseMode = string.IsNullOrWhiteSpace(query);

        LoadInstalledMods();
        var minecraftVersion = ResolveModManagementTargetMinecraftVersion(targetModpack);
        var modLoader = ResolveModManagementTargetModLoader(targetModpack);

        // Store pagination state
        _profileModSearchOffset = 0;
        _profileModSearchLastQuery = query;
        _profileModSearchLastMcVersion = minecraftVersion ?? string.Empty;
        _profileModSearchLastModLoader = modLoader ?? string.Empty;

        ProfileModSearchRuntimeLabel = BuildModSearchRuntimeLabel(targetModpack);
        ProfileModSearchUsedFallback = false;
        IsProfileModSearchLoading = true;
        Greeting = isBrowseMode ? "Načítám nejpopulárnější mody..." : $"Hledám mody: {query}...";

        try
        {
            var results = await SearchModsAcrossSourcesAsync(query, minecraftVersion, modLoader);
            if (results.Count == 0 && !isBrowseMode && (!string.IsNullOrWhiteSpace(minecraftVersion) || !string.IsNullOrWhiteSpace(modLoader)))
            {
                results = await SearchModsAcrossSourcesAsync(query, string.Empty, modLoader);
                ProfileModSearchUsedFallback = results.Count > 0;
            }

            if (results.Count == 0 && !isBrowseMode && !string.IsNullOrWhiteSpace(modLoader))
            {
                results = await SearchModsAcrossSourcesAsync(query, string.Empty, string.Empty);
                ProfileModSearchUsedFallback = results.Count > 0;
            }

            ReplaceProfileModSearchResults(results);
            SyncProfileSearchResultsWithInstalledMods();
            _profileModSearchOffset = results.Count;
            CanLoadMoreMods = results.Count >= 20; // at least 20 results suggests more pages exist

            Greeting = ProfileModSearchResults.Count == 0
                ? (isBrowseMode ? "Katalog modů je prázdný." : $"Pro {query} se nic nenašlo.")
                : isBrowseMode
                    ? $"Zobrazeno {ProfileModSearchResults.Count} nejpopulárnějších modů."
                    : ProfileModSearchUsedFallback
                        ? $"Nalezeno {ProfileModSearchResults.Count} modů po rozšíření hledání mimo přesný runtime filtr."
                        : $"Nalezeno {ProfileModSearchResults.Count} kompatibilních modů.";
        }
        catch (Exception ex)
        {
            Greeting = $"Chyba hledání: {ex.Message}";
            NotifyProfileModSearchStateChanged();

            if (IsStreamingToolsView)
            {
                ShowToast("Creator Studio", Greeting, ToastSeverity.Error, 3200);
            }
        }
        finally
        {
            IsProfileModSearchLoading = false;
        }
    }

    [RelayCommand]
    private async Task LoadMoreModsForProfile()
    {
        if (IsLoadingMoreMods || !CanLoadMoreMods) return;

        var targetModpack = ResolveModManagementTargetModpack();
        if (targetModpack == null || !targetModpack.IsCustomProfile) return;

        IsLoadingMoreMods = true;
        try
        {
            var newResults = await SearchModsAcrossSourcesAsync(
                _profileModSearchLastQuery,
                string.IsNullOrWhiteSpace(_profileModSearchLastMcVersion) ? null : _profileModSearchLastMcVersion,
                string.IsNullOrWhiteSpace(_profileModSearchLastModLoader) ? null : _profileModSearchLastModLoader,
                _profileModSearchOffset);

            if (newResults.Count == 0)
            {
                CanLoadMoreMods = false;
                return;
            }

            // Append new results, skipping duplicates
            var existingIds = new HashSet<string>(ProfileModSearchResults.Select(m => $"{m.Source}:{m.Id}"), StringComparer.OrdinalIgnoreCase);
            var addedCount = 0;
            foreach (var mod in newResults)
            {
                var key = $"{mod.Source}:{mod.Id}";
                if (existingIds.Contains(key)) continue;
                mod.PropertyChanged += OnProfileSearchResultPropertyChanged;
                ProfileModSearchResults.Add(mod);
                addedCount++;
            }

            _profileModSearchOffset += newResults.Count;
            CanLoadMoreMods = newResults.Count >= 20;
            SyncProfileSearchResultsWithInstalledMods();

            Greeting = $"Celkem {ProfileModSearchResults.Count} modů v katalogu.";
            NotifyProfileModSearchStateChanged();
        }
        catch (Exception ex)
        {
            Greeting = $"Chyba načítání dalších modů: {ex.Message}";
        }
        finally
        {
            IsLoadingMoreMods = false;
        }
    }

    [RelayCommand]
    private async Task LoadModVersions(ModpackItem? mod)
    {
        var targetModpack = ResolveModManagementTargetModpack();
        if (mod == null || targetModpack == null || mod.IsInstalled)
        {
            return;
        }

        if (mod.IsLoadingVersionOptions)
        {
            return;
        }

        mod.IsLoadingVersionOptions = true;
        mod.VersionSelectionStatusLabel = "Nacitám kompatibilni verze...";

        try
        {
            var versionOptions = await LoadCompatibleVersionOptionsAsync(mod, targetModpack);
            mod.ReplaceVersionOptions(versionOptions);
            mod.VersionSelectionStatusLabel = versionOptions.Count == 0
                ? "Kompatibilni verze se nepodarilo dohledat. Pouzije se nejnovejsi dostupna."
                : $"{versionOptions.Count} kompatibilnich verzi pripraveno k vyberu.";
            NotifyProfileModSearchStateChanged();
        }
        catch (Exception ex)
        {
            LogService.Error($"[LoadModVersions] Failed for {mod.Name}", ex);
            mod.VersionSelectionStatusLabel = "Nacitani verzi selhalo.";
            if (IsStreamingToolsView)
            {
                ShowToast("Creator Studio", $"Verze pro {mod.Name} se nepodarilo nacist.", ToastSeverity.Warning, 3000);
            }
        }
        finally
        {
            mod.IsLoadingVersionOptions = false;
        }
    }

    [RelayCommand]
    public async Task AddModToProfile(ModpackItem mod)
    {
        var targetModpack = ResolveModManagementTargetModpack();
        if (mod == null || targetModpack == null || !targetModpack.IsCustomProfile)
        {
            return;
        }

        try
        {
            Greeting = $"Stahuji mod: {mod.Name}...";

            var installed = await InstallRemoteModIntoProfileAsync(mod, targetModpack);
            if (!installed)
            {
                Greeting = $"Nepodařilo se dohledat stažitelný soubor pro {mod.Name}.";
                if (IsStreamingToolsView)
                {
                    ShowToast("Creator Studio", Greeting, ToastSeverity.Warning, 3200);
                }

                return;
            }

            LoadInstalledMods();
            Greeting = $"Mod {mod.Name} nainstalován.";

            if (IsStreamingToolsView)
            {
                RefreshCreatorWorkspaceContext();
                TrackCreatorActivity($"Pridan mod {mod.Name} do workspace.");
                ShowToast("Creator Studio", $"Mod {mod.Name} byl přidán do workspace.", ToastSeverity.Success, 2400);
            }
        }
        catch (Exception ex)
        {
            Greeting = $"Chyba instalace modu: {ex.Message}";

            if (IsStreamingToolsView)
            {
                ShowToast("Creator Studio", Greeting, ToastSeverity.Error, 3200);
            }
        }
    }

    [RelayCommand]
    private async Task AddSelectedModsToProfile()
    {
        var targetModpack = ResolveModManagementTargetModpack();
        if (targetModpack == null || !targetModpack.IsCustomProfile)
        {
            return;
        }

        var selectedMods = ProfileModSearchResults
            .Where(mod => mod.IsSelected && !mod.IsInstalled)
            .ToList();

        if (selectedMods.Count == 0)
        {
            ShowToast("Creator Studio", "Nejdřív vyber výsledky, které chceš přidat.", ToastSeverity.Warning, 2400);
            return;
        }

        Greeting = $"Instaluji {selectedMods.Count} modů do workspace...";
        var installedCount = 0;
        var total = selectedMods.Count;
        foreach (var mod in selectedMods)
        {
            Greeting = $"Instaluji mod {installedCount + 1}/{total}: {mod.Name}...";
            if (await InstallRemoteModIntoProfileAsync(mod, targetModpack))
            {
                installedCount++;
            }

            mod.IsSelected = false;
        }

        LoadInstalledMods();
        Greeting = installedCount == 0
            ? "Vybrané mody se nepodařilo nainstalovat."
            : $"Nainstalováno {installedCount} modů do workspace.";

        if (installedCount > 0 && IsStreamingToolsView)
        {
            RefreshCreatorWorkspaceContext();
            TrackCreatorActivity($"Hromadne pridano {installedCount} modu do workspace.");
            ShowToast("Creator Studio", Greeting, ToastSeverity.Success, 2800);
        }
    }

    [RelayCommand]
    private async Task AddLocalModsToProfile()
    {
        var targetModpack = ResolveModManagementTargetModpack();
        if (targetModpack == null || !targetModpack.IsCustomProfile)
        {
            return;
        }

        var storageProvider = (Avalonia.Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)
            ?.MainWindow?.StorageProvider;
        if (storageProvider == null)
        {
            ShowToast("Creator Studio", "Souborový picker není v tomto režimu dostupný.", ToastSeverity.Warning);
            return;
        }

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Vyber lokální mody pro workspace",
            AllowMultiple = true,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Minecraft Mods")
                {
                    Patterns = new[] { "*.jar", "*.jar.disabled" },
                    MimeTypes = new[] { "application/java-archive" }
                }
            }
        });

        var selectedPaths = files
            .Select(file => file.TryGetLocalPath())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>()
            .ToList();

        if (selectedPaths.Count == 0)
        {
            return;
        }

        var addedCount = AddLocalModFilesToProfile(targetModpack, selectedPaths);
        LoadInstalledMods();

        if (addedCount > 0)
        {
            Greeting = $"Přidáno {addedCount} lokálních modů do workspace.";
            if (IsStreamingToolsView)
            {
                RefreshCreatorWorkspaceContext();
                TrackCreatorActivity($"Pridano {addedCount} lokalnich modu do workspace.");
                ShowToast("Creator Studio", Greeting, ToastSeverity.Success, 2800);
            }
        }
    }

    [RelayCommand]
    private void SelectAllProfileSearchMods()
    {
        foreach (var mod in ProfileModSearchResults.Where(mod => !mod.IsInstalled))
        {
            mod.IsSelected = true;
        }

        NotifyProfileModSearchStateChanged();
    }

    [RelayCommand]
    private void ClearProfileSearchModSelection()
    {
        foreach (var mod in ProfileModSearchResults)
        {
            mod.IsSelected = false;
        }

        NotifyProfileModSearchStateChanged();
    }

    [RelayCommand]
    public void RemoveModFromProfile(ModpackItem mod)
    {
        var targetModpack = ResolveModManagementTargetModpack();
        if (mod == null || targetModpack == null || !targetModpack.IsCustomProfile)
        {
            return;
        }

        try
        {
            var removedCount = RemoveInstalledModsFromProfile(targetModpack, new[] { mod });
            if (removedCount == 0)
            {
                return;
            }

            LoadInstalledMods();
            Greeting = $"Mod {mod.Name} odstraněn.";

            if (IsStreamingToolsView)
            {
                RefreshCreatorWorkspaceContext();
                TrackCreatorActivity($"Odstranen mod {mod.Name} z workspace.");
                ShowToast("Creator Studio", $"Mod {mod.Name} byl odebrán z workspace.", ToastSeverity.Success, 2400);
            }
        }
        catch (Exception ex)
        {
            Greeting = $"Chyba: {ex.Message}";

            if (IsStreamingToolsView)
            {
                ShowToast("Creator Studio", Greeting, ToastSeverity.Error, 3200);
            }
        }
    }

    [RelayCommand]
    private void RemoveSelectedModsFromProfile()
    {
        var targetModpack = ResolveModManagementTargetModpack();
        if (targetModpack == null || !targetModpack.IsCustomProfile)
        {
            return;
        }

        var selectedMods = InstalledMods.Where(mod => mod.IsSelected).ToList();
        if (selectedMods.Count == 0)
        {
            ShowToast("Creator Studio", "Nejdřív vyber mody, které chceš odebrat.", ToastSeverity.Warning, 2400);
            return;
        }

        try
        {
            var removedCount = RemoveInstalledModsFromProfile(targetModpack, selectedMods);
            LoadInstalledMods();
            Greeting = removedCount == 0
                ? "Vybrané mody se nepodařilo odstranit."
                : $"Odebráno {removedCount} modů z workspace.";

            if (removedCount > 0 && IsStreamingToolsView)
            {
                RefreshCreatorWorkspaceContext();
                TrackCreatorActivity($"Hromadne odebrano {removedCount} modu z workspace.");
                ShowToast("Creator Studio", Greeting, ToastSeverity.Success, 2800);
            }
        }
        catch (Exception ex)
        {
            Greeting = $"Chyba: {ex.Message}";
            if (IsStreamingToolsView)
            {
                ShowToast("Creator Studio", Greeting, ToastSeverity.Error, 3200);
            }
        }
    }

    [RelayCommand]
    private void ToggleInstalledModEnabled(ModpackItem mod)
    {
        var targetModpack = ResolveModManagementTargetModpack();
        if (mod == null || targetModpack == null || !targetModpack.IsCustomProfile)
        {
            return;
        }

        var changedCount = SetInstalledModsEnabled(targetModpack, new[] { mod }, !mod.IsEnabled);
        if (changedCount == 0)
        {
            return;
        }

        var nextStateLabel = !mod.IsEnabled ? "zapnut" : "vypnut";
        LoadInstalledMods();
        Greeting = $"Mod {mod.Name} byl {nextStateLabel}.";

        if (IsStreamingToolsView)
        {
            RefreshCreatorWorkspaceContext();
            TrackCreatorActivity($"Zmenen stav modu {mod.Name} v workspace.");
            ShowToast("Creator Studio", Greeting, ToastSeverity.Success, 2400);
        }
    }

    [RelayCommand]
    private void EnableSelectedInstalledMods()
    {
        BulkSetSelectedInstalledModsEnabled(true);
    }

    [RelayCommand]
    private void DisableSelectedInstalledMods()
    {
        BulkSetSelectedInstalledModsEnabled(false);
    }

    [RelayCommand]
    private void SelectAllInstalledMods()
    {
        foreach (var mod in FilteredInstalledMods)
        {
            mod.IsSelected = true;
        }

        NotifyInstalledModsStateChanged();
    }

    [RelayCommand]
    private void ClearInstalledModSelection()
    {
        foreach (var mod in InstalledMods)
        {
            mod.IsSelected = false;
        }

        NotifyInstalledModsStateChanged();
    }

    [RelayCommand]
    private void RefreshInstalledMods()
    {
        LoadInstalledMods();
    }

    private async Task<bool> InstallRemoteModIntoProfileAsync(ModpackItem mod, ModpackInfo targetModpack)
    {
        var modsDir = Path.Combine(_launcherService.GetModpackPath(targetModpack.Name), "mods");
        Directory.CreateDirectory(modsDir);

        mod.IsDownloading = true;
        mod.DownloadStatusLabel = "Hledám verzi...";

        var resolvedDownload = await ResolveRemoteModDownloadAsync(mod, targetModpack);
        if (resolvedDownload == null)
        {
            mod.IsDownloading = false;
            mod.DownloadStatusLabel = "";
            return false;
        }

        mod.DownloadStatusLabel = "Stahuji...";

        var normalizedFileName = NormalizeInstalledModFileName(resolvedDownload.FileName);
        var enabledPath = Path.Combine(modsDir, normalizedFileName);
        var disabledPath = enabledPath + ".disabled";
        if (File.Exists(disabledPath))
        {
            File.Delete(disabledPath);
        }

        var data = await _httpClient.GetByteArrayAsync(resolvedDownload.DownloadUrl);
        await File.WriteAllBytesAsync(enabledPath, data);

        mod.FileId = resolvedDownload.FileId;
        mod.VersionId = resolvedDownload.VersionId;
        mod.DownloadUrl = resolvedDownload.DownloadUrl;
        mod.IsInstalled = true;
        mod.IsEnabled = true;
        mod.IsSelected = false;
        mod.IsDownloading = false;
        mod.DownloadStatusLabel = "";
        mod.InstalledFileName = normalizedFileName;

        SaveModMetadata(modsDir, CreateInstalledModMetadata(mod, normalizedFileName, true));
        return true;
    }

    private async Task<ResolvedRemoteModDownload?> ResolveRemoteModDownloadAsync(ModpackItem mod, ModpackInfo targetModpack)
    {
        var minecraftVersion = ResolveModManagementTargetMinecraftVersion(targetModpack);
        var modLoader = ResolveModManagementTargetModLoader(targetModpack);

        if (mod.SelectedVersionOption != null)
        {
            if (string.Equals(mod.Source, "CurseForge", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(mod.Id, out var selectedCurseModId) &&
                int.TryParse(mod.SelectedVersionOption.FileId, out var selectedFileId))
            {
                var downloadUrl = mod.SelectedVersionOption.DownloadUrl;
                if (string.IsNullOrWhiteSpace(downloadUrl))
                {
                    downloadUrl = await _curseForgeApi.GetFileDownloadUrlAsync(selectedCurseModId, selectedFileId);
                }

                if (!string.IsNullOrWhiteSpace(downloadUrl))
                {
                    return new ResolvedRemoteModDownload(
                        string.IsNullOrWhiteSpace(mod.SelectedVersionOption.FileName) ? $"{mod.Name}.jar" : mod.SelectedVersionOption.FileName,
                        downloadUrl,
                        mod.SelectedVersionOption.FileId,
                        mod.SelectedVersionOption.VersionId);
                }
            }

            if (string.Equals(mod.Source, "Modrinth", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(mod.SelectedVersionOption.VersionId))
            {
                var downloadUrl = mod.SelectedVersionOption.DownloadUrl;
                var fileName = mod.SelectedVersionOption.FileName;

                if (string.IsNullOrWhiteSpace(downloadUrl) || string.IsNullOrWhiteSpace(fileName))
                {
                    var versionJson = await _modrinthApi.GetVersionAsync(mod.SelectedVersionOption.VersionId);
                    var versionNode = JsonNode.Parse(versionJson);
                    var primaryFile = versionNode?["files"]?.AsArray()
                        ?.FirstOrDefault(file => file?["primary"]?.GetValue<bool>() == true)
                        ?? versionNode?["files"]?.AsArray()?.FirstOrDefault();

                    downloadUrl = string.IsNullOrWhiteSpace(downloadUrl) ? primaryFile?["url"]?.ToString() : downloadUrl;
                    fileName = string.IsNullOrWhiteSpace(fileName) ? primaryFile?["filename"]?.ToString() : fileName;
                }

                if (!string.IsNullOrWhiteSpace(downloadUrl))
                {
                    return new ResolvedRemoteModDownload(
                        string.IsNullOrWhiteSpace(fileName) ? $"{mod.Name}.jar" : fileName,
                        downloadUrl,
                        mod.SelectedVersionOption.FileId,
                        mod.SelectedVersionOption.VersionId);
                }
            }
        }

        if (string.Equals(mod.Source, "CurseForge", StringComparison.OrdinalIgnoreCase) && int.TryParse(mod.Id, out var curseModId))
        {
            var filesJson = await _curseForgeApi.GetModFilesAsync(curseModId, minecraftVersion);
            var filesData = JsonNode.Parse(filesJson)?["data"]?.AsArray();
            var latestFile = filesData?
                .FirstOrDefault(file => IsCompatibleCurseForgeFile(file, minecraftVersion, modLoader))
                ?? filesData?.FirstOrDefault(file => IsCompatibleCurseForgeFile(file, minecraftVersion, null))
                ?? filesData?.FirstOrDefault();

            if (latestFile != null)
            {
                var fileId = latestFile["id"]?.ToString() ?? string.Empty;
                var downloadUrl = latestFile["downloadUrl"]?.ToString();
                if (string.IsNullOrWhiteSpace(downloadUrl) && int.TryParse(fileId, out var parsedFileId))
                {
                    downloadUrl = await _curseForgeApi.GetFileDownloadUrlAsync(curseModId, parsedFileId);
                }

                if (!string.IsNullOrWhiteSpace(downloadUrl))
                {
                    return new ResolvedRemoteModDownload(
                        latestFile["fileName"]?.ToString() ?? $"{mod.Name}.jar",
                        downloadUrl,
                        fileId,
                        fileId);
                }
            }
        }

        if (string.Equals(mod.Source, "Modrinth", StringComparison.OrdinalIgnoreCase))
        {
            var versionsJson = await _modrinthApi.GetProjectVersionsAsync(mod.Id);
            var versions = JsonNode.Parse(versionsJson)?.AsArray();
            var compatibleVersion = versions?
                .FirstOrDefault(version => IsCompatibleModrinthVersion(version, minecraftVersion, modLoader))
                ?? versions?.FirstOrDefault(version => IsCompatibleModrinthVersion(version, minecraftVersion, null))
                ?? versions?.FirstOrDefault();

            if (compatibleVersion != null)
            {
                var primaryFile = compatibleVersion["files"]?.AsArray()
                    ?.FirstOrDefault(file => file?["primary"]?.GetValue<bool>() == true)
                    ?? compatibleVersion["files"]?.AsArray()?.FirstOrDefault();

                var downloadUrl = primaryFile?["url"]?.ToString();
                if (!string.IsNullOrWhiteSpace(downloadUrl))
                {
                    return new ResolvedRemoteModDownload(
                        primaryFile?["filename"]?.ToString() ?? $"{mod.Name}.jar",
                        downloadUrl,
                        string.Empty,
                        compatibleVersion["id"]?.ToString() ?? string.Empty);
                }
            }
        }

        return null;
    }

    private int AddLocalModFilesToProfile(ModpackInfo targetModpack, IEnumerable<string> sourceFiles)
    {
        var modsDir = Path.Combine(_launcherService.GetModpackPath(targetModpack.Name), "mods");
        Directory.CreateDirectory(modsDir);

        var addedCount = 0;
        foreach (var sourceFile in sourceFiles)
        {
            if (!File.Exists(sourceFile))
            {
                continue;
            }

            var sourceFileName = Path.GetFileName(sourceFile);
            if (!IsManagedModFile(sourceFileName))
            {
                continue;
            }

            var normalizedFileName = NormalizeInstalledModFileName(sourceFileName);
            var isEnabled = !sourceFileName.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase);
            var destinationPath = Path.Combine(modsDir, isEnabled ? normalizedFileName : normalizedFileName + ".disabled");
            var counterpartPath = isEnabled ? destinationPath + ".disabled" : Path.Combine(modsDir, normalizedFileName);

            if (File.Exists(counterpartPath))
            {
                File.Delete(counterpartPath);
            }

            if (!string.Equals(sourceFile, destinationPath, StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(sourceFile, destinationPath, overwrite: true);
            }

            var tempMod = new ModpackItem
            {
                Name = FormatInstalledModDisplayName(normalizedFileName),
                Description = "Lokálně přidaný mod.",
                Source = "Manual",
                IsInstalled = true,
                IsEnabled = isEnabled,
                InstalledFileName = Path.GetFileName(destinationPath)
            };

            SaveModMetadata(modsDir, CreateInstalledModMetadata(tempMod, normalizedFileName, isEnabled));
            addedCount++;
        }

        return addedCount;
    }

    private int RemoveInstalledModsFromProfile(ModpackInfo targetModpack, IEnumerable<ModpackItem> mods)
    {
        var modsDir = Path.Combine(_launcherService.GetModpackPath(targetModpack.Name), "mods");
        var removedCount = 0;

        foreach (var mod in mods.Where(mod => !string.IsNullOrWhiteSpace(mod.InstalledFileName)).DistinctBy(mod => mod.InstalledFileName))
        {
            var installedPath = Path.Combine(modsDir, mod.InstalledFileName);
            if (!File.Exists(installedPath))
            {
                continue;
            }

            File.Delete(installedPath);

            var metadataPath = GetInstalledModMetadataPath(modsDir, mod.InstalledFileName);
            if (File.Exists(metadataPath))
            {
                File.Delete(metadataPath);
            }

            removedCount++;
        }

        return removedCount;
    }

    private void BulkSetSelectedInstalledModsEnabled(bool isEnabled)
    {
        var targetModpack = ResolveModManagementTargetModpack();
        if (targetModpack == null || !targetModpack.IsCustomProfile)
        {
            return;
        }

        var selectedMods = InstalledMods.Where(mod => mod.IsSelected).ToList();
        if (selectedMods.Count == 0)
        {
            ShowToast("Creator Studio", "Nejdřív vyber mody, kterým chceš změnit stav.", ToastSeverity.Warning, 2400);
            return;
        }

        var changedCount = SetInstalledModsEnabled(targetModpack, selectedMods, isEnabled);
        if (changedCount == 0)
        {
            return;
        }

        LoadInstalledMods();
        Greeting = isEnabled
            ? $"Zapnuto {changedCount} modů."
            : $"Vypnuto {changedCount} modů.";

        if (IsStreamingToolsView)
        {
            RefreshCreatorWorkspaceContext();
            TrackCreatorActivity($"Hromadne zmenen stav {changedCount} modu ve workspace.");
            ShowToast("Creator Studio", Greeting, ToastSeverity.Success, 2600);
        }
    }

    private int SetInstalledModsEnabled(ModpackInfo targetModpack, IEnumerable<ModpackItem> mods, bool isEnabled)
    {
        var modsDir = Path.Combine(_launcherService.GetModpackPath(targetModpack.Name), "mods");
        var changedCount = 0;

        foreach (var mod in mods.Where(mod => !string.IsNullOrWhiteSpace(mod.InstalledFileName)).DistinctBy(mod => mod.InstalledFileName))
        {
            var normalizedFileName = NormalizeInstalledModFileName(mod.InstalledFileName);
            var enabledPath = Path.Combine(modsDir, normalizedFileName);
            var disabledPath = enabledPath + ".disabled";
            var sourcePath = Path.Combine(modsDir, mod.InstalledFileName);
            var destinationPath = isEnabled ? enabledPath : disabledPath;

            if (!File.Exists(sourcePath) || string.Equals(sourcePath, destinationPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (File.Exists(destinationPath))
            {
                File.Delete(destinationPath);
            }

            File.Move(sourcePath, destinationPath);
            SaveModMetadata(modsDir, CreateInstalledModMetadata(mod, normalizedFileName, isEnabled));
            changedCount++;
        }

        return changedCount;
    }

    private void SaveModMetadata(string modsDir, ModMetadata metadata)
    {
        try
        {
            Directory.CreateDirectory(Path.Combine(modsDir, ".mod_metadata"));
            var metadataPath = GetInstalledModMetadataPath(modsDir, metadata.FileName);
            var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(metadataPath, json);
        }
        catch (Exception ex)
        {
            LogService.Error("[SaveModMetadata] Failed", ex);
        }
    }

    private void LoadInstalledMods()
    {
        var targetModpack = ResolveModManagementTargetModpack();
        if (targetModpack == null)
        {
            ReplaceInstalledMods(Array.Empty<ModpackItem>());
            return;
        }

        var modpackPath = _launcherService.GetModpackPath(targetModpack.Name);
        var modsDir = Path.Combine(modpackPath, "mods");
        if (!Directory.Exists(modsDir))
        {
            ReplaceInstalledMods(Array.Empty<ModpackItem>());
            return;
        }

        var metaDir = Path.Combine(modsDir, ".mod_metadata");
        var metadataIndex = LoadInstalledModMetadataIndex(modpackPath, metaDir);
        var installedMods = new List<ModpackItem>();

        foreach (var modFile in Directory.GetFiles(modsDir, "*", SearchOption.TopDirectoryOnly)
                     .Where(IsManagedModFile)
                     .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase))
        {
            var installedFileName = Path.GetFileName(modFile);
            var normalizedFileName = NormalizeInstalledModFileName(installedFileName);
            metadataIndex.TryGetValue(normalizedFileName, out var metadata);
            var isEnabled = !installedFileName.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase);

            installedMods.Add(new ModpackItem
            {
                Id = metadata?.ProjectId ?? string.Empty,
                Name = string.IsNullOrWhiteSpace(metadata?.Name) ? FormatInstalledModDisplayName(normalizedFileName) : metadata!.Name,
                Description = metadata?.Summary ?? string.Empty,
                Author = metadata?.Author ?? string.Empty,
                Source = string.IsNullOrWhiteSpace(metadata?.Source) ? "Manual" : metadata!.Source,
                IconUrl = metadata?.IconUrl ?? string.Empty,
                WebLink = metadata?.WebLink ?? string.Empty,
                Slug = metadata?.Slug ?? string.Empty,
                FileId = metadata?.FileId ?? string.Empty,
                VersionId = metadata?.VersionId ?? string.Empty,
                DownloadUrl = metadata?.DownloadUrl ?? string.Empty,
                IsInstalled = true,
                IsEnabled = metadata?.IsEnabled ?? isEnabled,
                InstalledFileName = installedFileName
            });
        }

        ReplaceInstalledMods(installedMods);
    }

    private void RefreshFilteredInstalledMods()
    {
        var normalizedQuery = InstalledModsSearchQuery?.Trim() ?? string.Empty;
        var filtered = string.IsNullOrWhiteSpace(normalizedQuery)
            ? InstalledMods.ToList()
            : InstalledMods.Where(mod => MatchesInstalledModSearch(mod, normalizedQuery)).ToList();

        FilteredInstalledMods.Clear();
        foreach (var mod in filtered)
        {
            FilteredInstalledMods.Add(mod);
        }

        NotifyInstalledModsStateChanged();
    }

    private static bool MatchesInstalledModSearch(ModpackItem mod, string query)
    {
        return ContainsSearchTerm(mod.Name, query) ||
               ContainsSearchTerm(mod.Description, query) ||
               ContainsSearchTerm(mod.Author, query) ||
               ContainsSearchTerm(mod.Source, query) ||
               ContainsSearchTerm(mod.InstalledFileName, query);
    }

    private static bool ContainsSearchTerm(string? value, string query)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private Dictionary<string, ModMetadata> LoadInstalledModMetadataIndex(string modpackPath, string metaDir)
    {
        var metadata = new Dictionary<string, ModMetadata>(StringComparer.OrdinalIgnoreCase);
        var metadataPath = Path.Combine(modpackPath, "mods_metadata.json");

        if (File.Exists(metadataPath))
        {
            try
            {
                var json = File.ReadAllText(metadataPath);
                var entries = JsonSerializer.Deserialize<List<ModMetadata>>(json);
                if (entries != null)
                {
                    foreach (var entry in entries.Where(entry => !string.IsNullOrWhiteSpace(entry.FileName)))
                    {
                        entry.FileName = NormalizeInstalledModFileName(entry.FileName);
                        if (string.IsNullOrWhiteSpace(entry.Source))
                        {
                            entry.Source = "Modpack";
                        }

                        metadata[entry.FileName] = entry;
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.Error("[LoadInstalledModMetadataIndex] Failed to read mods_metadata.json", ex);
            }
        }

        if (!Directory.Exists(metaDir))
        {
            return metadata;
        }

        foreach (var file in Directory.GetFiles(metaDir, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var entry = ReadInstalledModMetadata(file);
                if (entry == null || string.IsNullOrWhiteSpace(entry.FileName))
                {
                    continue;
                }

                entry.FileName = NormalizeInstalledModFileName(entry.FileName);
                metadata[entry.FileName] = entry;
            }
            catch (Exception ex)
            {
                LogService.Error($"[LoadInstalledModMetadataIndex] Failed to read {file}", ex);
            }
        }

        return metadata;
    }

    private static ModMetadata? ReadInstalledModMetadata(string filePath)
    {
        var json = File.ReadAllText(filePath);

        try
        {
            var metadata = JsonSerializer.Deserialize<ModMetadata>(json);
            if (metadata != null)
            {
                return metadata;
            }
        }
        catch
        {
        }

        var node = JsonNode.Parse(json);
        var fileName = node?["FileName"]?.ToString() ?? node?["fileName"]?.ToString();
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        return new ModMetadata
        {
            FileName = fileName,
            Name = node?["Name"]?.ToString() ?? node?["name"]?.ToString() ?? string.Empty,
            Slug = node?["Slug"]?.ToString() ?? node?["slug"]?.ToString() ?? string.Empty,
            ProjectId = node?["Id"]?.ToString() ?? node?["ProjectId"]?.ToString() ?? node?["projectId"]?.ToString() ?? string.Empty,
            FileId = node?["FileId"]?.ToString() ?? node?["fileId"]?.ToString() ?? string.Empty,
            VersionId = node?["VersionId"]?.ToString() ?? node?["versionId"]?.ToString() ?? string.Empty,
            Source = node?["Source"]?.ToString() ?? node?["source"]?.ToString() ?? string.Empty,
            Summary = node?["Description"]?.ToString() ?? node?["Summary"]?.ToString() ?? node?["summary"]?.ToString() ?? string.Empty,
            Author = node?["Author"]?.ToString() ?? node?["author"]?.ToString() ?? string.Empty,
            IconUrl = node?["IconUrl"]?.ToString() ?? node?["iconUrl"]?.ToString() ?? string.Empty,
            WebLink = node?["WebLink"]?.ToString() ?? node?["webLink"]?.ToString() ?? string.Empty,
            DownloadUrl = node?["DownloadUrl"]?.ToString() ?? node?["downloadUrl"]?.ToString() ?? string.Empty,
            IsEnabled = node?["IsEnabled"]?.GetValue<bool?>() ?? node?["isEnabled"]?.GetValue<bool?>() ?? true
        };
    }

    private static string FormatInstalledModDisplayName(string fileName)
    {
        return Path.GetFileNameWithoutExtension(NormalizeInstalledModFileName(fileName))
            .Replace('_', ' ')
            .Replace('-', ' ');
    }

    private async Task<IReadOnlyList<ModInstallVersionOption>> LoadCompatibleVersionOptionsAsync(ModpackItem mod, ModpackInfo targetModpack)
    {
        var minecraftVersion = ResolveModManagementTargetMinecraftVersion(targetModpack);
        var modLoader = ResolveModManagementTargetModLoader(targetModpack);

        if (string.Equals(mod.Source, "CurseForge", StringComparison.OrdinalIgnoreCase) && int.TryParse(mod.Id, out var curseModId))
        {
            var filesJson = await _curseForgeApi.GetModFilesAsync(curseModId, minecraftVersion);
            var files = JsonNode.Parse(filesJson)?["data"]?.AsArray();
            if (files == null)
            {
                return Array.Empty<ModInstallVersionOption>();
            }

            var compatibleFiles = files
                .Where(file => IsCompatibleCurseForgeFile(file, minecraftVersion, modLoader))
                .ToList();

            if (compatibleFiles.Count == 0)
            {
                compatibleFiles = files
                    .Where(file => IsCompatibleCurseForgeFile(file, minecraftVersion, null))
                    .ToList();
            }

            if (compatibleFiles.Count == 0)
            {
                compatibleFiles = files.ToList();
            }

            return compatibleFiles
                .Take(25)
                .Select(BuildCurseForgeVersionOption)
                .Where(option => !string.IsNullOrWhiteSpace(option.FileId))
                .ToList();
        }

        if (string.Equals(mod.Source, "Modrinth", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(mod.Id))
        {
            var versionsJson = await _modrinthApi.GetProjectVersionsAsync(mod.Id);
            var versions = JsonNode.Parse(versionsJson)?.AsArray();
            if (versions == null)
            {
                return Array.Empty<ModInstallVersionOption>();
            }

            var compatibleVersions = versions
                .Where(version => IsCompatibleModrinthVersion(version, minecraftVersion, modLoader))
                .ToList();

            if (compatibleVersions.Count == 0)
            {
                compatibleVersions = versions
                    .Where(version => IsCompatibleModrinthVersion(version, minecraftVersion, null))
                    .ToList();
            }

            if (compatibleVersions.Count == 0)
            {
                compatibleVersions = versions.ToList();
            }

            return compatibleVersions
                .Take(25)
                .Select(BuildModrinthVersionOption)
                .Where(option => !string.IsNullOrWhiteSpace(option.VersionId))
                .ToList();
        }

        return Array.Empty<ModInstallVersionOption>();
    }

    private static ModInstallVersionOption BuildCurseForgeVersionOption(JsonNode? fileNode)
    {
        var fileId = fileNode?["id"]?.ToString() ?? string.Empty;
        var fileName = fileNode?["fileName"]?.ToString() ?? string.Empty;
        var displayName = fileNode?["displayName"]?.ToString() ?? string.Empty;
        var downloadUrl = fileNode?["downloadUrl"]?.ToString() ?? string.Empty;
        var releaseTypeValue = fileNode?["releaseType"]?.ToString() ?? string.Empty;
        var releaseType = int.TryParse(releaseTypeValue, out var parsedReleaseType) ? parsedReleaseType : 0;
        var gameVersions = ExtractJsonStringValues(fileNode?["gameVersions"]);
        var summaryParts = new List<string>();
        var releaseLabel = releaseType switch
        {
            1 => "release",
            2 => "beta",
            3 => "alpha",
            _ => string.Empty
        };

        if (!string.IsNullOrWhiteSpace(releaseLabel))
        {
            summaryParts.Add(releaseLabel);
        }

        if (gameVersions.Count > 0)
        {
            summaryParts.Add(string.Join(", ", gameVersions.Take(3)));
        }

        return new ModInstallVersionOption
        {
            Label = !string.IsNullOrWhiteSpace(displayName) ? displayName : (!string.IsNullOrWhiteSpace(fileName) ? fileName : fileId),
            Summary = string.Join(" • ", summaryParts.Where(part => !string.IsNullOrWhiteSpace(part))),
            FileId = fileId,
            VersionId = fileId,
            DownloadUrl = downloadUrl,
            FileName = fileName
        };
    }

    private static ModInstallVersionOption BuildModrinthVersionOption(JsonNode? versionNode)
    {
        var versionId = versionNode?["id"]?.ToString() ?? string.Empty;
        var versionNumber = versionNode?["version_number"]?.ToString() ?? string.Empty;
        var versionName = versionNode?["name"]?.ToString() ?? string.Empty;
        var gameVersions = ExtractJsonStringValues(versionNode?["game_versions"]);
        var loaders = ExtractJsonStringValues(versionNode?["loaders"]);
        var primaryFile = versionNode?["files"]?.AsArray()
            ?.FirstOrDefault(file => file?["primary"]?.GetValue<bool>() == true)
            ?? versionNode?["files"]?.AsArray()?.FirstOrDefault();

        var summaryParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(versionName) && !string.Equals(versionName, versionNumber, StringComparison.OrdinalIgnoreCase))
        {
            summaryParts.Add(versionName);
        }

        if (gameVersions.Count > 0)
        {
            summaryParts.Add(string.Join(", ", gameVersions.Take(3)));
        }

        if (loaders.Count > 0)
        {
            summaryParts.Add(string.Join(", ", loaders.Take(2)));
        }

        return new ModInstallVersionOption
        {
            Label = !string.IsNullOrWhiteSpace(versionNumber) ? versionNumber : (!string.IsNullOrWhiteSpace(versionName) ? versionName : versionId),
            Summary = string.Join(" • ", summaryParts.Where(part => !string.IsNullOrWhiteSpace(part))),
            FileId = primaryFile?["hashes"]?["sha1"]?.ToString() ?? string.Empty,
            VersionId = versionId,
            DownloadUrl = primaryFile?["url"]?.ToString() ?? string.Empty,
            FileName = primaryFile?["filename"]?.ToString() ?? string.Empty
        };
    }

    private static List<string> ExtractJsonStringValues(JsonNode? node)
    {
        return node?.AsArray()
            ?.Select(value => value?.ToString() ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
            ?? new List<string>();
    }

    private static bool IsCompatibleCurseForgeFile(JsonNode? fileNode, string? minecraftVersion, string? modLoader)
    {
        var gameVersions = fileNode?["gameVersions"]?.AsArray()?.Select(version => version?.ToString() ?? string.Empty).ToList()
            ?? new List<string>();

        var matchesMinecraft = string.IsNullOrWhiteSpace(minecraftVersion) ||
                               gameVersions.Any(version => string.Equals(version, minecraftVersion, StringComparison.OrdinalIgnoreCase));
        var matchesLoader = string.IsNullOrWhiteSpace(modLoader) ||
                            gameVersions.Any(version => string.Equals(version, modLoader, StringComparison.OrdinalIgnoreCase));
        return matchesMinecraft && matchesLoader;
    }

    private static bool IsCompatibleModrinthVersion(JsonNode? versionNode, string? minecraftVersion, string? modLoader)
    {
        var gameVersions = versionNode?["game_versions"]?.AsArray()?.Select(version => version?.ToString() ?? string.Empty).ToList()
            ?? new List<string>();
        var loaders = versionNode?["loaders"]?.AsArray()?.Select(loader => loader?.ToString() ?? string.Empty).ToList()
            ?? new List<string>();

        var matchesMinecraft = string.IsNullOrWhiteSpace(minecraftVersion) ||
                               gameVersions.Any(version => string.Equals(version, minecraftVersion, StringComparison.OrdinalIgnoreCase));
        var matchesLoader = string.IsNullOrWhiteSpace(modLoader) ||
                            loaders.Any(loader => string.Equals(loader, modLoader, StringComparison.OrdinalIgnoreCase));
        return matchesMinecraft && matchesLoader;
    }

    private sealed record ResolvedRemoteModDownload(string FileName, string DownloadUrl, string FileId, string VersionId);
}
