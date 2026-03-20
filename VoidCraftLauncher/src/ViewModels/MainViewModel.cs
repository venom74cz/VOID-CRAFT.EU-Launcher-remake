using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VoidCraftLauncher.Services;
using VoidCraftLauncher.Models;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Linq;
using CmlLib.Core.Auth;
using System.Diagnostics;
using System.Net.Http;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Collections.Specialized;
using System.Text.RegularExpressions;

namespace VoidCraftLauncher.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public ObservableCollection<ModpackInfo> InstalledModpacks { get; private set; }
    
    private readonly AuthService _authService;
    private readonly LauncherService _launcherService;
    private readonly CurseForgeApi _curseForgeApi;
    private readonly ModrinthApi _modrinthApi;
    private readonly HttpClient _httpClient;
    private readonly ModpackInstaller _modpackInstaller;
    private readonly DiscordRpcService _discordRpcService;
    private ModpackManifestInfo _lastManifestInfo;
    private readonly SemaphoreSlim _modpackUpdateCheckLock = new(1, 1);
    private static readonly TimeSpan ModpackUpdateCheckInterval = TimeSpan.FromSeconds(5);

    [ObservableProperty]
    private string _serverStatusText = "Načítám...";

    [ObservableProperty]
    private int _serverPlayerCount = 0;

    [ObservableProperty]
    private int _serverMaxPlayers = 100;

    [ObservableProperty]
    private bool _isServerOnline = false;

    [ObservableProperty]
    private LauncherConfig _config;

    [ObservableProperty]
    private ModpackInfo _currentModpack;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCurrentModpackScreenshots))]
    private ObservableCollection<string> _currentModpackScreenshots = new();

    public bool HasCurrentModpackScreenshots => CurrentModpackScreenshots.Count > 0;

    [ObservableProperty]
    private MSession _userSession;

    [ObservableProperty]
    private bool _isLoggedIn;

    // Skin providers: mc-heads.net, crafatar.com, minotar.net
    public string PlayerSkinUrl => UserSession?.UUID != null 
        ? $"https://mc-heads.net/avatar/{UserSession.UUID.Replace("-", "")}/40" 
        : "https://mc-heads.net/avatar/MHF_Steve/40";

    // Expose Enum values for UI
    public List<GcType> GcTypes { get; } = Enum.GetValues(typeof(GcType)).Cast<GcType>().ToList();

    [ObservableProperty]
    private string _greeting = "Vítejte ve VOID-CRAFT Launcheru!";

    [ObservableProperty]
    private string _authUrl;

    [ObservableProperty]
    private string _manualLoginCode;

    [ObservableProperty]
    private string _loginStatus = "";

    [ObservableProperty]
    private bool _isLoginInProgress = false;

    [ObservableProperty]
    private bool _isWebviewVisible = true;

    [ObservableProperty]
    private bool _isBrowserPanelVisible = false;

    [ObservableProperty]
    private bool _isLaunchIndeterminate = false;

    public enum MainViewType
    {
        Library,
        Discover,
        Settings,
        InstanceDetail
    }

    [ObservableProperty]
    private MainViewType _currentMainView = MainViewType.Library;

    public bool IsLibraryView => CurrentMainView == MainViewType.Library;
    public bool IsDiscoverView => CurrentMainView == MainViewType.Discover;
    public bool IsSettingsView => CurrentMainView == MainViewType.Settings;
    public bool IsInstanceDetailView => CurrentMainView == MainViewType.InstanceDetail;

    partial void OnCurrentMainViewChanged(MainViewType value)
    {
        OnPropertyChanged(nameof(IsLibraryView));
        OnPropertyChanged(nameof(IsDiscoverView));
        OnPropertyChanged(nameof(IsSettingsView));
        OnPropertyChanged(nameof(IsInstanceDetailView));
        OnPropertyChanged(nameof(MainPanelTitle));

        if (value == MainViewType.InstanceDetail)
        {
            LoadInstalledMods();
            _ = FetchFullDescriptionAsync();
        }

        UpdateDiscordPresence();
    }

    private void UpdateDiscordPresence()
    {
        string details = MainPanelTitle;
        string state = CurrentMainView switch
        {
            MainViewType.Library => "Prohlíží si knihovnu",
            MainViewType.Discover => $"Hledá nové modpacky ({BrowserSource})",
            MainViewType.Settings => "Upravuje nastavení",
            MainViewType.InstanceDetail => $"Detail: {CurrentModpack?.Name}",
            _ => "V hlavní nabídce"
        };

        _discordRpcService.SetState(details, state);
    }

    private async Task FetchFullDescriptionAsync()
    {
        if (CurrentModpack == null) return;
        
        // Don't refetch if already have a long description (optional optimization)
        // if (CurrentModpack.Description.Length > 500) return;

        try
        {
            string fullDescription = "";
            if (CurrentModpack.Source == "CurseForge" && CurrentModpack.ProjectId > 0)
            {
                fullDescription = await _curseForgeApi.GetProjectDescriptionAsync(CurrentModpack.ProjectId);
            }
            else if (CurrentModpack.Source == "Modrinth" && !string.IsNullOrEmpty(CurrentModpack.ModrinthId))
            {
                fullDescription = await _modrinthApi.GetProjectDescriptionAsync(CurrentModpack.ModrinthId);
            }

            if (!string.IsNullOrWhiteSpace(fullDescription))
            {
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => 
                {
                    CurrentModpack.Description = fullDescription;
                });
            }
        }
        catch (Exception ex)
        {
            LogService.Error($"[FetchFullDescriptionAsync] Failed for {CurrentModpack.Name}", ex);
        }
    }

    public string MainPanelTitle => CurrentMainView switch
    {
        MainViewType.Library => "Knihovna Modpacků",
        MainViewType.Discover => $"Procházet Modpacky ({BrowserSource})",
        MainViewType.Settings => "Nastavení",
        MainViewType.InstanceDetail => CurrentModpack?.Name ?? "Detail Modpacku",
        _ => "VoidCraft Launcher"
    };

    // Detect system RAM (in MB)
    public int SystemRamMb => (int)(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024 / 1024);

    [ObservableProperty]
    private bool _isLaunching = false;

    [ObservableProperty]
    private double _launchProgress = 0;

    [ObservableProperty]
    private ModpackInfo? _targetModpack; // Used to track which modpack is actively installing/launching so progress bars don't jump on navigation

    [ObservableProperty]
    private string _launchStatus = "";

    [ObservableProperty]
    private string _currentFile = "";

    [ObservableProperty]
    private bool _isGameRunning = false;

    [ObservableProperty]
    private ModpackInfo? _runningModpack;

    [ObservableProperty]
    private bool _isLoginModalVisible = false;

    [ObservableProperty]
    private string _offlineUsername = "";

    [ObservableProperty]
    private string _appVersion = "v1.0.0"; // Placeholder

    [ObservableProperty]
    private ObservableCollection<ChangelogEntry> _changelogEntries = new();

    // Multi-Account
    [ObservableProperty]
    private ObservableCollection<AccountProfile> _accounts = new();

    [ObservableProperty]
    private AccountProfile? _activeAccount;

    [ObservableProperty]
    private bool _isAccountPickerOpen = false;

    public MainViewModel()
    {
        // Set Version
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        AppVersion = $"v{version?.ToString(3) ?? "?.?.?"}";

        // Cleanup old update backups
        try 
        {
            var myPath = Environment.ProcessPath;
            if (myPath != null)
            {
                var bakPath = Path.ChangeExtension(myPath, ".bak");
                if (File.Exists(bakPath)) File.Delete(bakPath);
            }
        }
        catch { /* Ignore if locked */ }

        // Restore offline username if used previously? (Can be added later if needed)
        _authService = new AuthService();
        _launcherService = new LauncherService();
        _curseForgeApi = new CurseForgeApi();
        _modrinthApi = new ModrinthApi();
        _modpackInstaller = new ModpackInstaller(_curseForgeApi);
        _discordRpcService = new DiscordRpcService();
        _discordRpcService.Initialize();
        
        // Forward installer events to UI
        _modpackInstaller.StatusChanged += (status) => LaunchStatus = status;
        _modpackInstaller.ProgressChanged += (progress) => 
        {
            LaunchProgress = progress * 100;
            IsLaunchIndeterminate = false;
        };

        _currentModpack = new ModpackInfo();
        InstalledModpacks = new ObservableCollection<ModpackInfo>();
        CurrentModpackScreenshots.CollectionChanged += OnCurrentModpackScreenshotsChanged;
        
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "VoidCraftLauncher/1.0");
        
        // Load Config
        try 
        {
            Config = _launcherService.LoadConfig();
        } 
        catch 
        {
            Config = new LauncherConfig();
        }


        // Restore offline username
        if (!string.IsNullOrEmpty(Config.LastOfflineUsername))
        {
            OfflineUsername = Config.LastOfflineUsername;
        }

        // Load saved accounts
        if (Config.Accounts != null && Config.Accounts.Count > 0)
        {
            foreach (var acc in Config.Accounts)
                Accounts.Add(acc);
        }
        else if (!string.IsNullOrEmpty(Config.LastOfflineUsername))
        {
            // Migration: convert old single offline username to account profile
            var migrated = new AccountProfile
            {
                DisplayName = Config.LastOfflineUsername,
                Type = AccountType.Offline
            };
            Accounts.Add(migrated);
            Config.Accounts.Add(migrated);
            Config.ActiveAccountId = migrated.Id;
            _launcherService.SaveConfig(Config);
        }

        // Výchozí offline session
        _userSession = MSession.CreateOfflineSession("Guest");
        IsLoggedIn = false;

        // Spustíme načítání na pozadí
        Task.Run(LoadModpackData);

        // Načtení changelogu z GitHub repa (live)
        Task.Run(LoadChangelogAsync);

        // Kontrola aktualizací modpacků: hned při startu a pak každých 5s
        Task.Run(ModpackUpdateLoop);
        
        // Zkusíme auto-login z cache
        Task.Run(TryAutoLogin);

        // Update Check
        Task.Run(CheckForUpdates);
        
        // Server Status Update Loop
        Task.Run(async () => 
        {
            while (true)
            {
                await UpdateServerStatus();
                await Task.Delay(60000); // 1 min update
            }
        });
    }

    partial void OnCurrentModpackChanged(ModpackInfo value)
    {
        _ = LoadCurrentModpackScreenshotsAsync();
    }

    private void OnCurrentModpackScreenshotsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasCurrentModpackScreenshots));
    }

    private async Task LoadCurrentModpackScreenshotsAsync()
    {
        try
        {
            if (CurrentModpack == null || string.IsNullOrWhiteSpace(CurrentModpack.Name))
            {
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => CurrentModpackScreenshots.Clear());
                return;
            }

            var modpackPath = _launcherService.GetModpackPath(CurrentModpack.Name);
            var screenshotsPath = Path.Combine(modpackPath, "screenshots");
            var screenshotyPath = Path.Combine(modpackPath, "screenshoty");

            var targetFolder = Directory.Exists(screenshotsPath)
                ? screenshotsPath
                : (Directory.Exists(screenshotyPath) ? screenshotyPath : screenshotsPath);

            if (!Directory.Exists(targetFolder))
            {
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => CurrentModpackScreenshots.Clear());
                return;
            }

            var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp"
            };

            var screenshots = Directory
                .GetFiles(targetFolder)
                .Where(file => allowedExtensions.Contains(Path.GetExtension(file)))
                .OrderByDescending(file => File.GetLastWriteTimeUtc(file))
                .Select(file => new Uri(file).AbsoluteUri)
                .ToList();

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                CurrentModpackScreenshots.Clear();
                foreach (var screenshot in screenshots)
                {
                    CurrentModpackScreenshots.Add(screenshot);
                }
            });
        }
        catch (Exception ex)
        {
            LogService.Error("[LoadCurrentModpackScreenshotsAsync] Failed", ex);
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => CurrentModpackScreenshots.Clear());
        }
    }

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
    
    [RelayCommand]
    public async Task CheckForUpdates()
    {
        try
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => Greeting = "🔄 Kontroluji aktualizace...");
            LogService.Log("Checking for updates via GitHub...");
            var currentVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            
            // GitHub API requires User-Agent
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("VOID-CRAFT-Launcher");
            
            var response = await _httpClient.GetStringAsync("https://api.github.com/repos/venom74cz/VOID-CRAFT.EU-Launcher-remake/releases/latest");
            var json = JsonNode.Parse(response);
            
            var tagName = json?["tag_name"]?.ToString(); // e.g. "v1.0.1" or "v1.0.1-alpha"
            var cleanVersion = tagName?.TrimStart('v');
            
            // Remove suffixes for comparison (simple check)
            if (cleanVersion?.Contains('-') == true)
                cleanVersion = cleanVersion.Split('-')[0];

            var assets = json?["assets"]?.AsArray();
            var downloadUrl = assets?.FirstOrDefault(a => a?["name"]?.ToString().EndsWith("Setup.exe") == true)?["browser_download_url"]?.ToString();
            
            // Only proceed if Setup.exe is found
            if (Version.TryParse(cleanVersion, out var latestVersion) && !string.IsNullOrEmpty(downloadUrl))
            {
                if (latestVersion > currentVersion)
                {
                    LogService.Log($"New version found: {latestVersion} (Current: {currentVersion})");
                    
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => 
                    {
                        Greeting = $"Stahuji aktualizaci {tagName}...";
                    });

                    // Perform Auto-Update
                    await PerformUpdate(downloadUrl);
                }
                else
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => Greeting = $"✅ Máš nejnovější verzi ({currentVersion})");
                    await Task.Delay(3000); // Show for 3s then restore
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => Greeting = $"Vítejte, {UserSession?.Username ?? "Hráči"}!");
                }
            }
        }
        catch (Exception ex)
        {
            LogService.Error("Update check failed", ex);
            Avalonia.Threading.Dispatcher.UIThread.Post(() => Greeting = "❌ Chyba kontroly aktualizací.");
        }
    }

    private async Task PerformUpdate(string url)
    {
        try
        {
            var tempPath = Path.Combine(Path.GetTempPath(), "VoidCraftLauncher_Setup.exe");
            
            // 1. Download Setup.exe
            var data = await _httpClient.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(tempPath, data);

            Avalonia.Threading.Dispatcher.UIThread.Post(() => Greeting = "Spouštím instalátor...");

            // 2. Run Installer
            // /VERYSILENT = No UI, /SUPPRESSMSGBOXES = No prompts, /NORESTART = Don't reboot OS
            // But we might want /SILENT to show progress bar? User prefers "Clean Reinstall" so maybe full Wizard?
            // "chci vždy aby to krásně reinstalovalo appku" implies visible process.
            // Let's use /SILENT (Progress bar only) or default (Wizard). 
            // Default is safest so user knows what is happening.
            
            LogService.Log("Update downloaded. running installer...");
            
            Process.Start(new ProcessStartInfo 
            {
                FileName = tempPath,
                Arguments = "/SILENT /SP-", // Silent install, no startup prompt
                UseShellExecute = true
            });

            // 3. Exit Launcher to allow overwrite
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
             LogService.Error("Update failed", ex);
             Avalonia.Threading.Dispatcher.UIThread.Post(() => Greeting = "❌ Chyba aktualizace: " + ex.Message);
        }
    }



    [ObservableProperty]
    private string _serverMotd = "Načítání...";

    private async Task UpdateServerStatus()
    {
        try
        {
            // Using mcsrvstat.us API
            var response = await _httpClient.GetStringAsync("https://api.mcsrvstat.us/2/mc.void-craft.eu");
            var json = JsonNode.Parse(response);
            
            if (json != null && json["online"]?.GetValue<bool>() == true)
            {
                var players = json["players"];
                var online = players?["online"]?.GetValue<int>() ?? 0;
                var max = players?["max"]?.GetValue<int>() ?? 0;
                
                // Parse MOTD (show full text as requested by user, joined with spaces for single line)
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

    private async Task TryAutoLogin()
    {
        try
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => Greeting = "Přihlašuji...");
            
            // Find active account from config
            var activeAcc = Accounts.FirstOrDefault(a => a.Id == Config.ActiveAccountId)
                            ?? Accounts.FirstOrDefault();

            if (activeAcc != null)
            {
                MSession? session = null;

                if (activeAcc.Type == AccountType.Microsoft && !string.IsNullOrEmpty(activeAcc.MsalAccountId))
                {
                    session = await _authService.TrySilentLoginForAccountAsync(activeAcc.MsalAccountId);
                }
                else if (activeAcc.Type == AccountType.Offline)
                {
                    session = _authService.LoginOffline(activeAcc.DisplayName);
                }

                if (session != null)
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        UserSession = session;
                        IsLoggedIn = true;
                        ActiveAccount = activeAcc;
                        OnPropertyChanged(nameof(PlayerSkinUrl));
                        var suffix = activeAcc.Type == AccountType.Offline ? " (Offline)" : "";
                        Greeting = $"Vítejte, {session.Username}{suffix}!";
                    });
                    return;
                }
            }

            // Fallback: try generic silent login (backward compat)
            var fallbackSession = await _authService.TrySilentLoginAsync();
            if (fallbackSession != null)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    UserSession = fallbackSession;
                    IsLoggedIn = true;
                    OnPropertyChanged(nameof(PlayerSkinUrl));
                    Greeting = $"Vítejte, {fallbackSession.Username}!";
                });
            }
            else
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => Greeting = "Vítejte ve VOID-CRAFT Launcheru!");
            }
        }
        catch
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => Greeting = "Vítejte ve VOID-CRAFT Launcheru!");
        }
    }

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
                // Match version headers like "## 2.1.0 - 2026-03-15" or "## [2.1.0] - 2026-03-15" or "## 1.2.8"
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

                // Match section titles like "### 🧠 Chytrý Update Configů"
                var sectionMatch = Regex.Match(line, @"^###\s+(.+)$");
                if (sectionMatch.Success)
                {
                    if (string.IsNullOrEmpty(current.Title))
                        current.Title = sectionMatch.Groups[1].Value.Trim();
                    continue;
                }

                // Match bullet items like "- **Something**: Description"
                var itemMatch = Regex.Match(line, @"^-\s+(.+)$");
                if (itemMatch.Success)
                {
                    // Clean markdown bold
                    var text = Regex.Replace(itemMatch.Groups[1].Value, @"\*\*([^*]+)\*\*", "$1");
                    current.Items.Add(text);
                }
            }

            // Take only the last few entries for the UI
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

    private async Task LoadModpackData()
    {
        try
        {
            // VOID-BOX2 CurseForge Project ID
            const int VOID_BOX_PROJECT_ID = 1402056;
            
            LogService.Log($"[LoadModpackData] Fetching modpack ID: {VOID_BOX_PROJECT_ID}");
            
            // 1. Získáme info o modpacku přímo pomocí ID
            var modpackJson = await _curseForgeApi.GetModpackInfoAsync(VOID_BOX_PROJECT_ID);
            
            var root = JsonNode.Parse(modpackJson);
            var modpack = root?["data"];
            var name = modpack?["name"]?.ToString();
            var logo = modpack?["logo"]?["url"]?.ToString();
            var summary = modpack?["summary"]?.ToString();
            var id = modpack?["id"]?.GetValue<int>();
            
            LogService.Log($"[LoadModpackData] Parsed - Name: {name}, ID: {id}");

            // Načteme všechny soubory (verze)
            var versionsList = new System.Collections.ObjectModel.ObservableCollection<ModpackVersion>();
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
                        // Seřadíme od nejnovějšího
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
                Name = name ?? "VOID-BOX 2",
                LogoUrl = logo ?? "",
                Description = summary ?? "",
                CurrentVersion = selectedVersion,
                Versions = versionsList,
                IsDeletable = false
            };

            // Add to Library if not exists
            if (!InstalledModpacks.Any(m => m.ProjectId == CurrentModpack.ProjectId))
            {
                // Dispatcher? LoadModpackData runs in Task.Run
                Avalonia.Threading.Dispatcher.UIThread.Post(() => InstalledModpacks.Add(CurrentModpack));
            }
            
            // Load other saved modpacks
            await Task.Run(LoadSavedModpacks);
        }
        catch (Exception ex)
        {
            // Fallback při chybě - show error in UI!
            LogService.Error("[LoadModpackData] ERROR", ex);
            Greeting = $"API Error: {ex.Message}";
            CurrentModpack = new ModpackInfo { Name = $"Chyba: {ex.Message.Substring(0, Math.Min(50, ex.Message.Length))}", CurrentVersion = new ModpackVersion { Name = "-" } };
        }
    }

    [RelayCommand]
    public void ToggleModpackBrowser()
    {
        IsWebviewVisible = !IsWebviewVisible;
        IsBrowserPanelVisible = !IsBrowserPanelVisible;
    }

    [RelayCommand]
    public async Task PlayModpack()
    {
        if (IsLaunching) return;

        if (!IsLoggedIn)
        {
            Greeting = "Nejdříve se přihlas!";
            return;
        }

        if (CurrentModpack?.CurrentVersion == null)
        {
            Greeting = "Žádný modpack není vybrán.";
            return;
        }

        try
        {
            TargetModpack = CurrentModpack;
            IsLaunching = true;
            LaunchProgress = 0;

            // ---------------------------------------------------------
            // 1. CHECK & DOWNLOAD & VERIFY (ALWAYS RUN)
            // ---------------------------------------------------------
            var modpackDir = _launcherService.GetModpackPath(CurrentModpack.Name);
            var modsDir = Path.Combine(modpackDir, "mods");
            
            // 1.1 TRY UPDATING (IF PROJECT ID EXISTS)
            ModpackManifestInfo manifestInfo = null;
            bool attemptsUpdate = CurrentModpack.ProjectId > 0 && !CurrentModpack.IsCustomProfile;

            if (attemptsUpdate)
            {
                try
                {
                    LaunchStatus = "Ověřuji aktualizace...";
                    ModpackManifestInfo installedManifest = ModpackInstaller.LoadManifestInfo(modpackDir);
                    
                    int fileId = 0;
                    if (int.TryParse(CurrentModpack.CurrentVersion?.FileId, out var parsedId)) 
                        fileId = parsedId;

                    // Fetch latest if update available
                    if (CurrentModpack.IsUpdateAvailable)
                    {
                        var filesJson = await _curseForgeApi.GetModpackFilesAsync(CurrentModpack.ProjectId);
                        var latestFile = JsonNode.Parse(filesJson)?["data"]?.AsArray()
                            .OrderByDescending(f => f?["fileDate"]?.ToString()).FirstOrDefault();
                        
                        if (int.TryParse(latestFile?["id"]?.ToString(), out var latestId))
                            fileId = latestId;
                    }

                    int currentModCount = Directory.Exists(Path.Combine(modpackDir, "mods")) 
                        ? Directory.GetFiles(Path.Combine(modpackDir, "mods"), "*.jar").Length 
                        : 0;
                        
                    bool isBrokenGTNH = IsGTNHModpack(CurrentModpack.Name, modpackDir) && 
                                       (currentModCount < 100 || !Directory.GetFiles(Path.Combine(modpackDir, "mods"), "GTNewHorizonsCoreMod*", SearchOption.TopDirectoryOnly).Any());

                    if (installedManifest == null || installedManifest.FileId != fileId || isBrokenGTNH)
                    {
                        if (isBrokenGTNH) LogService.Log($"GTNH installation appears broken (Mods: {currentModCount}). Force repair.", "WARN");
                        var fileJson = await _curseForgeApi.GetModFileAsync(CurrentModpack.ProjectId, fileId);
                        var dataNode = JsonNode.Parse(fileJson)?["data"];
                        var downloadUrl = dataNode?["downloadUrl"]?.ToString();
                        var fileName = dataNode?["fileName"]?.ToString() ?? "modpack.zip";

                        if (string.IsNullOrEmpty(downloadUrl) && !string.IsNullOrEmpty(fileName))
                        {
                            var idStr = fileId.ToString();
                            if (idStr.Length >= 4)
                                downloadUrl = $"https://edge.forgecdn.net/files/{idStr.Substring(0, 4)}/{idStr.Substring(4)}/{fileName}";
                        }

                        if (!string.IsNullOrEmpty(downloadUrl))
                        {
                            LaunchStatus = "Stahuji aktualizaci...";
                            var tempZip = Path.Combine(Path.GetTempPath(), fileName);
                            var data = await _httpClient.GetByteArrayAsync(downloadUrl);
                            await File.WriteAllBytesAsync(tempZip, data);

                            LaunchStatus = "Instaluji aktualizaci...";
                            manifestInfo = await _modpackInstaller.InstallOrUpdateAsync(tempZip, modpackDir, fileId);
                            try { File.Delete(tempZip); } catch {}
                        }
                    }
                    else
                    {
                        manifestInfo = installedManifest;
                    }
                }
                catch (Exception ex)
                {
                    LogService.Error("Update check failed", ex);
                    LaunchStatus = "Aktualizace selhala (Pokračuji s tím, co je nainstalováno...)";
                    await Task.Delay(1000);
                }
            }

            // 1.2 LOAD LOCAL CACHE IF NEEDED (For Modrinth or Custom or Failed Update)
            if (manifestInfo == null)
            {
                manifestInfo = ModpackInstaller.LoadManifestInfo(modpackDir);
            }

            // 1.3 VALIDATION
            if (manifestInfo == null && !CurrentModpack.IsCustomProfile)
            {
                LaunchStatus = "Chyba: Modpack není korektně nainstalován.";
                await Task.Delay(3000);
                IsLaunching = false;
                TargetModpack = null;
                return;
            }

            // Store for launch
            _lastManifestInfo = manifestInfo;
            if (manifestInfo != null && manifestInfo.FileId > 0 && CurrentModpack.ProjectId > 0)
            {
                CurrentModpack.CurrentVersion = new ModpackVersion { FileId = manifestInfo.FileId.ToString(), Name = "Installed" };
                SaveModpacks();
            }
            
            LaunchStatus = "Spouštím hru...";

            // Get Minecraft version from manifest (dynamically)
            var mcVersion = _lastManifestInfo?.MinecraftVersion ?? "1.21.1";
            var modLoaderId = _lastManifestInfo?.ModLoaderId ?? "";

            // DETECT JAVA VERSION FOR DYNAMIC FLAGS
            LaunchStatus = "Detekuji Javu...";
            
            int? requiredJava = null;
            // Removed hard force for Java 17 to allow the GTNH integrated installer to run on Java 8 if needed.
            // Adoptium 8 is recommended if Java 17 fails to bootstrap the skeleton pack.

            var javaPath = await _launcherService.GetJavaPathAsync(_lastManifestInfo?.MinecraftVersion ?? mcVersion, requiredJava);
            int javaVersion = _launcherService.GetJavaVersion(javaPath ?? "");
            Debug.WriteLine($"[JavaDetection] Detected Java {javaVersion} at {javaPath}");
            
            var jvmArgs = new List<string>();


            // Check for instance-specific overrides or global config
            bool enableOptimizations = Config.InstanceOverrides.TryGetValue(CurrentModpack.Name, out var overrideConfig) 
                ? (overrideConfig.OverrideEnableOptimizationFlags ?? Config.EnableOptimizationFlags)
                : Config.EnableOptimizationFlags;
            
            if (enableOptimizations)
            {
                // General Optimizations (Safe for all)
                jvmArgs.Add("-XX:+UnlockExperimentalVMOptions");
                jvmArgs.Add("-XX:+DisableExplicitGC");
                jvmArgs.Add("-XX:+AlwaysPreTouch");
                jvmArgs.Add("-XX:+PerfDisableSharedMem");
                jvmArgs.Add("-XX:+UseNUMA"); // Added as per request

                // Determine GC Type (Override > Global)
                var effectiveGc = overrideConfig?.OverrideGcType ?? Config.SelectedGc;

                if (effectiveGc == Models.GcType.ZGC)
                {
                    // Experimental High-Performance ZGC
                    jvmArgs.Add("-XX:+UseZGC");
                    
                    // Generational ZGC (Java 21+)
                    if (javaVersion >= 21)
                    {
                        jvmArgs.Add("-XX:+ZGenerational");
                    }
                }
                else if (effectiveGc == Models.GcType.G1GC)
                {
                    // Default: BruceTheMoose Universal G1GC (Stable)
                    jvmArgs.Add("-XX:+UseG1GC");
                    jvmArgs.Add("-XX:+ParallelRefProcEnabled");
                    jvmArgs.Add("-XX:MaxGCPauseMillis=200");
                    jvmArgs.Add("-XX:G1NewSizePercent=30");
                    jvmArgs.Add("-XX:G1MaxNewSizePercent=40");
                    jvmArgs.Add("-XX:G1HeapRegionSize=8M");
                    jvmArgs.Add("-XX:G1ReservePercent=20");
                    jvmArgs.Add("-XX:G1HeapWastePercent=5");
                    jvmArgs.Add("-XX:G1MixedGCCountTarget=4");
                    jvmArgs.Add("-XX:InitiatingHeapOccupancyPercent=15");
                    jvmArgs.Add("-XX:G1MixedGCLiveThresholdPercent=90");
                    jvmArgs.Add("-XX:G1RSetUpdatingPauseTimePercent=5");
                    jvmArgs.Add("-XX:SurvivorRatio=32");
                    jvmArgs.Add("-XX:MaxTenuringThreshold=1");
                }
                // If None, do not add any GC flags (Manual Control)
            }

            // --- GTNH Detection: auto-add --add-opens JVM flags and fix 403 errors ---
            if (IsGTNHModpack(CurrentModpack.Name, modpackDir))
            {
                // GLOBAL USER-AGENT FIX: Fixes 403 errors in the internal GTNH mod downloader (txloader)
                jvmArgs.Add("-Dhttp.agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                jvmArgs.Add("-Dfile.encoding=UTF-8");

                // Only add --add-opens if we are using Java 9+ (to avoid error with Java 8)
                if (javaVersion >= 9)
                {
                    LaunchStatus = $"Detekován GTNH s Java {javaVersion} – aplikuji kompatibilní argumenty...";
                    jvmArgs.AddRange(new[] {
                        "--add-opens", "java.base/jdk.internal.loader=ALL-UNNAMED",
                        "--add-opens", "java.base/java.net=ALL-UNNAMED",
                        "--add-opens", "java.base/java.nio=ALL-UNNAMED",
                        "--add-opens", "java.base/java.io=ALL-UNNAMED",
                        "--add-opens", "java.base/java.lang=ALL-UNNAMED",
                        "--add-opens", "java.base/java.lang.reflect=ALL-UNNAMED",
                        "--add-opens", "java.base/java.util=ALL-UNNAMED",
                        "--add-opens", "java.base/java.util.concurrent=ALL-UNNAMED",
                        "--add-opens", "java.base/sun.nio.ch=ALL-UNNAMED",
                        "--add-opens", "java.base/sun.security.ssl=ALL-UNNAMED",
                        "--add-opens", "java.base/sun.security.util=ALL-UNNAMED",
                        "--add-opens", "java.base/sun.net.www.protocol.jar=ALL-UNNAMED",
                        "--add-opens", "java.desktop/sun.awt=ALL-UNNAMED",
                        "--add-opens", "java.desktop/sun.font=ALL-UNNAMED",
                        "--add-opens", "java.desktop/sun.java2d=ALL-UNNAMED",
                        "--add-opens", "jdk.naming.dns/com.sun.jndi.dns=ALL-UNNAMED",
                        "-Djava.security.manager=allow"
                    });
                }
            }

            // Append per-instance custom JVM arguments if any
            if (overrideConfig?.CustomJvmArguments != null && overrideConfig.CustomJvmArguments.Length > 0)
            {
                jvmArgs.AddRange(overrideConfig.CustomJvmArguments);
            }

            // Append user custom arguments from global config if any
            if (Config.JvmArguments != null && Config.JvmArguments.Length > 0)
            {
                jvmArgs.AddRange(Config.JvmArguments);
            }

            // ---------------------------------------------------------
            // APPLY POTATO MODE (Renaming files)
            // ---------------------------------------------------------
            bool potatoMode = overrideConfig?.PotatoModeEnabled ?? false;
            LaunchStatus = potatoMode ? "Aplikuji Potato Mode (vypínám mody)..." : "Kontrola Potato Mode...";
            
            try
            {
               ModUtils.ApplyPotatoMode(modsDir, modpackDir, potatoMode);
            }
            catch (Exception ex)
            {
                // If file lock occurs, we should probably stop launch or warn?
                // For now log and continue, unless critical?
                LogService.Error("Failed to apply Potato Mode", ex);
                // Ideally show dialog here if critical
            }
            
            var gameProcess = await _launcherService.LaunchAsync(
                mcVersion,
                UserSession,
                Config,
                modpackDir,
                modLoaderId,
                jvmArgs.ToArray(),
                requiredJava,
                (status) => Avalonia.Threading.Dispatcher.UIThread.Post(() => LaunchStatus = status),
                (percent) => Avalonia.Threading.Dispatcher.UIThread.Post(() => LaunchProgress = percent),
                (file) => Avalonia.Threading.Dispatcher.UIThread.Post(() => LaunchStatus = $"Stahuji: {file}")
            );
            
            // Redirect output for debugging
            gameProcess.StartInfo.RedirectStandardOutput = true;
            gameProcess.StartInfo.RedirectStandardError = true;
            
            // Log file for troubleshooting
            // Log file for troubleshooting via LogService
            LogService.Log($"--- GAME START ---", "GAME");
            LogService.Log($"Java Args: {string.Join(" ", jvmArgs)}", "GAME");

            void LogTo(string prefix, string data)
            {
                if (string.IsNullOrEmpty(data)) return;
                LogService.Log(data, $"G-{prefix}");
            }

            gameProcess.OutputDataReceived += (s, e) => LogTo("STDOUT", e.Data);
            gameProcess.ErrorDataReceived += (s, e) => LogTo("STDERR", e.Data);
            
            gameProcess.Start();
            gameProcess.BeginOutputReadLine();
            gameProcess.BeginErrorReadLine();

            IsLaunching = false;
            IsGameRunning = true;
            RunningModpack = TargetModpack ?? CurrentModpack;
            _discordRpcService.SetPlayingState(RunningModpack?.Name ?? "Minecraft");
            LaunchProgress = 100;
            TargetModpack = null; // Clear so progress bar disappears from card after launch success

            // Minimize to tray while game is running
            App.MinimizeToTray();
            
            // Wait for game to exit in background
            _ = Task.Run(async () => 
            {
                await gameProcess.WaitForExitAsync();
                var exitCode = gameProcess.ExitCode;
                LogTo("LAUNCHER", $"Game exited with code {exitCode}");
                
                Avalonia.Threading.Dispatcher.UIThread.Post(() => 
                {
                    IsGameRunning = false;
                    RunningModpack = null;
                    Greeting = $"Minecraft ukončen (ExitCode: {exitCode}). Viz game_log.txt";

                    // Restore window from tray
                    App.RestoreMainWindow();
                });
            });
        }
        catch (Exception ex)
        {
            LaunchStatus = $"Chyba: {ex.Message}";
            await Task.Delay(3000);
            IsLaunching = false;
            TargetModpack = null;
            RunningModpack = null;
        }
    }

    [RelayCommand]
    public void StopGame()
    {
        _launcherService.StopGame();
        IsGameRunning = false;
        RunningModpack = null;
        Greeting = "Hra ukončena.";
        UpdateDiscordPresence();
    }

    [RelayCommand]
    public void ManageMods()
    {
        if (CurrentModpack != null)
        {
            var modpackPath = _launcherService.GetModpackPath(CurrentModpack.Name);
            var vm = new ModManagerViewModel(CurrentModpack.Name, modpackPath);
            var window = new VoidCraftLauncher.Views.ModManagerWindow { DataContext = vm };

            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                window.ShowDialog(desktop.MainWindow);
            }
        }
    }

    [RelayCommand]
    public void OpenModpackFolder()
    {
        if (CurrentModpack != null)
        {
            var path = _launcherService.GetModpackPath(CurrentModpack.Name);
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
                Verb = "open"
            });
        }
    }

    // Context menu versions that accept modpack parameter
    [RelayCommand]
    public void OpenModpackFolderContext(ModpackInfo modpack)
    {
        if (modpack != null)
        {
            var path = _launcherService.GetModpackPath(modpack.Name);
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
                Verb = "open"
            });
        }
    }

    [RelayCommand]
    public void ManageModsContext(ModpackInfo modpack)
    {
        if (modpack != null)
        {
            var modpackPath = _launcherService.GetModpackPath(modpack.Name);
            var vm = new ModManagerViewModel(modpack.Name, modpackPath);
            var window = new VoidCraftLauncher.Views.ModManagerWindow { DataContext = vm };

            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                window.ShowDialog(desktop.MainWindow);
            }
        }
    }

    [RelayCommand]
    public async Task DeleteModpackContext(ModpackInfo modpack)
    {
        if (modpack == null) return;
        if (!modpack.IsDeletable) 
        {
            Greeting = "Tento modpack nelze odstranit.";
            return;
        }
        
        var modpackName = modpack.Name;
        var modpackPath = _launcherService.GetModpackPath(modpackName);
        
        // Remove from library
        InstalledModpacks.Remove(modpack);
        
        // Delete files if directory exists
        if (Directory.Exists(modpackPath))
        {
            try
            {
                Directory.Delete(modpackPath, recursive: true);
                Greeting = $"Modpack {modpackName} byl smazán.";
            }
            catch (Exception ex)
            {
                Greeting = $"Chyba při mazání: {ex.Message}";
            }
        }
        else
        {
            Greeting = $"Modpack {modpackName} odebrán z knihovny.";
        }
        
        // Update current modpack
        if (CurrentModpack == modpack)
        {
            CurrentModpack = InstalledModpacks.FirstOrDefault();
        }
        
        SaveModpacks(); // Save changes
    }

    [RelayCommand]
    public void OpenUrl(string url)
    {
        if (!string.IsNullOrWhiteSpace(url))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                // Log error
                System.Diagnostics.Debug.WriteLine($"Error opening URL: {ex}");
            }
        }
    }

    [RelayCommand]
    public void OpenScreenshot(string screenshotUri)
    {
        if (string.IsNullOrWhiteSpace(screenshotUri))
        {
            return;
        }

        try
        {
            var filePath = screenshotUri;
            if (Uri.TryCreate(screenshotUri, UriKind.Absolute, out var parsedUri) && parsedUri.IsFile)
            {
                filePath = parsedUri.LocalPath;
            }

            if (!File.Exists(filePath))
            {
                Greeting = "Screenshot nebyl nalezen.";
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            LogService.Error("[OpenScreenshot] Failed to open screenshot", ex);
            Greeting = "Nepodařilo se otevřít screenshot.";
        }
    }

    [RelayCommand]
    public async Task CopyIp()
    {
        try
        {
            var topLevel = Avalonia.Controls.TopLevel.GetTopLevel(Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null);
            if (topLevel?.Clipboard != null)
            {
                await topLevel.Clipboard.SetTextAsync("mc.void-craft.eu");
                Greeting = "IP adresa zkopírována!";
                
                // Reset greeting after 2 seconds
                _ = Task.Delay(2000).ContinueWith(_ => 
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => Greeting = "Vítejte ve VoidCraft Launcheru!");
                });
            }
        }
        catch
        {
            // Ignore clipboard errors
        }
    }

    [RelayCommand]
    public void OpenLoginModal()
    {
        // Allow opening login modal even when logged in (to add additional accounts)
        IsLoginModalVisible = true;
        LoginStatus = "";
        ManualLoginCode = "";
        AuthUrl = "";
        IsLoginInProgress = false;
        // Pre-fill "Player" if empty and nothing in config
        if (string.IsNullOrEmpty(OfflineUsername)) OfflineUsername = Config.LastOfflineUsername ?? "Player";
    }

    [RelayCommand]
    public void CloseLoginModal()
    {
        IsLoginModalVisible = false;
    }

    [RelayCommand]
    public async Task LoginMicrosoft()
    {
        if (IsLoginInProgress)
            return;

        try
        {
            IsLoginInProgress = true;
            IsLoginModalVisible = true;
            IsWebviewVisible = true;
            IsBrowserPanelVisible = false;
            LoginStatus = "Otevírám přihlášení...";
            ManualLoginCode = "";
            AuthUrl = "";
            
            var session = await _authService.LoginWithBrowserAsync((msg) =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    Greeting = msg;
                    LoginStatus = msg;

                    var urlMatch = Regex.Match(msg, @"https?://\S+", RegexOptions.IgnoreCase);
                    if (urlMatch.Success)
                    {
                        AuthUrl = urlMatch.Value.Trim().TrimEnd('.', ',', ';');
                    }

                    var codeMatch = Regex.Match(msg, @"(?:kód|code):\s*([A-Z0-9\-]+)", RegexOptions.IgnoreCase);
                    if (codeMatch.Success)
                    {
                        ManualLoginCode = codeMatch.Groups[1].Value.Trim();
                    }
                });
            });
            
            UserSession = session;
            IsLoggedIn = true;
            IsLoginInProgress = false;
            IsLoginModalVisible = false;
            IsWebviewVisible = false;
            OnPropertyChanged(nameof(PlayerSkinUrl));
            Greeting = $"Vítejte, {session.Username}!";

            // Add to multi-account profiles
            var msalId = await _authService.GetLastMsalAccountIdAsync();
            var existingProfile = Accounts.FirstOrDefault(a => a.Type == AccountType.Microsoft && a.MsalAccountId == msalId);
            if (existingProfile != null)
            {
                existingProfile.DisplayName = session.Username;
                existingProfile.Uuid = session.UUID;
                existingProfile.LastUsed = DateTime.UtcNow;
                ActiveAccount = existingProfile;
            }
            else
            {
                var newProfile = new AccountProfile
                {
                    DisplayName = session.Username,
                    Uuid = session.UUID,
                    Type = AccountType.Microsoft,
                    MsalAccountId = msalId,
                    LastUsed = DateTime.UtcNow
                };
                Accounts.Add(newProfile);
                ActiveAccount = newProfile;
            }
            SaveAccountProfiles();
        }
        catch (System.Exception ex)
        {
            IsLoginInProgress = false;
            Greeting = $"Chyba přihlášení: {ex.Message}";
            LoginStatus = Greeting;
            IsWebviewVisible = false;
        }
    }

    [RelayCommand]
    public async Task CopyLoginCode()
    {
        if (string.IsNullOrWhiteSpace(ManualLoginCode))
            return;

        try
        {
            var topLevel = Avalonia.Controls.TopLevel.GetTopLevel(
                Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                    ? desktop.MainWindow
                    : null);

            if (topLevel?.Clipboard != null)
            {
                await topLevel.Clipboard.SetTextAsync(ManualLoginCode);
                Greeting = "Přihlašovací kód zkopírován.";
            }
        }
        catch
        {
        }
    }

    [RelayCommand]
    public void LoginOffline()
    {
        if (string.IsNullOrWhiteSpace(OfflineUsername))
        {
            Greeting = "Zadej prosím herní jméno.";
            return;
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(OfflineUsername, "^[a-zA-Z0-9_]{3,16}$"))
        {
            Greeting = "Neplatné jméno (3-16 znaků, a-z, 0-9, _).";
            return;
        }

        try
        {
            _launcherService.StopGame(); // Just in case
            
            UserSession = _authService.LoginOffline(OfflineUsername);
            IsLoggedIn = true;
            IsLoginModalVisible = false;
            OnPropertyChanged(nameof(PlayerSkinUrl));
            // Save username
            Config.LastOfflineUsername = OfflineUsername;

            // Add to multi-account profiles
            var existingProfile = Accounts.FirstOrDefault(a => a.Type == AccountType.Offline && a.DisplayName == OfflineUsername);
            if (existingProfile != null)
            {
                existingProfile.LastUsed = DateTime.UtcNow;
                ActiveAccount = existingProfile;
            }
            else
            {
                var newProfile = new AccountProfile
                {
                    DisplayName = OfflineUsername,
                    Type = AccountType.Offline,
                    LastUsed = DateTime.UtcNow
                };
                Accounts.Add(newProfile);
                ActiveAccount = newProfile;
            }
            SaveAccountProfiles();

            Greeting = $"Vítejte, {UserSession.Username} (Offline)!";
        }
        catch (Exception ex)
        {
            Greeting = $"Chyba přihlášení: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task Logout()
    {
        try 
        {
            // Remove active account's MSAL tokens if MS
            if (ActiveAccount?.Type == AccountType.Microsoft && !string.IsNullOrEmpty(ActiveAccount.MsalAccountId))
            {
                await _authService.RemoveAccountAsync(ActiveAccount.MsalAccountId);
            }
            
            // Clear persistent session
            Config.LastOfflineUsername = null;
            ActiveAccount = null;
            Config.ActiveAccountId = null;
            _launcherService.SaveConfig(Config);

            UserSession = MSession.CreateOfflineSession("Guest");
            IsLoggedIn = false;
            Greeting = "Byli jste odhlášeni.";
            OnPropertyChanged(nameof(PlayerSkinUrl));
        }
        catch (Exception ex)
        {
            Greeting = $"Chyba při odhlašování: {ex.Message}";
        }
    }

    // ===== MULTI-ACCOUNT COMMANDS =====

    [RelayCommand]
    public async Task SwitchAccount(AccountProfile profile)
    {
        if (profile == null) return;

        try
        {
            MSession? session = null;

            if (profile.Type == AccountType.Microsoft)
            {
                Greeting = $"Přepínám na {profile.DisplayName}...";
                session = await _authService.TrySilentLoginForAccountAsync(profile.MsalAccountId!);
                if (session == null)
                {
                    Greeting = $"Token pro {profile.DisplayName} expiroval. Přihlas se znovu.";
                    return;
                }
            }
            else
            {
                session = _authService.LoginOffline(profile.DisplayName);
            }

            UserSession = session;
            IsLoggedIn = true;
            ActiveAccount = profile;
            profile.LastUsed = DateTime.UtcNow;
            IsAccountPickerOpen = false;
            OnPropertyChanged(nameof(PlayerSkinUrl));
            SaveAccountProfiles();

            var suffix = profile.Type == AccountType.Offline ? " (Offline)" : "";
            Greeting = $"Přepnuto na {session.Username}{suffix}!";
        }
        catch (Exception ex)
        {
            Greeting = $"Chyba přepnutí: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task RemoveAccount(AccountProfile profile)
    {
        if (profile == null) return;

        // Remove from MSAL cache if MS account
        if (profile.Type == AccountType.Microsoft && !string.IsNullOrEmpty(profile.MsalAccountId))
        {
            await _authService.RemoveAccountAsync(profile.MsalAccountId);
        }

        Accounts.Remove(profile);

        // If removed the active account, switch to first available or logout
        if (ActiveAccount?.Id == profile.Id)
        {
            var nextAccount = Accounts.FirstOrDefault();
            if (nextAccount != null)
            {
                await SwitchAccount(nextAccount);
            }
            else
            {
                ActiveAccount = null;
                UserSession = MSession.CreateOfflineSession("Guest");
                IsLoggedIn = false;
                OnPropertyChanged(nameof(PlayerSkinUrl));
                Greeting = "Všechny účty odebrány.";
            }
        }

        SaveAccountProfiles();
    }

    [RelayCommand]
    public void ToggleAccountPicker()
    {
        IsAccountPickerOpen = !IsAccountPickerOpen;
    }

    private void SaveAccountProfiles()
    {
        Config.Accounts = new List<AccountProfile>(Accounts);
        Config.ActiveAccountId = ActiveAccount?.Id;
        _launcherService.SaveConfig(Config);
    }

    // ===== GTNH DETECTION =====

    private static bool IsGTNHModpack(string name, string modpackDir)
    {
        // Strictly check for GTNH name or specific marker
        if (name.Contains("GT New Horizons", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("GTNH", StringComparison.OrdinalIgnoreCase))
            return true;

        // Also check for .gtnh marker file or specific GTNH core mod
        var modsDir = Path.Combine(modpackDir, "mods");
        if (Directory.Exists(modsDir))
        {
            return File.Exists(Path.Combine(modpackDir, ".gtnh")) || 
                   Directory.GetFiles(modsDir, "GTNewHorizonsCoreMod*", SearchOption.TopDirectoryOnly).Length > 0;
        }

        return false;
    }


    [RelayCommand]
    public async Task DeleteModpack()
    {
        if (CurrentModpack == null) return;
        
        var modpackName = CurrentModpack.Name;
        var modpackPath = _launcherService.GetModpackPath(modpackName);
        
        // Remove from library
        InstalledModpacks.Remove(CurrentModpack);
        
        // Delete files if directory exists
        if (Directory.Exists(modpackPath))
        {
            try
            {
                Directory.Delete(modpackPath, recursive: true);
                Greeting = $"Modpack {modpackName} byl smazán.";
            }
            catch (Exception ex)
            {
                Greeting = $"Chyba při mazání: {ex.Message}";
            }
        }
        else
        {
            Greeting = $"Modpack {modpackName} odebrán z knihovny.";
        }
        
        // Clear current modpack
        CurrentModpack = InstalledModpacks.FirstOrDefault();
        GoToHome();
    }

    [RelayCommand]
    public void SaveSettings()
    {
        _launcherService.SaveConfig(Config);
        Greeting = "Nastavení uloženo.";
        GoToHome(); 
        // Reset greeting after 2s
        _ = Task.Delay(2000).ContinueWith(_ => 
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => Greeting = "Vítejte ve VOID-CRAFT Launcheru");
        });
    }

    [RelayCommand]
    public void GoToBrowser()
    {
        CurrentMainView = MainViewType.Discover;
        if (BrowserResults.Count == 0 && !IsSearching)
        {
            SearchModpacksCommand.Execute(null);
        }
    }
    [RelayCommand]
    public void GoToSettings()
    {
        CurrentMainView = MainViewType.Settings;
    }

    [RelayCommand]
    public void GoToHome()
    {
        CurrentMainView = MainViewType.Library;
    }

    [ObservableProperty]
    private InstanceConfig _currentModpackConfig;

    [ObservableProperty]
    private ObservableCollection<string> _optionsPresetNames = new();

    [ObservableProperty]
    private string _newOptionsPresetName = "";

    [ObservableProperty]
    private string? _selectedOptionsPresetName;

    [RelayCommand]
    public void GoToModpackSettings()
    {
        if (CurrentModpack == null) return;

        // Try to find existing override or create default (without adding it yet)
        if (Config.InstanceOverrides.TryGetValue(CurrentModpack.Name, out var existingConfig))
        {
            CurrentModpackConfig = existingConfig;
        }
        else
        {
            CurrentModpackConfig = new InstanceConfig 
            { 
                ModpackName = CurrentModpack.Name,
                IsEnabled = true
            };
        }

        ReloadOptionsPresets();
        CurrentMainView = MainViewType.InstanceDetail;
    }

    private void ReloadOptionsPresets()
    {
        var names = Config.OptionsPresets?.Keys
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new List<string>();

        OptionsPresetNames = new ObservableCollection<string>(names);

        if (names.Count == 0)
        {
            SelectedOptionsPresetName = null;
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedOptionsPresetName) || !names.Contains(SelectedOptionsPresetName))
        {
            SelectedOptionsPresetName = names[0];
        }
    }

    [RelayCommand]
    public void SaveOptionsPreset()
    {
        if (CurrentModpack == null)
        {
            Greeting = "Nejdříve vyber modpack.";
            return;
        }

        var presetName = (NewOptionsPresetName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(presetName))
        {
            Greeting = "Zadej název presetu options.";
            return;
        }

        try
        {
            var modpackPath = _launcherService.GetModpackPath(CurrentModpack.Name);
            var optionsPath = Path.Combine(modpackPath, "options.txt");

            if (!File.Exists(optionsPath))
            {
                Greeting = "V modpacku nebyl nalezen options.txt.";
                return;
            }

            var content = File.ReadAllText(optionsPath);
            Config.OptionsPresets[presetName] = content;
            _launcherService.SaveConfig(Config);

            NewOptionsPresetName = "";
            ReloadOptionsPresets();
            SelectedOptionsPresetName = presetName;
            Greeting = $"Preset '{presetName}' uložen.";
        }
        catch (Exception ex)
        {
            LogService.Error("[SaveOptionsPreset] Failed", ex);
            Greeting = "Nepodařilo se uložit preset options.";
        }
    }

    [RelayCommand]
    public void LoadOptionsPresetToCurrentModpack()
    {
        if (CurrentModpack == null)
        {
            Greeting = "Nejdříve vyber modpack.";
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedOptionsPresetName))
        {
            Greeting = "Vyber preset options pro načtení.";
            return;
        }

        if (!Config.OptionsPresets.TryGetValue(SelectedOptionsPresetName, out var content))
        {
            Greeting = "Vybraný preset už neexistuje.";
            ReloadOptionsPresets();
            return;
        }

        try
        {
            var modpackPath = _launcherService.GetModpackPath(CurrentModpack.Name);
            Directory.CreateDirectory(modpackPath);
            var optionsPath = Path.Combine(modpackPath, "options.txt");
            File.WriteAllText(optionsPath, content);

            Greeting = $"Preset '{SelectedOptionsPresetName}' načten do modpacku {CurrentModpack.Name}.";
        }
        catch (Exception ex)
        {
            LogService.Error("[LoadOptionsPresetToCurrentModpack] Failed", ex);
            Greeting = "Nepodařilo se načíst preset options do modpacku.";
        }
    }

    [RelayCommand]
    public void DeleteOptionsPreset()
    {
        if (string.IsNullOrWhiteSpace(SelectedOptionsPresetName))
        {
            Greeting = "Vyber preset, který chceš smazat.";
            return;
        }

        var presetName = SelectedOptionsPresetName;
        if (!Config.OptionsPresets.Remove(presetName))
        {
            Greeting = "Preset nebyl nalezen.";
            ReloadOptionsPresets();
            return;
        }

        try
        {
            _launcherService.SaveConfig(Config);
            ReloadOptionsPresets();
            Greeting = $"Preset '{presetName}' byl smazán.";
        }
        catch (Exception ex)
        {
            LogService.Error("[DeleteOptionsPreset] Failed", ex);
            Greeting = "Nepodařilo se smazat preset.";
        }
    }

    [RelayCommand]
    public void SaveModpackSettings()
    {
        if (CurrentModpackConfig == null) return;

        // Add or update the override in the dictionary
        Config.InstanceOverrides[CurrentModpackConfig.ModpackName] = CurrentModpackConfig;

        // Persist to disk
        _launcherService.SaveConfig(Config);
        Greeting = $"Nastavení pro {CurrentModpackConfig.ModpackName} uloženo.";
    }

    [RelayCommand]
    public void OpenPotatoConfig()
    {
        if (CurrentModpack == null) return;
        
        var modpackDir = _launcherService.GetModpackPath(CurrentModpack.Name);
        // Ensure config exists
        ModUtils.GetPotatoModList(modpackDir);
        
        // Open UI
        var vm = new PotatoModsViewModel(modpackDir);
        var window = new VoidCraftLauncher.Views.PotatoModsWindow { DataContext = vm };
        
        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            window.ShowDialog(desktop.MainWindow);
        }
    }

    // --- Modpack Browser Logic ---

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

    [RelayCommand]
    public async Task SetBrowserSource(string source)
    {
        BrowserSource = source;
        IsSearching = false; 
        BrowserResults.Clear(); // Immediate clear for feedback
        await SearchModpacks();
    }

    [RelayCommand]
    public void OpenBrowser(string source)
    {
        BrowserSource = source;
        BrowserSearchQuery = "";
        BrowserResults.Clear();
        CurrentMainView = MainViewType.Discover;
        
        // Populate with popular modpacks initially
        SearchModpacksCommand.Execute(null);
    }

    [RelayCommand]
    public async Task SearchModpacks()
    {
        if (IsSearching) return;
        IsSearching = true;
        BrowserResults.Clear();
        CurrentBrowserPage = 0; // Reset na prvni stranku

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
            CurrentBrowserPage--; // Vrátíme zpět při chybě
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
            json = await _curseForgeApi.SearchModpacksAsync("", offset); // Popular
        else
            json = await _curseForgeApi.SearchModpacksAsync(BrowserSearchQuery, offset);

         var root = JsonNode.Parse(json);
         var data = root?["data"]?.AsArray();

         if (data != null)
         {
             HasMoreResults = data.Count == 50; // Předpoklad pro pagination CF

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
        string json = await _modrinthApi.SearchModpacksAsync(BrowserSearchQuery, offset); // Modrinth handles empty query as generic search

        var root = JsonNode.Parse(json);
        var hits = root?["hits"]?.AsArray();
        
        // Modrinth vrací i property total_hits
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
    public void OpenDashboard(ModpackInfo modpack)
    {
        CurrentModpack = modpack;
        // CurrentRightView = RightViewType.Dashboard; // Removed dashboard nav req
    }

    [RelayCommand]
    public async Task SelectAndPlay(ModpackInfo modpack)
    {
        CurrentModpack = modpack;
        TargetModpack = modpack; // Set target immediately for progress bar
        await PlayModpack();
    }

    [RelayCommand]
    public void SelectAndConfigure(ModpackInfo modpack)
    {
        CurrentModpack = modpack;
        GoToModpackSettings(); // This properly initializes CurrentModpackConfig
        LoadInstalledMods(); // Pre-load installed mods for Custom Profiles
    }

    [RelayCommand]
    public async Task InstallModpackFromBrowser(ModpackItem item)
    {
        if (IsSearching || IsLaunching) return;
        
        // 1. Immediate UI switch
        IsSearching = false; // Close browser logic if needed, but we switch view
        
        // Create temporary ModpackInfo so it shows up immediately
        var newModpack = new ModpackInfo
        {
            Name = item.Name,
            LogoUrl = item.IconUrl,
            Description = item.Description,
            Author = item.Author,
            WebLink = item.WebLink,
            Source = item.Source,
            ModrinthId = item.Source == "Modrinth" ? item.Id : "",
            ProjectId = item.Source == "CurseForge" ? (int.TryParse(item.Id, out var id) ? id : 0) : 0
        };
        
                CurrentModpack = newModpack;
                
                // Add to Library
                Avalonia.Threading.Dispatcher.UIThread.Post(() => InstalledModpacks.Add(CurrentModpack));
                
                // Switch to Library (Home) so user sees it in grid
                GoToHome(); 
                
                // 2. Start Installation Task
        // Run in background but linked to UI
        Task.Run(async () => 
        {
            try
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => 
                {
                    IsLaunching = true; // Show Progress Bar
                    LaunchStatus = $"Připravuji instalaci {item.Name}...";
                    LaunchProgress = 0;
                });

                string downloadUrl = "";
                string fileName = "modpack.zip";
                string versionId = "0";

                // 2.1 Get Download URL
                using (var httpClient = new HttpClient())
                {
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
                    }

                    if (string.IsNullOrEmpty(downloadUrl)) throw new Exception("Chybí URL.");

                    // 2.2 Download
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => LaunchStatus = "Stahuji balíček...");
                    var tempPath = Path.Combine(Path.GetTempPath(), fileName);
                    
                    // Simple progress tracking for download?
                    // reusing existing _httpClient might be better but for now simple
                    var fileBytes = await httpClient.GetByteArrayAsync(downloadUrl);
                    await File.WriteAllBytesAsync(tempPath, fileBytes);
                    
                    // 2.3 Install
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
                        manifestInfo = await _modpackInstaller.InstallOrUpdateAsync(tempPath, installPath);
                    }
                    catch (Exception ex)
                    {
                         Avalonia.Threading.Dispatcher.UIThread.Post(() => Greeting = $"Chyba instalace: {ex.Message}");
                         Avalonia.Threading.Dispatcher.UIThread.Post(() => Greeting = $"Chyba instalace: {ex.Message}");
                         LogService.Error("Install Modpack Error", ex);
                         
                         // Cleanup failed install
                         try { Directory.Delete(installPath, true); } catch {}
                         
                         return;
                    }
                    finally
                    {
                        _modpackInstaller.StatusChanged -= OnStatus;
                        _modpackInstaller.ProgressChanged -= OnProgress;
                        if (File.Exists(tempPath)) File.Delete(tempPath);
                    }

                    // 2.4 Update Modpack Info with real details
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => 
                    {
                        var versionInfo = new ModpackVersion 
                        { 
                            Name = item.Source == "CurseForge" ? "Latest" : (versionId ?? "1.0"), 
                            FileId = versionId ?? "0"
                        };
                        
                        // Update the object in place if possible, or create new
                        CurrentModpack = new ModpackInfo
                        {
                            Name = safeName, // Match folder name
                            ProjectId = item.Source == "CurseForge" ? int.Parse(item.Id) : 0,
                            Source = item.Source,
                            ModrinthId = item.Source == "Modrinth" ? item.Id : "",
                            LogoUrl = item.IconUrl,
                            Author = item.Author,
                            WebLink = item.WebLink,
                            Description = item.Description,
                            CurrentVersion = versionInfo
                        };

                        // Add to library and save
                        // Check for existing modpack by ProjectId or Name
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

    private void SaveModpacks()
    {
        try
        {
            // Save to src/installed_modpacks.json or BasePath? LauncherService.BasePath is better.
            var path = Path.Combine(_launcherService.BasePath, "installed_modpacks.json");
            var options = new JsonSerializerOptions { WriteIndented = true };
            
            // Filter out duplicate or invalid entries if needed
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
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        foreach (var modpack in list)
                        {
                            // Avoid duplicates based on Name or ProjectId
                            if (!InstalledModpacks.Any(m => m.Name == modpack.Name))
                            {
                                InstalledModpacks.Add(modpack);
                            }
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


    // ===== CONTEXT MENU COMMANDS =====

    [RelayCommand]
    public async Task ReinstallModpackContext(ModpackInfo modpack)
    {
        if (modpack == null) return;

        var modpackPath = _launcherService.GetModpackPath(modpack.Name);
        
        try
        {
            Greeting = $"Přeinstalovávám {modpack.Name}...";
            IsLaunching = true;
            LaunchStatus = $"Mažu starý obsah {modpack.Name}...";
            LaunchProgress = 0;
            TargetModpack = modpack;
            
            // 1. Delete instance directory contents
            if (Directory.Exists(modpackPath))
            {
                Directory.Delete(modpackPath, true);
                Directory.CreateDirectory(modpackPath);
            }

            LaunchStatus = "Starý obsah smazán. Spouštím reinstalaci...";
            LaunchProgress = 10;

            // 2. Reset version info to force fresh install
            modpack.CurrentVersion = new ModpackVersion { Name = "-", FileId = "0" };

            // 3. Trigger PlayModpack which will detect missing files and reinstall
            IsLaunching = false;
            await PlayModpack();
        }
        catch (Exception ex)
        {
            Greeting = $"Chyba reinstalace: {ex.Message}";
            IsLaunching = false;
            TargetModpack = null;
        }
    }

    // ===== CUSTOM PROFILE =====

    [ObservableProperty]
    private bool _isCreateProfileModalVisible = false;

    [ObservableProperty]
    private string _newProfileName = "";

    [ObservableProperty]
    private string _newProfileMcVersion = "1.21.1";
    
    partial void OnNewProfileMcVersionChanged(string value)
    {
        _ = FetchModLoaderVersionsAsync();
    }

    [ObservableProperty]
    private string _newProfileModLoader = "fabric";
    
    partial void OnNewProfileModLoaderChanged(string value)
    {
        _ = FetchModLoaderVersionsAsync();
    }
    
    [ObservableProperty]
    private string _newProfileModLoaderVersion = "";

    [ObservableProperty]
    private ObservableCollection<string> _availableMcVersions = new();

    [ObservableProperty]
    private ObservableCollection<string> _availableModLoaderVersions = new();

    [RelayCommand]
    public void OpenCreateProfileModal()
    {
        NewProfileName = "";
        NewProfileMcVersion = "1.21.1";
        NewProfileModLoader = "fabric";
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
        IsCreateProfileModalVisible = false;
    }

    [RelayCommand]
    public void CreateCustomProfile()
    {
        if (string.IsNullOrWhiteSpace(NewProfileName))
        {
            Greeting = "Zadej název profilu!";
            return;
        }

        var sanitizedName = string.Join("_", NewProfileName.Split(Path.GetInvalidFileNameChars()));
        
        // We cannot use _launcherService.GetModpackPath because it auto-creates the directory
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var instancePath = Path.Combine(localAppData, "VoidCraftLauncher", "Instances", sanitizedName);

        if (Directory.Exists(instancePath) && Directory.GetFileSystemEntries(instancePath).Length > 0)
        {
            Greeting = "Profil s tímto názvem už existuje!";
            return;
        }

        Directory.CreateDirectory(instancePath);
        Directory.CreateDirectory(Path.Combine(instancePath, "mods"));

        var newProfile = new ModpackInfo
        {
            Name = sanitizedName,
            IsCustomProfile = true,
            CustomMcVersion = NewProfileMcVersion,
            CustomModLoader = NewProfileModLoader,
            // Include specific loader version if selected
            CustomModLoaderVersion = NewProfileModLoaderVersion 
        };

        InstalledModpacks.Add(newProfile);
        SaveModpacks();

        Greeting = $"Vlastní profil '{sanitizedName}' vytvořen.";
        CloseCreateProfileModal();
        
        CurrentModpack = newProfile;
        CurrentMainView = MainViewType.InstanceDetail;
    }
    
    // Fetching versions for Custom Profile creation
    private async Task FetchMcVersionsAsync()
    {
        try
        {
            using var client = new HttpClient();
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
                    foreach(var ver in releases.Take(50)) // Top 50 releases
                    {
                        AvailableMcVersions.Add(ver!);
                    }
                    if (!AvailableMcVersions.Contains(NewProfileMcVersion))
                        NewProfileMcVersion = AvailableMcVersions.FirstOrDefault() ?? "1.21.1";
                });
            }
        }
        catch (Exception ex)
        {
            LogService.Error("Failed to fetch MC versions", ex);
        }
    }

    private async Task FetchModLoaderVersionsAsync()
    {
        if (string.IsNullOrEmpty(NewProfileMcVersion) || string.IsNullOrEmpty(NewProfileModLoader)) return;
        
        var mcVer = NewProfileMcVersion;
        var loader = NewProfileModLoader.ToLower();
        
        try
        {
            using var client = new HttpClient();
            List<string> versions = new();
            
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
                    // NeoForge versions are usually of format "21.1.X" for MC 1.21.1
                    // Let's filter by exact prefix matching the short version + "." to avoid 1.21.1 matching 1.21.11
                    var shortMcVer = mcVer.StartsWith("1.") ? mcVer.Substring(2) : mcVer;
                    var precisePrefix = shortMcVer + ".";
                    
                    versions = allVersions.Select(x => x?.ToString())
                                          .Where(x => x != null && (x.StartsWith(precisePrefix) || x == shortMcVer))
                                          .Cast<string>()
                                          .Reverse() // newest first
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
                    // Usually "version-latest" and "version-recommended"
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
                    NewProfileModLoaderVersion = "Nenalezeny verze";
                }
            });
        }
        catch (Exception ex)
        {
            LogService.Error($"Failed to fetch {loader} versions for {mcVer}", ex);
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                AvailableModLoaderVersions.Clear();
                NewProfileModLoaderVersion = "";
            });
        }
    }    [ObservableProperty]
    private ObservableCollection<ModpackItem> _profileModSearchResults = new();

    [ObservableProperty]
    private ObservableCollection<ModpackItem> _installedMods = new();

    [ObservableProperty]
    private string _profileModSearchQuery = "";

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
                        // Basic heuristic: if any jar file contains the mod name (ignoring case/symbols)
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
                var filesJson = await _curseForgeApi.GetModFilesAsync(modId, CurrentModpack.CustomMcVersion);
                var files = JsonNode.Parse(filesJson)?["data"]?.AsArray();
                var latestFile = files?.FirstOrDefault();
                
                if (latestFile == null)
                {
                    Greeting = $"Žádný soubor pro {mod.Name} a MC {CurrentModpack.CustomMcVersion}.";
                    return;
                }

                var downloadUrl = latestFile["downloadUrl"]?.ToString();
                var fileName = latestFile["fileName"]?.ToString() ?? $"{mod.Name}.jar";
                
                if (string.IsNullOrEmpty(downloadUrl))
                {
                    // CurseForge sometimes hides direct download – try CDN pattern
                    var fileId = latestFile["id"]?.GetValue<int>() ?? 0;
                    downloadUrl = $"https://edge.forgecdn.net/files/{fileId / 1000}/{fileId % 1000}/{Uri.EscapeDataString(fileName)}";
                }

                var data = await _httpClient.GetByteArrayAsync(downloadUrl);
                await File.WriteAllBytesAsync(Path.Combine(modsDir, fileName), data);
                
                // Save metadata
                await SaveModMetadata(fileName, mod);
                
                mod.IsInstalled = true;
                mod.InstalledFileName = fileName;
                OnPropertyChanged(nameof(ProfileModSearchResults)); 
            }
            else // Modrinth
            {
                var versionsJson = await _modrinthApi.GetProjectVersionsAsync(mod.Id);
                var versions = JsonNode.Parse(versionsJson)?.AsArray();
                
                // Find version matching MC version and loader
                var matchingVersion = versions?.FirstOrDefault(v =>
                {
                    var gameVersions = v?["game_versions"]?.AsArray();
                    var loaders = v?["loaders"]?.AsArray();
                    bool mcMatch = gameVersions?.Any(gv => gv?.ToString() == CurrentModpack.CustomMcVersion) ?? false;
                    bool loaderMatch = loaders?.Any(l => l?.ToString()?.Equals(CurrentModpack.CustomModLoader, StringComparison.OrdinalIgnoreCase) ?? false) ?? false;
                    return mcMatch && loaderMatch;
                }) ?? versions?.FirstOrDefault();

                if (matchingVersion == null)
                {
                    Greeting = $"Žádná verze pro {mod.Name} a MC {CurrentModpack.CustomMcVersion}.";
                    return;
                }

                var fileObj = matchingVersion["files"]?.AsArray()?.FirstOrDefault(f => f?["primary"]?.GetValue<bool>() == true)
                              ?? matchingVersion["files"]?.AsArray()?.FirstOrDefault();
                
                var downloadUrl = fileObj?["url"]?.ToString();
                var fileName = fileObj?["filename"]?.ToString() ?? $"{mod.Name}.jar";

                if (string.IsNullOrEmpty(downloadUrl))
                {
                    Greeting = "Download URL nenalezena.";
                    return;
                }

                var data = await _httpClient.GetByteArrayAsync(downloadUrl);
                await File.WriteAllBytesAsync(Path.Combine(modsDir, fileName), data);

                // Save metadata
                await SaveModMetadata(fileName, mod);
                
                mod.IsInstalled = true;
                mod.InstalledFileName = fileName;
                OnPropertyChanged(nameof(ProfileModSearchResults)); 
            }

            Greeting = $"Mod {mod.Name} nainstalován do {CurrentModpack.Name}!";
            LoadInstalledMods(); 
        }
        catch (Exception ex)
        {
            Greeting = $"Chyba instalace modu: {ex.Message}";
            LogService.Error($"AddModToProfile failed for {mod.Name}", ex);
        }
    }

    private async Task SaveModMetadata(string fileName, ModpackItem mod)
    {
        try
        {
            var modpackPath = _launcherService.GetModpackPath(CurrentModpack.Name);
            var metaPath = Path.Combine(modpackPath, "mods_metadata.json");
            var metadata = new List<ModMetadata>();
            
            if (File.Exists(metaPath))
            {
                var existingJson = await File.ReadAllTextAsync(metaPath);
                metadata = JsonSerializer.Deserialize<List<ModMetadata>>(existingJson) ?? new();
            }

            // Remove existing entry for this filename
            metadata.RemoveAll(m => m.FileName == fileName);

            metadata.Add(new ModMetadata
            {
                FileName = fileName,
                Name = mod.Name,
                Slug = mod.Id,
                Summary = mod.Description,
                Categories = new List<string>(),
                IconUrl = mod.IconUrl,
                WebLink = mod.WebLink
            });

            var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(metaPath, json);
        }
        catch (Exception ex)
        {
            LogService.Error($"Failed to save manual mod metadata for {mod.Name}", ex);
        }
    }

    [RelayCommand]
    public void RemoveModFromProfile(ModpackItem mod)
    {
        if (CurrentModpack == null || mod == null || string.IsNullOrEmpty(mod.InstalledFileName)) return;

        var modsDir = Path.Combine(_launcherService.GetModpackPath(CurrentModpack.Name), "mods");
        var filePath = Path.Combine(modsDir, mod.InstalledFileName);
        
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            Greeting = $"Mod {mod.Name} odebrán.";
        }
        else
        {
            Greeting = $"Soubor {mod.InstalledFileName} nenalezen, ale odebírám ze seznamu.";
        }

        mod.IsInstalled = false;
        mod.InstalledFileName = "";
        
        LoadInstalledMods(); // Refresh the manual list
    }

    private void LoadInstalledMods()
    {
        InstalledMods.Clear();
        if (CurrentModpack == null) return;

        var modpackPath = _launcherService.GetModpackPath(CurrentModpack.Name);
        var modsDir = Path.Combine(modpackPath, "mods");
        
        // Load metadata if exists
        var metadata = new List<ModMetadata>();
        var metaPath = Path.Combine(modpackPath, "mods_metadata.json");
        if (File.Exists(metaPath))
        {
            try 
            { 
                var json = File.ReadAllText(metaPath);
                metadata = JsonSerializer.Deserialize<List<ModMetadata>>(json) ?? new(); 
            } 
            catch {}
        }

        if (Directory.Exists(modsDir))
        {
            var files = Directory.GetFiles(modsDir, "*.jar");
            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);
                var meta = metadata.FirstOrDefault(m => m.FileName == fileName);

                InstalledMods.Add(new ModpackItem
                {
                    Name = meta?.Name ?? Path.GetFileNameWithoutExtension(file),
                    InstalledFileName = fileName,
                    IsInstalled = true,
                    Source = meta != null ? "CurseForge" : "Stáhnuto z aplikace",
                    IconUrl = meta?.IconUrl ?? "",
                    WebLink = meta?.WebLink ?? ""
                });
            }
        }
    }
}
