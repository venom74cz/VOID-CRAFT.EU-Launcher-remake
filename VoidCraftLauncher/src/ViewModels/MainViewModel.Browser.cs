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
/// Modpack browser/discover: CurseForge + Modrinth search, pagination, install from browser.
/// </summary>
public partial class MainViewModel
{
    // ===== BROWSER STATE =====

    [ObservableProperty]
    private ObservableCollection<ModpackItem> _browserResults = new();

    [ObservableProperty]
    private string _browserSearchQuery = "";

    [ObservableProperty]
    private string _browserSource = "CurseForge"; // "CurseForge" or "Modrinth"

    [ObservableProperty]
    private bool _isSearching = false;

    // Pagination properties
    [ObservableProperty]
    private int _currentBrowserPage = 0;

    [ObservableProperty]
    private bool _hasMoreResults = false;

    // ===== BROWSER COMMANDS =====

    [RelayCommand]
    public async Task SetBrowserSource(string source)
    {
        BrowserSource = source;
        IsSearching = false; 
        BrowserResults.Clear();
        await SearchModpacks();
    }

    [RelayCommand]
    public void OpenBrowser(string source)
    {
        BrowserSource = source;
        BrowserSearchQuery = "";
        BrowserResults.Clear();
        NavigateToView(MainViewType.Discover, true);
        SearchModpacksCommand.Execute(null);
    }

    [RelayCommand]
    public async Task SearchModpacks()
    {
        if (IsSearching) return;
        IsSearching = true;
        BrowserResults.Clear();
        CurrentBrowserPage = 0;

        try
        {
            await FetchModpacksPage(CurrentBrowserPage);
        }
        catch (Exception ex)
        {
            Greeting = $"Chyba vyhledávání: {ex.Message}";
        }
        finally
        {
            IsSearching = false;
        }
    }

    [RelayCommand]
    public async Task LoadMoreModpacks()
    {
        if (IsSearching || !HasMoreResults) return;
        IsSearching = true;

        try
        {
            CurrentBrowserPage++;
            await FetchModpacksPage(CurrentBrowserPage);
        }
        catch (Exception ex)
        {
            Greeting = $"Chyba při načítání dalších výsledků: {ex.Message}";
            CurrentBrowserPage--;
        }
        finally
        {
            IsSearching = false;
        }
    }

    private async Task FetchModpacksPage(int page)
    {
        if (BrowserSource == "CurseForge")
        {
            await SearchCurseForge(page);
        }
        else
        {
            await SearchModrinth(page);
        }
    }

    private async Task SearchCurseForge(int page)
    {
        int offset = page * 50; 
        string json;
        if (string.IsNullOrWhiteSpace(BrowserSearchQuery))
            json = await _curseForgeApi.SearchModpacksAsync("", offset);
        else
            json = await _curseForgeApi.SearchModpacksAsync(BrowserSearchQuery, offset);

         var root = JsonNode.Parse(json);
         var data = root?["data"]?.AsArray();

         if (data != null)
         {
             HasMoreResults = data.Count == 50;

             foreach (var item in data)
             {
                 var mp = new ModpackItem
                 {
                     Name = item["name"]?.ToString() ?? "Unknown",
                     Description = item["summary"]?.ToString() ?? "",
                     Author = item["authors"]?[0]?["name"]?.ToString() ?? "Unknown",
                     IconUrl = item["logo"]?["thumbnailUrl"]?.ToString() ?? "",
                     Id = item["id"]?.ToString() ?? "",
                     Source = "CurseForge",
                     WebLink = item["links"]?["websiteUrl"]?.ToString() ?? "",
                     DownloadCount = item["downloadCount"]?.GetValue<long>() ?? 0
                 };
                 BrowserResults.Add(mp);
             }
         }
         else
         {
             HasMoreResults = false;
         }
    }

    private async Task SearchModrinth(int page)
    {
        int offset = page * 50;
        string json = await _modrinthApi.SearchModpacksAsync(BrowserSearchQuery, offset);

        var root = JsonNode.Parse(json);
        var hits = root?["hits"]?.AsArray();
        var totalHits = root?["total_hits"]?.GetValue<int>() ?? 0;

        if (hits != null)
        {
            HasMoreResults = (offset + hits.Count) < totalHits;

            foreach (var item in hits)
            {
                var mp = new ModpackItem
                {
                    Name = item["title"]?.ToString() ?? "Unknown",
                    Description = item["description"]?.ToString() ?? "",
                    Author = item["author"]?.ToString() ?? "Unknown",
                    IconUrl = item["icon_url"]?.ToString() ?? "",
                    Id = item["project_id"]?.ToString() ?? "",
                    Source = "Modrinth",
                    WebLink = $"https://modrinth.com/modpack/{item["slug"]}",
                    DownloadCount = item["downloads"]?.GetValue<long>() ?? 0
                };
                BrowserResults.Add(mp);
            }
        }
        else
        {
             HasMoreResults = false;
        }
    }

    [RelayCommand]
    public async Task InstallModpackFromBrowser(ModpackItem item)
    {
        if (IsSearching || IsLaunching) return;
        
        IsSearching = false;
        
        var newModpack = new ModpackInfo
        {
            Name = item.Name,
            DisplayName = item.Name,
            LogoUrl = item.IconUrl,
            Description = item.Description,
            Author = item.Author,
            WebLink = item.WebLink,
            Source = item.Source,
            ModrinthId = item.Source == "Modrinth" ? item.Id : "",
            ProjectId = item.Source == "CurseForge" ? (int.TryParse(item.Id, out var id) ? id : 0) : 0
        };
        
        CurrentModpack = newModpack;
        Avalonia.Threading.Dispatcher.UIThread.Post(() => InstalledModpacks.Add(CurrentModpack));
        GoToHome(); 
                
        _ = Task.Run(async () => 
        {
            try
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => 
                {
                    IsLaunching = true;
                    LaunchStatus = $"Připravuji instalaci {item.Name}...";
                    LaunchProgress = 0;
                });

                string downloadUrl = "";
                string fileName = "modpack.zip";
                string versionId = "0";
                string versionDisplayName = "Latest";
                List<string> downloadCandidates;

                if (item.Source == "CurseForge")
                {
                    var json = await _curseForgeApi.GetModpackFilesAsync(int.Parse(item.Id));
                    var root = JsonNode.Parse(json);
                    var data = root?["data"]?.AsArray();
                    
                    var file = data?.Where(x => x?["releaseType"]?.GetValue<int>() == 1).FirstOrDefault() 
                               ?? data?.FirstOrDefault(); 

                    if (file == null) throw new Exception("Nenalezena žádná verze.");

                    downloadUrl = file["downloadUrl"]?.ToString();
                    fileName = file["fileName"]?.ToString() ?? "modpack.zip";
                    versionId = file["id"]?.ToString() ?? "0";
                    versionDisplayName = file["displayName"]?.ToString() ?? "Latest";

                    if (!int.TryParse(versionId, out var curseFileId)) throw new Exception("Chybí validní FileId modpacku.");
                    downloadCandidates = await BuildCurseForgeArchiveDownloadCandidatesAsync(int.Parse(item.Id), curseFileId, downloadUrl, fileName);
                }
                else // Modrinth
                {
                    var json = await _modrinthApi.GetProjectVersionsAsync(item.Id);
                    var versions = JsonNode.Parse(json)?.AsArray();
                    var version = versions?.FirstOrDefault(v => v?["version_type"]?.ToString() == "release")
                                  ?? versions?.FirstOrDefault();

                    if (version == null) throw new Exception("Nenalezena žádná verze.");

                    var files = version["files"]?.AsArray();
                    var primaryFile = files?.FirstOrDefault(f => f?["primary"]?.GetValue<bool>() == true)
                                      ?? files?.FirstOrDefault();
                    
                    if (primaryFile == null) throw new Exception("Chybí soubor verze.");

                    downloadUrl = primaryFile["url"]?.ToString();
                    fileName = primaryFile["filename"]?.ToString() ?? "modpack.mrpack";
                    versionId = version["id"]?.ToString();
                    versionDisplayName = version["version_number"]?.ToString() ?? versionId ?? "1.0";
                    downloadCandidates = BuildModrinthArchiveDownloadCandidates(files, primaryFile);
                }

                if (downloadCandidates.Count == 0) throw new Exception("Chybí URL.");

                Avalonia.Threading.Dispatcher.UIThread.Post(() => LaunchStatus = "Stahuji balíček...");
                var tempPath = Path.Combine(Path.GetTempPath(), fileName);
                await DownloadPackageArchiveAsync(downloadCandidates, tempPath, versionDisplayName);
                
                var safeName = string.Join("_", item.Name.Split(Path.GetInvalidFileNameChars())).Trim();
                var installPath = _launcherService.GetModpackPath(safeName);
                
                Avalonia.Threading.Dispatcher.UIThread.Post(() => LaunchStatus = "Instaluji...");

                void OnStatus(string s) => Avalonia.Threading.Dispatcher.UIThread.Post(() => LaunchStatus = s);
                void OnProgress(double p) => Avalonia.Threading.Dispatcher.UIThread.Post(() => LaunchProgress = p * 100);
                
                _modpackInstaller.StatusChanged += OnStatus;
                _modpackInstaller.ProgressChanged += OnProgress;

                ModpackManifestInfo manifestInfo = new ModpackManifestInfo();
                try 
                {
                    int? targetFileId = item.Source == "CurseForge" && int.TryParse(versionId, out var parsedFileId)
                        ? parsedFileId
                        : null;

                    manifestInfo = await _modpackInstaller.InstallOrUpdateAsync(
                        tempPath,
                        installPath,
                        targetFileId,
                        versionDisplayName);
                }
                catch (Exception ex)
                {
                     Avalonia.Threading.Dispatcher.UIThread.Post(() => Greeting = $"Chyba instalace: {ex.Message}");
                     LogService.Error("Install Modpack Error", ex);
                     try { Directory.Delete(installPath, true); } catch {}
                     return;
                }
                finally
                {
                    _modpackInstaller.StatusChanged -= OnStatus;
                    _modpackInstaller.ProgressChanged -= OnProgress;
                    if (File.Exists(tempPath)) File.Delete(tempPath);
                }

                Avalonia.Threading.Dispatcher.UIThread.Post(() => 
                {
                    var versionInfo = new ModpackVersion 
                    { 
                        Name = versionDisplayName,
                        FileId = versionId ?? "0"
                    };
                    
                    CurrentModpack = new ModpackInfo
                    {
                        Name = safeName,
                        DisplayName = item.Name,
                        ProjectId = item.Source == "CurseForge" ? int.Parse(item.Id) : 0,
                        Source = item.Source,
                        ModrinthId = item.Source == "Modrinth" ? item.Id : "",
                        LogoUrl = item.IconUrl,
                        Author = item.Author,
                        WebLink = item.WebLink,
                        Description = item.Description,
                        CurrentVersion = versionInfo
                    };

                    var existing = InstalledModpacks.FirstOrDefault(m => 
                        (CurrentModpack.ProjectId > 0 && m.ProjectId == CurrentModpack.ProjectId) ||
                        m.Name.Equals(CurrentModpack.Name, StringComparison.OrdinalIgnoreCase));

                        if (existing != null)
                        {
                            var index = InstalledModpacks.IndexOf(existing);
                            InstalledModpacks[index] = CurrentModpack;
                        }
                        else
                        {
                            InstalledModpacks.Add(CurrentModpack);
                        }
                        SaveModpacks();

                        IsLaunching = false;
                        LaunchStatus = "Nainstalováno - Připraveno ke hře";
                        LaunchProgress = 100;
                        Greeting = $"Instalace dokončena: {item.Name}";
                    });
                }
            catch (Exception ex)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => 
                {
                    IsLaunching = false;
                    Greeting = $"Chyba instalace: {ex.Message}";
                    LaunchStatus = "Chyba";
                });
            }
        });
    }

    [RelayCommand]
    public void OpenDashboard(ModpackInfo modpack)
    {
        CurrentModpack = modpack;
    }
}
