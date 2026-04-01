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
    private string _profileModSearchQuery = "";

    // ===== CUSTOM PROFILE COMMANDS =====

    [RelayCommand]
    public void OpenCreateProfileModal()
    {
        InitializeCreateProfileBootstrapOptions();
        RebuildCreateProfileSnapshotOptions();
        NewProfileName = "";
        _isCreateProfileSlugAutoSync = true;
        NewProfileSlug = "";
        NewProfileSummary = "";
        NewProfileAuthors = "";
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
                    error = "Restore snapshot potřebuje existující snapshot z launcher backup workspace.";
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

    [RelayCommand]
    public async Task SearchModsForProfile()
    {
        if (CurrentModpack == null || !CurrentModpack.IsCustomProfile) return;
        if (string.IsNullOrWhiteSpace(ProfileModSearchQuery)) return;

        ProfileModSearchResults.Clear();
        Greeting = $"Hledám mody: {ProfileModSearchQuery}...";

        try
        {
            // Search CurseForge
            var cfJson = await _curseForgeApi.SearchModsAsync(
                ProfileModSearchQuery, 
                CurrentModpack.CustomMcVersion,
                CurrentModpack.CustomModLoader);
            
            var cfData = JsonNode.Parse(cfJson)?["data"]?.AsArray();
            if (cfData != null)
            {
                foreach (var mod in cfData)
                {
                    ProfileModSearchResults.Add(new ModpackItem
                    {
                        Id = mod?["id"]?.ToString() ?? "",
                        Name = mod?["name"]?.ToString() ?? "",
                        Description = mod?["summary"]?.ToString() ?? "",
                        IconUrl = mod?["logo"]?["url"]?.ToString() ?? "",
                        Author = mod?["authors"]?.AsArray()?.FirstOrDefault()?["name"]?.ToString() ?? "",
                        DownloadCount = mod?["downloadCount"]?.GetValue<long>() ?? 0,
                        Source = "CurseForge"
                    });
                }
            }

            // Search Modrinth
            var mrJson = await _modrinthApi.SearchModsAsync(
                ProfileModSearchQuery,
                CurrentModpack.CustomMcVersion,
                CurrentModpack.CustomModLoader);

            var mrData = JsonNode.Parse(mrJson)?["hits"]?.AsArray();
            if (mrData != null)
            {
                foreach (var mod in mrData)
                {
                    ProfileModSearchResults.Add(new ModpackItem
                    {
                        Id = mod?["project_id"]?.ToString() ?? "",
                        Name = mod?["title"]?.ToString() ?? "",
                        Description = mod?["description"]?.ToString() ?? "",
                        IconUrl = mod?["icon_url"]?.ToString() ?? "",
                        Author = mod?["author"]?.ToString() ?? "",
                        DownloadCount = mod?["downloads"]?.GetValue<long>() ?? 0,
                        Source = "Modrinth"
                    });
                }
            }

            // Check installed mods in the directory
            if (CurrentModpack != null && CurrentModpack.IsCustomProfile)
            {
                var modsDir = Path.Combine(_launcherService.GetModpackPath(CurrentModpack.Name), "mods");
                if (Directory.Exists(modsDir))
                {
                    var existingFiles = Directory.GetFiles(modsDir, "*.jar").Select(Path.GetFileName).ToList();
                    foreach (var mod in ProfileModSearchResults)
                    {
                        var safeModName = new string(mod.Name.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
                        var match = existingFiles.FirstOrDefault(f => 
                            new string(f.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant().Contains(safeModName));
                        
                        if (match != null)
                        {
                            mod.IsInstalled = true;
                            mod.InstalledFileName = match;
                        }
                    }
                }
            }

            Greeting = $"Nalezeno {ProfileModSearchResults.Count} modů.";
        }
        catch (Exception ex)
        {
            Greeting = $"Chyba hledání: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task AddModToProfile(ModpackItem mod)
    {
        if (mod == null || CurrentModpack == null || !CurrentModpack.IsCustomProfile) return;

        var modsDir = Path.Combine(_launcherService.GetModpackPath(CurrentModpack.Name), "mods");
        Directory.CreateDirectory(modsDir);

        try
        {
            Greeting = $"Stahuji mod: {mod.Name}...";

            if (mod.Source == "CurseForge")
            {
                var modId = int.Parse(mod.Id);
                var filesJson = await _curseForgeApi.GetModFilesAsync(modId);
                var filesData = JsonNode.Parse(filesJson)?["data"]?.AsArray();
                var latestFile = filesData?
                    .Where(f => f?["gameVersion"]?.AsArray()?.Any(v => v?.ToString() == CurrentModpack.CustomMcVersion) == true)
                    .FirstOrDefault() ?? filesData?.FirstOrDefault();

                if (latestFile != null)
                {
                    var downloadUrl = latestFile["downloadUrl"]?.ToString();
                    var fileName = latestFile["fileName"]?.ToString() ?? $"{mod.Name}.jar";

                    if (!string.IsNullOrEmpty(downloadUrl))
                    {
                        using var client = new System.Net.Http.HttpClient();
                        var data = await client.GetByteArrayAsync(downloadUrl);
                        await File.WriteAllBytesAsync(Path.Combine(modsDir, fileName), data);

                        mod.IsInstalled = true;
                        mod.InstalledFileName = fileName;
                        Greeting = $"Mod {mod.Name} nainstalován.";
                        
                        SaveModMetadata(modsDir, mod, fileName);
                    }
                }
            }
            else if (mod.Source == "Modrinth")
            {
                var versionsJson = await _modrinthApi.GetProjectVersionsAsync(mod.Id);
                var versions = JsonNode.Parse(versionsJson)?.AsArray();
                var compatibleVersion = versions?
                    .FirstOrDefault(v => v?["game_versions"]?.AsArray()?.Any(gv => gv?.ToString() == CurrentModpack.CustomMcVersion) == true)
                    ?? versions?.FirstOrDefault();

                if (compatibleVersion != null)
                {
                    var primaryFile = compatibleVersion["files"]?.AsArray()
                        ?.FirstOrDefault(f => f?["primary"]?.GetValue<bool>() == true)
                        ?? compatibleVersion["files"]?.AsArray()?.FirstOrDefault();

                    var downloadUrl = primaryFile?["url"]?.ToString();
                    var fileName = primaryFile?["filename"]?.ToString() ?? $"{mod.Name}.jar";

                    if (!string.IsNullOrEmpty(downloadUrl))
                    {
                        using var client = new System.Net.Http.HttpClient();
                        var data = await client.GetByteArrayAsync(downloadUrl);
                        await File.WriteAllBytesAsync(Path.Combine(modsDir, fileName), data);

                        mod.IsInstalled = true;
                        mod.InstalledFileName = fileName;
                        Greeting = $"Mod {mod.Name} nainstalován.";
                        
                        SaveModMetadata(modsDir, mod, fileName);
                    }
                }
            }

            LoadInstalledMods();
        }
        catch (Exception ex)
        {
            Greeting = $"Chyba instalace modu: {ex.Message}";
        }
    }

    [RelayCommand]
    public void RemoveModFromProfile(ModpackItem mod)
    {
        if (mod == null || CurrentModpack == null) return;

        var modsDir = Path.Combine(_launcherService.GetModpackPath(CurrentModpack.Name), "mods");
        if (!string.IsNullOrEmpty(mod.InstalledFileName))
        {
            var filePath = Path.Combine(modsDir, mod.InstalledFileName);
            if (File.Exists(filePath))
            {
                try
                {
                    File.Delete(filePath);
                    mod.IsInstalled = false;
                    mod.InstalledFileName = null;
                    Greeting = $"Mod {mod.Name} odstraněn.";

                    // Remove metadata
                    var metaPath = Path.Combine(modsDir, ".mod_metadata", mod.InstalledFileName + ".json");
                    if (File.Exists(metaPath)) File.Delete(metaPath);

                    LoadInstalledMods();
                }
                catch (Exception ex)
                {
                    Greeting = $"Chyba: {ex.Message}";
                }
            }
        }
    }

    [RelayCommand]
    private void RefreshInstalledMods()
    {
        LoadInstalledMods();
    }

    private void SaveModMetadata(string modsDir, ModpackItem mod, string fileName)
    {
        try
        {
            var metaDir = Path.Combine(modsDir, ".mod_metadata");
            Directory.CreateDirectory(metaDir);

            var meta = new
            {
                mod.Id,
                mod.Name,
                mod.Source,
                mod.Author,
                mod.IconUrl,
                FileName = fileName,
                InstalledAt = DateTime.UtcNow
            };

            var json = System.Text.Json.JsonSerializer.Serialize(meta, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(metaDir, fileName + ".json"), json);
        }
        catch (Exception ex)
        {
            LogService.Error("[SaveModMetadata] Failed", ex);
        }
    }

    private void LoadInstalledMods()
    {
        InstalledMods.Clear();

        if (CurrentModpack == null) return;

        var modpackPath = _launcherService.GetModpackPath(CurrentModpack.Name);
        var modsDir = Path.Combine(modpackPath, "mods");
        if (!Directory.Exists(modsDir)) return;

        var metaDir = Path.Combine(modsDir, ".mod_metadata");
        var metadataIndex = LoadInstalledModMetadataIndex(modpackPath, metaDir);
        var jarFiles = Directory.GetFiles(modsDir, "*.jar");

        foreach (var jar in jarFiles)
        {
            var fileName = Path.GetFileName(jar);
            var metaPath = Path.Combine(metaDir, fileName + ".json");

            if (metadataIndex.TryGetValue(fileName, out var metadata))
            {
                InstalledMods.Add(new ModpackItem
                {
                    Id = metadata.Id,
                    Name = string.IsNullOrWhiteSpace(metadata.Name) ? FormatInstalledModDisplayName(fileName) : metadata.Name,
                    Description = metadata.Summary ?? string.Empty,
                    Author = metadata.Author ?? string.Empty,
                    Source = metadata.Source ?? string.Empty,
                    IconUrl = metadata.IconUrl ?? string.Empty,
                    WebLink = metadata.WebLink ?? string.Empty,
                    IsInstalled = true,
                    InstalledFileName = fileName
                });
                continue;
            }

            if (File.Exists(metaPath))
            {
                try
                {
                    var json = File.ReadAllText(metaPath);
                    var meta = JsonNode.Parse(json);
                    InstalledMods.Add(new ModpackItem
                    {
                        Id = meta?["Id"]?.ToString() ?? "",
                        Name = meta?["Name"]?.ToString() ?? FormatInstalledModDisplayName(fileName),
                        Description = meta?["Description"]?.ToString() ?? "",
                        Source = meta?["Source"]?.ToString() ?? "",
                        Author = meta?["Author"]?.ToString() ?? "",
                        WebLink = meta?["WebLink"]?.ToString() ?? "",
                        IconUrl = meta?["IconUrl"]?.ToString() ?? "",
                        IsInstalled = true,
                        InstalledFileName = fileName
                    });
                }
                catch
                {
                    InstalledMods.Add(new ModpackItem { Name = FormatInstalledModDisplayName(fileName), IsInstalled = true, InstalledFileName = fileName });
                }
            }
            else
            {
                InstalledMods.Add(new ModpackItem { Name = FormatInstalledModDisplayName(fileName), IsInstalled = true, InstalledFileName = fileName });
            }
        }
    }

    private Dictionary<string, ModMetadataEnvelope> LoadInstalledModMetadataIndex(string modpackPath, string metaDir)
    {
        var metadata = new Dictionary<string, ModMetadataEnvelope>(StringComparer.OrdinalIgnoreCase);
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
                        metadata[entry.FileName] = new ModMetadataEnvelope
                        {
                            Id = entry.Slug ?? string.Empty,
                            Name = entry.Name ?? string.Empty,
                            Summary = entry.Summary ?? string.Empty,
                            IconUrl = entry.IconUrl ?? string.Empty,
                            WebLink = entry.WebLink ?? string.Empty,
                            Source = "Modpack",
                            Author = string.Empty
                        };
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
                var json = File.ReadAllText(file);
                var node = JsonNode.Parse(json);
                var fileName = node?["FileName"]?.ToString();
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    continue;
                }

                metadata[fileName] = new ModMetadataEnvelope
                {
                    Id = node?["Id"]?.ToString() ?? string.Empty,
                    Name = node?["Name"]?.ToString() ?? string.Empty,
                    Summary = node?["Description"]?.ToString() ?? string.Empty,
                    IconUrl = node?["IconUrl"]?.ToString() ?? string.Empty,
                    WebLink = node?["WebLink"]?.ToString() ?? string.Empty,
                    Source = node?["Source"]?.ToString() ?? string.Empty,
                    Author = node?["Author"]?.ToString() ?? string.Empty
                };
            }
            catch (Exception ex)
            {
                LogService.Error($"[LoadInstalledModMetadataIndex] Failed to read {file}", ex);
            }
        }

        return metadata;
    }

    private static string FormatInstalledModDisplayName(string fileName)
    {
        return Path.GetFileNameWithoutExtension(fileName)
            .Replace('_', ' ')
            .Replace('-', ' ');
    }

    private sealed class ModMetadataEnvelope
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Summary { get; set; } = string.Empty;

        public string IconUrl { get; set; } = string.Empty;

        public string WebLink { get; set; } = string.Empty;

        public string Source { get; set; } = string.Empty;

        public string Author { get; set; } = string.Empty;
    }
}
