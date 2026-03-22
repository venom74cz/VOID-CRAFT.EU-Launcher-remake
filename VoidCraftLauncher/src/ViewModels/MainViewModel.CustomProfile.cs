using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VoidCraftLauncher.Models;
using VoidCraftLauncher.Services;
using System;
using System.Collections.Generic;
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

    // ===== CUSTOM PROFILE STATE =====

    [ObservableProperty]
    private bool _isCreateProfileModalVisible = false;

    [ObservableProperty]
    private string _newProfileName = "";

    partial void OnNewProfileNameChanged(string value)
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

    public bool IsCreateProfileIdentityStep => CreateProfileStepIndex == 0;

    public bool IsCreateProfileRuntimeStep => CreateProfileStepIndex == 1;

    public bool IsCreateProfileReviewStep => CreateProfileStepIndex == 2;

    public bool CanGoBackCreateProfileStep => CreateProfileStepIndex > 0;

    public bool CanAdvanceCreateProfileStep =>
        IsCreateProfileIdentityStep
            ? ValidateCreateProfileName(out _)
            : IsCreateProfileRuntimeStep && ValidateCreateProfileRuntime(out _);

    public bool CanCreateCustomProfile =>
        ValidateCreateProfileName(out _) &&
        ValidateCreateProfileRuntime(out _);

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
        0 => "Vyber název a zkontroluj, kam se instance uloží.",
        1 => "Navol Minecraft verzi a kompatibilní mod loader.",
        _ => "Potvrď finální konfiguraci nové custom instance."
    };

    public string CreateProfileRuntimeSummary =>
        $"Minecraft {NewProfileMcVersion} • {CreateProfileSelectedLoaderLabel} {NewProfileModLoaderVersion}";

    public string CreateProfileRuntimeStatus
    {
        get
        {
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
                return ValidateCreateProfileName(out var identityError)
                    ? "Název se použije jako technická složka instance. Nepovolené znaky se při ukládání očistí."
                    : identityError;
            }

            if (IsCreateProfileRuntimeStep)
            {
                return ValidateCreateProfileRuntime(out var runtimeError)
                    ? CreateProfileRuntimeStatus
                    : runtimeError;
            }

            return CanCreateCustomProfile
                ? "Instance se založí do hlavního launcher workspace a hned se otevře v detailu."
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
        NewProfileName = "";
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
    public void CreateCustomProfile()
    {
        if (!ValidateCreateProfileName(out var identityError))
        {
            Greeting = identityError;
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

        Directory.CreateDirectory(instancePath);
        Directory.CreateDirectory(Path.Combine(instancePath, "mods"));

        var newProfile = new ModpackInfo
        {
            Name = sanitizedName,
            IsCustomProfile = true,
            CustomMcVersion = NewProfileMcVersion,
            CustomModLoader = NewProfileModLoader,
            CustomModLoaderVersion = NewProfileModLoaderVersion 
        };

        InstalledModpacks.Add(newProfile);
        SaveModpacks();

        Greeting = $"Vlastní profil '{sanitizedName}' vytvořen.";
        CloseCreateProfileModal();
        
        CurrentModpack = newProfile;
        GoToInstanceDetail(newProfile);
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

    private bool ValidateCreateProfileRuntime(out string error)
    {
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
        OnPropertyChanged(nameof(CreateProfileDirectoryName));
        OnPropertyChanged(nameof(CreateProfilePathPreview));
        OnPropertyChanged(nameof(CreateProfileSelectedLoaderLabel));
        OnPropertyChanged(nameof(CreateProfileStepTitle));
        OnPropertyChanged(nameof(CreateProfileStepSubtitle));
        OnPropertyChanged(nameof(CreateProfileRuntimeSummary));
        OnPropertyChanged(nameof(CreateProfileRuntimeStatus));
        OnPropertyChanged(nameof(CreateProfileWizardHint));
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
