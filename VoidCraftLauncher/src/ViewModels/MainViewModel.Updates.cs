using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VoidCraftLauncher.Models;
using VoidCraftLauncher.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace VoidCraftLauncher.ViewModels;

/// <summary>
/// Update checks, modpack data loading, changelog, server status polling.
/// </summary>
public partial class MainViewModel
{
    // ===== SERVER STATUS STATE =====

    [ObservableProperty]
    private string _serverMotd = "Načítání...";

    // ===== UPDATE CHECK =====

    [RelayCommand]
    public async Task CheckForUpdates()
    {
        try
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => Greeting = "Kontroluji aktualizace...");
            LogService.Log("Checking for updates via GitHub...");
            var currentVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("VOID-CRAFT-Launcher");
            
            var response = await _httpClient.GetStringAsync("https://api.github.com/repos/venom74cz/VOID-CRAFT.EU-Launcher-remake/releases/latest");
            var json = JsonNode.Parse(response);
            
            var tagName = json?["tag_name"]?.ToString();
            var cleanVersion = tagName?.TrimStart('v');
            
            if (cleanVersion?.Contains('-') == true)
                cleanVersion = cleanVersion.Split('-')[0];

            var assets = json?["assets"]?.AsArray();
            var downloadUrl = assets?.FirstOrDefault(a => a?["name"]?.ToString().EndsWith("Setup.exe") == true)?["browser_download_url"]?.ToString();
            
            if (Version.TryParse(cleanVersion, out var latestVersion) && !string.IsNullOrEmpty(downloadUrl))
            {
                if (latestVersion > currentVersion)
                {
                    LogService.Log($"New version found: {latestVersion} (Current: {currentVersion})");
                    
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => 
                    {
                        Greeting = $"Stahuji aktualizaci {tagName}...";
                    });

                    await PerformUpdate(downloadUrl);
                }
                else
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => Greeting = $"Máš nejnovější verzi ({currentVersion})");
                    await Task.Delay(3000);
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => Greeting = $"Vítejte, {UserSession?.Username ?? "Hráči"}!");
                }
            }
        }
        catch (Exception ex)
        {
            LogService.Error("Update check failed", ex);
            Avalonia.Threading.Dispatcher.UIThread.Post(() => Greeting = "Chyba kontroly aktualizací.");
        }
    }

    private async Task PerformUpdate(string url)
    {
        try
        {
            var tempPath = Path.Combine(Path.GetTempPath(), "VoidCraftLauncher_Setup.exe");
            
            var data = await _httpClient.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(tempPath, data);

            Avalonia.Threading.Dispatcher.UIThread.Post(() => Greeting = "Spouštím instalátor...");

            LogService.Log("Update downloaded. running installer...");
            
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo 
            {
                FileName = tempPath,
                Arguments = "/SILENT /SP-",
                UseShellExecute = true
            });

            Environment.Exit(0);
        }
        catch (Exception ex)
        {
             LogService.Error("Update failed", ex);
               Avalonia.Threading.Dispatcher.UIThread.Post(() => Greeting = "Chyba aktualizace: " + ex.Message);
        }
    }

    // ===== SERVER STATUS =====

    private async Task UpdateServerStatus()
    {
        try
        {
            var response = await _httpClient.GetStringAsync("https://api.mcsrvstat.us/2/mc.void-craft.eu");
            var json = JsonNode.Parse(response);
            
            if (json != null && json["online"]?.GetValue<bool>() == true)
            {
                var players = json["players"];
                var online = players?["online"]?.GetValue<int>() ?? 0;
                var max = players?["max"]?.GetValue<int>() ?? 0;
                
                var motdList = json["motd"]?["clean"]?.AsArray();
                var motd = "Void Craft";
                if (motdList != null && motdList.Count > 0)
                {
                    motd = string.Join(" ", motdList.Select(m => m?.ToString()));
                }

                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    IsServerOnline = true;
                    ServerPlayerCount = online;
                    ServerMaxPlayers = max;
                    ServerStatusText = $"{online}/{max} Hráčů";
                    ServerMotd = (string.IsNullOrWhiteSpace(motd) ? "Void-Craft.eu" : motd).Trim();
                });
            }
            else
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    IsServerOnline = false;
                    ServerStatusText = "Offline";
                    ServerPlayerCount = 0;
                    ServerMotd = "Server nedostupný";
                });
            }
        }
        catch (Exception ex)
        {
            LogService.Error("Error fetching server status", ex);
            Avalonia.Threading.Dispatcher.UIThread.Post(() => ServerStatusText = "Chyba načítání");
        }
    }

    // ===== MODPACK DATA LOADING =====

    private async Task LoadModpackData()
    {
        try
        {
            const int VOID_BOX_PROJECT_ID = 1402056;
            
            LogService.Log($"[LoadModpackData] Fetching modpack ID: {VOID_BOX_PROJECT_ID}");
            
            var modpackJson = await _curseForgeApi.GetModpackInfoAsync(VOID_BOX_PROJECT_ID);
            
            var root = JsonNode.Parse(modpackJson);
            var modpack = root?["data"];
            var name = modpack?["name"]?.ToString();
            var logo = modpack?["logo"]?["url"]?.ToString();
            var summary = modpack?["summary"]?.ToString();
            var id = modpack?["id"]?.GetValue<int>();
            
            LogService.Log($"[LoadModpackData] Parsed - Name: {name}, ID: {id}");

            var versionsList = new ObservableCollection<ModpackVersion>();
            ModpackVersion selectedVersion = new ModpackVersion { Name = "Unknown" };

            if (id.HasValue)
            {
                try 
                {
                    LogService.Log($"[LoadModpackData] Fetching files for ID: {id.Value}");
                    var filesJson = await _curseForgeApi.GetModpackFilesAsync(id.Value);
                    
                    var filesNode = JsonNode.Parse(filesJson);
                    var files = filesNode?["data"]?.AsArray();
                    
                    LogService.Log($"[LoadModpackData] Files count: {files?.Count ?? 0}");
                    
                    if (files != null && files.Count > 0)
                    {
                        var sortedFiles = files.OrderByDescending(f => f?["fileDate"]?.ToString());
                        
                        foreach(var f in sortedFiles)
                        {
                            var v = new ModpackVersion 
                            { 
                                Name = f?["displayName"]?.ToString() ?? "Unknown",
                                FileId = f?["id"]?.ToString() ?? "0",
                                ReleaseDate = f?["fileDate"]?.ToString() ?? ""
                            };
                            versionsList.Add(v);
                        }

                        selectedVersion = versionsList.FirstOrDefault() ?? selectedVersion;
                        LogService.Log($"[LoadModpackData] Selected version: {selectedVersion.Name}, FileId: {selectedVersion.FileId}");
                    }
                }
                catch (Exception ex) 
                { 
                    LogService.Error("[LoadModpackData] Version fetch error", ex);
                }
            }

            CurrentModpack = new ModpackInfo
            {
                ProjectId = id ?? VOID_BOX_PROJECT_ID,
                Source = "CurseForge",
                Name = name ?? "VOID-BOX 2",
                DisplayName = name ?? "VOID-BOX 2",
                LogoUrl = logo ?? "",
                Description = summary ?? "",
                Author = modpack?["authors"]?[0]?["name"]?.ToString() ?? modpack?["author"]?.ToString() ?? "",
                WebLink = modpack?["links"]?["websiteUrl"]?.ToString() ?? "",
                CurrentVersion = selectedVersion,
                Versions = versionsList,
                IsDeletable = false
            };

            if (!InstalledModpacks.Any(m => m.ProjectId == CurrentModpack.ProjectId))
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => InstalledModpacks.Add(CurrentModpack));
            }
            
            await Task.Run(LoadSavedModpacks);
        }
        catch (Exception ex)
        {
            LogService.Error("[LoadModpackData] ERROR", ex);
            Greeting = $"API Error: {ex.Message}";
            CurrentModpack = new ModpackInfo { Name = $"Chyba: {ex.Message.Substring(0, Math.Min(50, ex.Message.Length))}", CurrentVersion = new ModpackVersion { Name = "-" } };
        }
    }

    // ===== MODPACK UPDATE LOOP =====

    private async Task ModpackUpdateLoop()
    {
        while (true)
        {
            try
            {
                await CheckInstalledModpacksForUpdates();
            }
            catch (Exception ex)
            {
                LogService.Error("[ModpackUpdateLoop] Update check failed", ex);
            }

            await Task.Delay(ModpackUpdateCheckInterval);
        }
    }

    private async Task CheckInstalledModpacksForUpdates()
    {
        if (!await _modpackUpdateCheckLock.WaitAsync(0))
        {
            return;
        }

        try
        {
            List<ModpackInfo> modpacks = new();
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                modpacks = InstalledModpacks
                    .Where(m => m != null && m.ProjectId > 0)
                    .ToList();
            });

            foreach (var modpack in modpacks)
            {
                try
                {
                    var filesJson = await _curseForgeApi.GetModpackFilesAsync(modpack.ProjectId);
                    var filesNode = JsonNode.Parse(filesJson);
                    var files = filesNode?["data"]?.AsArray();
                    if (files == null || files.Count == 0)
                    {
                        continue;
                    }

                    var sortedFiles = files.OrderByDescending(f => f?["fileDate"]?.ToString());
                    var latestVersions = new ObservableCollection<ModpackVersion>();

                    foreach (var file in sortedFiles)
                    {
                        latestVersions.Add(new ModpackVersion
                        {
                            Name = file?["displayName"]?.ToString() ?? "Unknown",
                            FileId = file?["id"]?.ToString() ?? "0",
                            ReleaseDate = file?["fileDate"]?.ToString() ?? ""
                        });
                    }

                    var currentVersion = modpack.CurrentVersion;
                    var installedManifest = ModpackInstaller.LoadManifestInfo(_launcherService.GetModpackPath(modpack.Name));

                    if (installedManifest?.FileId > 0)
                    {
                        var installedFileId = installedManifest.FileId.ToString();
                        currentVersion = latestVersions.FirstOrDefault(v => v.FileId == installedFileId)
                            ?? new ModpackVersion
                            {
                                Name = modpack.CurrentVersion?.Name ?? $"File {installedFileId}",
                                FileId = installedFileId,
                                ReleaseDate = modpack.CurrentVersion?.ReleaseDate ?? ""
                            };
                    }
                    else if (currentVersion == null)
                    {
                        currentVersion = new ModpackVersion { Name = "-", FileId = "0", ReleaseDate = "" };
                    }

                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        modpack.Versions = latestVersions;
                        modpack.CurrentVersion = currentVersion;
                    });
                }
                catch (Exception ex)
                {
                    LogService.Error($"[CheckInstalledModpacksForUpdates] Failed for {modpack.Name}", ex);
                }
            }
        }
        finally
        {
            _modpackUpdateCheckLock.Release();
        }
    }

    // ===== CHANGELOG =====

    private async Task LoadChangelogAsync()
    {
        const string changelogUrl = "https://raw.githubusercontent.com/venom74cz/VOID-CRAFT.EU-Launcher-remake/main/CHANGELOG.md";

        try
        {
            var response = await _httpClient.GetAsync(changelogUrl);
            if (!response.IsSuccessStatusCode)
            {
                LogService.Error($"[LoadChangelog] GitHub returned {response.StatusCode}");
                return;
            }

            var content = await response.Content.ReadAsStringAsync();
            var lines = content.Split('\n');
            var entries = new List<ChangelogEntry>();
            ChangelogEntry? current = null;

            foreach (var line in lines)
            {
                var versionMatch = Regex.Match(line, @"^##\s+\[?(\d+\.\d+\.\d+)\]?\s*(?:-\s*(.+))?$");
                if (versionMatch.Success)
                {
                    current = new ChangelogEntry
                    {
                        Version = versionMatch.Groups[1].Value,
                        Date = versionMatch.Groups[2].Success ? versionMatch.Groups[2].Value.Trim() : ""
                    };
                    entries.Add(current);
                    continue;
                }

                if (current == null) continue;

                var sectionMatch = Regex.Match(line, @"^###\s+(.+)$");
                if (sectionMatch.Success)
                {
                    if (string.IsNullOrEmpty(current.Title))
                        current.Title = sectionMatch.Groups[1].Value.Trim();
                    continue;
                }

                var itemMatch = Regex.Match(line, @"^-\s+(.+)$");
                if (itemMatch.Success)
                {
                    var text = Regex.Replace(itemMatch.Groups[1].Value, @"\*\*([^*]+)\*\*", "$1");
                    current.Items.Add(text);
                }
            }

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                ChangelogEntries = new ObservableCollection<ChangelogEntry>(entries.Take(5));
            });
        }
        catch (Exception ex)
        {
            LogService.Error("[LoadChangelog] Failed to fetch changelog from GitHub", ex);
        }
    }

    // ===== MODPACK PERSISTENCE =====

    private void SaveModpacks()
    {
        try
        {
            var path = Path.Combine(_launcherService.BasePath, "installed_modpacks.json");
            var options = new JsonSerializerOptions { WriteIndented = true };
            var listToSave = InstalledModpacks.ToList();
            var json = JsonSerializer.Serialize(listToSave, options);
            File.WriteAllText(path, json);
            Debug.WriteLine($"[SaveModpacks] Saved {listToSave.Count} modpacks to {path}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SaveModpacks] Failed to save: {ex.Message}");
        }
    }

    private void LoadSavedModpacks()
    {
        try
        {
            var path = Path.Combine(_launcherService.BasePath, "installed_modpacks.json");
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var list = JsonSerializer.Deserialize<List<ModpackInfo>>(json);
                
                if (list != null)
                {
                    var hydratedAny = false;
                    foreach (var modpack in list)
                    {
                        hydratedAny |= HydrateModpackFromInstalledManifest(modpack);
                    }

                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        foreach (var modpack in list)
                        {
                            if (!InstalledModpacks.Any(m => m.Name == modpack.Name))
                            {
                                InstalledModpacks.Add(modpack);
                            }
                        }

                        if (hydratedAny)
                        {
                            SaveModpacks();
                        }
                    });
                    Debug.WriteLine($"[LoadSavedModpacks] Loaded {list.Count} modpacks.");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LoadSavedModpacks] Failed to load: {ex.Message}");
        }
    }

    // ===== DESCRIPTION FETCHING =====

    private async Task FetchFullDescriptionAsync()
    {
        if (CurrentModpack == null) return;

        try
        {
            HydrateCurrentModpackIdentity(CurrentModpack);

            string? fetchedName = null;
            string? fetchedSummary = null;
            string? fetchedAuthor = null;
            string? fetchedLogoUrl = null;
            string? fetchedWebLink = null;
            string fullDescription = "";

            if ((string.IsNullOrWhiteSpace(CurrentModpack.Source) || CurrentModpack.Source == "CurseForge") && CurrentModpack.ProjectId > 0)
            {
                var modpackJson = await _curseForgeApi.GetModpackInfoAsync(CurrentModpack.ProjectId);
                var modpackNode = JsonNode.Parse(modpackJson)?["data"];
                fetchedName = modpackNode?["name"]?.ToString();
                fetchedSummary = modpackNode?["summary"]?.ToString();
                fetchedAuthor = modpackNode?["authors"]?[0]?["name"]?.ToString() ?? modpackNode?["author"]?.ToString();
                fetchedLogoUrl = modpackNode?["logo"]?["url"]?.ToString() ?? modpackNode?["logo"]?["thumbnailUrl"]?.ToString();
                fetchedWebLink = modpackNode?["links"]?["websiteUrl"]?.ToString();
                fullDescription = await _curseForgeApi.GetProjectDescriptionAsync(CurrentModpack.ProjectId);
            }
            else if (CurrentModpack.Source == "Modrinth" && !string.IsNullOrEmpty(CurrentModpack.ModrinthId))
            {
                var projectJson = await _modrinthApi.GetProjectAsync(CurrentModpack.ModrinthId);
                var projectNode = JsonNode.Parse(projectJson);
                fetchedName = projectNode?["title"]?.ToString();
                fetchedSummary = projectNode?["description"]?.ToString();
                fetchedAuthor = projectNode?["author"]?.ToString();
                fetchedLogoUrl = projectNode?["icon_url"]?.ToString();
                fetchedWebLink = projectNode?["slug"] is JsonNode slugNode
                    ? $"https://modrinth.com/modpack/{slugNode}"
                    : CurrentModpack.WebLink;
                fullDescription = projectNode?["body"]?.ToString() ?? "";
            }

            var descriptionToApply = !string.IsNullOrWhiteSpace(fullDescription)
                ? fullDescription
                : fetchedSummary ?? "";

            var changed = await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                var metadataChanged = false;

                if (!string.IsNullOrWhiteSpace(fetchedName) &&
                    (string.IsNullOrWhiteSpace(CurrentModpack.DisplayName) ||
                     string.Equals(CurrentModpack.DisplayName, CurrentModpack.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    CurrentModpack.DisplayName = fetchedName;
                    metadataChanged = true;
                }

                if (ShouldReplaceCurrentModpackName(CurrentModpack.Name) && !string.IsNullOrWhiteSpace(fetchedName))
                {
                    CurrentModpack.Name = fetchedName;
                    metadataChanged = true;
                }

                if (!string.IsNullOrWhiteSpace(descriptionToApply) && !string.Equals(CurrentModpack.Description, descriptionToApply, StringComparison.Ordinal))
                {
                    CurrentModpack.Description = descriptionToApply;
                    metadataChanged = true;
                }

                if (string.IsNullOrWhiteSpace(CurrentModpack.Author) && !string.IsNullOrWhiteSpace(fetchedAuthor))
                {
                    CurrentModpack.Author = fetchedAuthor;
                    metadataChanged = true;
                }

                if (string.IsNullOrWhiteSpace(CurrentModpack.LogoUrl) && !string.IsNullOrWhiteSpace(fetchedLogoUrl))
                {
                    CurrentModpack.LogoUrl = fetchedLogoUrl;
                    metadataChanged = true;
                }

                if (string.IsNullOrWhiteSpace(CurrentModpack.WebLink) && !string.IsNullOrWhiteSpace(fetchedWebLink))
                {
                    CurrentModpack.WebLink = fetchedWebLink;
                    metadataChanged = true;
                }

                if (string.IsNullOrWhiteSpace(CurrentModpack.Source))
                {
                    CurrentModpack.Source = CurrentModpack.ProjectId > 0 ? "CurseForge" : "Modrinth";
                    metadataChanged = true;
                }

                return metadataChanged;
            });

            if (changed)
            {
                SaveModpacks();
            }
        }
        catch (Exception ex)
        {
            LogService.Error($"[FetchFullDescriptionAsync] Failed for {CurrentModpack.Name}", ex);
        }
    }

    private void HydrateCurrentModpackIdentity(ModpackInfo modpack)
    {
        HydrateModpackFromInstalledManifest(modpack);

        var existing = InstalledModpacks.FirstOrDefault(candidate =>
            !ReferenceEquals(candidate, modpack) &&
            ((modpack.ProjectId > 0 && candidate.ProjectId == modpack.ProjectId) ||
             (!string.IsNullOrWhiteSpace(modpack.ModrinthId) && string.Equals(candidate.ModrinthId, modpack.ModrinthId, StringComparison.OrdinalIgnoreCase)) ||
             ArePackNamesEquivalent(candidate.Name, modpack.Name)));

        if (existing == null)
        {
            return;
        }

        if (modpack.ProjectId <= 0)
        {
            modpack.ProjectId = existing.ProjectId;
        }

        if (string.IsNullOrWhiteSpace(modpack.Source))
        {
            modpack.Source = existing.Source;
        }

        if (string.IsNullOrWhiteSpace(modpack.ModrinthId))
        {
            modpack.ModrinthId = existing.ModrinthId;
        }

        if (string.IsNullOrWhiteSpace(modpack.WebLink))
        {
            modpack.WebLink = existing.WebLink;
        }

        if (string.IsNullOrWhiteSpace(modpack.DisplayName))
        {
            modpack.DisplayName = existing.DisplayName;
        }

        if (ShouldReplaceCurrentModpackName(modpack.Name) && !string.IsNullOrWhiteSpace(existing.Name))
        {
            modpack.Name = existing.Name;
        }

        if (string.IsNullOrWhiteSpace(modpack.LogoUrl))
        {
            modpack.LogoUrl = existing.LogoUrl;
        }

        if (string.IsNullOrWhiteSpace(modpack.Author))
        {
            modpack.Author = existing.Author;
        }

        if (string.IsNullOrWhiteSpace(modpack.Description))
        {
            modpack.Description = existing.Description;
        }
    }

    private bool HydrateModpackFromInstalledManifest(ModpackInfo modpack)
    {
        if (modpack == null || string.IsNullOrWhiteSpace(modpack.Name) || ShouldReplaceCurrentModpackName(modpack.Name))
        {
            return false;
        }

        var manifest = ModpackInstaller.LoadManifestInfo(_launcherService.GetModpackPath(modpack.Name));
        if (manifest == null)
        {
            return false;
        }

        var metadataChanged = false;

        if (string.IsNullOrWhiteSpace(modpack.DisplayName) && !string.IsNullOrWhiteSpace(manifest.PackName))
        {
            modpack.DisplayName = manifest.PackName;
            metadataChanged = true;
        }

        if (string.IsNullOrWhiteSpace(modpack.Author) && !string.IsNullOrWhiteSpace(manifest.Author))
        {
            modpack.Author = manifest.Author;
            metadataChanged = true;
        }

        if (string.IsNullOrWhiteSpace(modpack.CustomMcVersion) && !string.IsNullOrWhiteSpace(manifest.MinecraftVersion))
        {
            modpack.CustomMcVersion = manifest.MinecraftVersion;
            metadataChanged = true;
        }

        if (string.IsNullOrWhiteSpace(modpack.CustomModLoader) && !string.IsNullOrWhiteSpace(manifest.ModLoaderType))
        {
            modpack.CustomModLoader = manifest.ModLoaderType;
            metadataChanged = true;
        }

        var loaderVersion = ExtractManifestLoaderVersion(manifest.ModLoaderId);
        if (string.IsNullOrWhiteSpace(modpack.CustomModLoaderVersion) && !string.IsNullOrWhiteSpace(loaderVersion))
        {
            modpack.CustomModLoaderVersion = loaderVersion;
            metadataChanged = true;
        }

        if (modpack.CurrentVersion == null && (!string.IsNullOrWhiteSpace(manifest.Version) || manifest.FileId > 0))
        {
            modpack.CurrentVersion = new ModpackVersion
            {
                Name = string.IsNullOrWhiteSpace(manifest.Version) ? $"File {manifest.FileId}" : manifest.Version,
                FileId = manifest.FileId > 0 ? manifest.FileId.ToString() : "0"
            };
            metadataChanged = true;
        }

        if (string.IsNullOrWhiteSpace(modpack.Source))
        {
            if (modpack.ProjectId > 0)
            {
                modpack.Source = "CurseForge";
                metadataChanged = true;
            }
            else if (!string.IsNullOrWhiteSpace(modpack.ModrinthId))
            {
                modpack.Source = "Modrinth";
                metadataChanged = true;
            }
        }

        return metadataChanged;
    }

    private static string ExtractManifestLoaderVersion(string? modLoaderId)
    {
        if (string.IsNullOrWhiteSpace(modLoaderId))
        {
            return string.Empty;
        }

        var separatorIndex = modLoaderId.IndexOf('-');
        return separatorIndex > 0 && separatorIndex < modLoaderId.Length - 1
            ? modLoaderId[(separatorIndex + 1)..]
            : string.Empty;
    }

    private static bool ShouldReplaceCurrentModpackName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        return string.Equals(value.Trim(), "Načítání...", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value.Trim(), "Loading...", StringComparison.OrdinalIgnoreCase);
    }
}
