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
    private ModpackManifestInfo _lastManifestInfo;

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
    private bool _isWebviewVisible = true;

    [ObservableProperty]
    private bool _isBrowserPanelVisible = false;

    [ObservableProperty]
    private bool _isLaunchIndeterminate = false;

    public enum RightViewType
    {
        Home,
        GlobalSettings,
        ModpackSettings,
        Browser,
        Dashboard
    }

    [ObservableProperty]
    private RightViewType _currentRightView = RightViewType.Home;

    public bool IsHomeView => CurrentRightView == RightViewType.Home;
    public bool IsSettingsView => CurrentRightView == RightViewType.GlobalSettings;
    public bool IsModpackSettingsView => CurrentRightView == RightViewType.ModpackSettings;
    public bool IsBrowserView => CurrentRightView == RightViewType.Browser;
    public bool IsDashboardView => CurrentRightView == RightViewType.Dashboard;

    partial void OnCurrentRightViewChanged(RightViewType value)
    {
        OnPropertyChanged(nameof(IsHomeView));
        OnPropertyChanged(nameof(IsSettingsView));
        OnPropertyChanged(nameof(IsModpackSettingsView));
        OnPropertyChanged(nameof(IsBrowserView));
        OnPropertyChanged(nameof(IsDashboardView));
        OnPropertyChanged(nameof(RightPanelTitle));
    }

    public string RightPanelTitle => CurrentRightView switch
    {
        RightViewType.GlobalSettings => "Globální Nastavení",
        RightViewType.ModpackSettings => "Nastavení Modpacku",
        RightViewType.Browser => $"Procházet Modpacky ({BrowserSource})",
        RightViewType.Home => "Moje Modpacky",
        RightViewType.Dashboard => CurrentModpack?.Name ?? "Přehled Modpacku",
        _ => "Přehled"
    };

    // Detect system RAM (in MB)
    public int SystemRamMb => (int)(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024 / 1024);

    [ObservableProperty]
    private bool _isLaunching = false;

    [ObservableProperty]
    private double _launchProgress = 0;

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
        
        // Forward installer events to UI
        _modpackInstaller.StatusChanged += (status) => LaunchStatus = status;
        _modpackInstaller.ProgressChanged += (progress) => 
        {
            LaunchProgress = progress * 100;
            IsLaunchIndeterminate = false;
        };

        _currentModpack = new ModpackInfo();
        InstalledModpacks = new ObservableCollection<ModpackInfo>();
        
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
        
        // One-time migration to force Auto Java (User Request)
        try 
        {
            var migrationFlag = Path.Combine(_launcherService.BasePath, "auto_java_migrated.flag");
            if (!File.Exists(migrationFlag) && _launcherService.BasePath != null)
            {
                Config.JavaPath = ""; // Force Auto
                _launcherService.SaveConfig(Config);
                File.Create(migrationFlag).Dispose();
                Debug.WriteLine("Migrated JavaPath to Auto.");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Migration failed: {ex.Message}");
        }

        // Restore offline username
        if (!string.IsNullOrEmpty(Config.LastOfflineUsername))
        {
            OfflineUsername = Config.LastOfflineUsername;
        }

        // Výchozí offline session
        _userSession = MSession.CreateOfflineSession("Guest");
        IsLoggedIn = false;

        // Spustíme načítání na pozadí
        Task.Run(LoadModpackData);
        
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
                
                // Parse MOTD (cleanup formatting codes if needed, but for now simple string)
                var motdList = json["motd"]?["clean"]?.AsArray();
                var motd = motdList != null && motdList.Count > 0 ? motdList[0]?.ToString() : "Void Craft";

                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    IsServerOnline = true;
                    ServerPlayerCount = online;
                    ServerMaxPlayers = max;
                    ServerStatusText = $"{online}/{max} Hráčů";
                    ServerMotd = motd ?? "Void-Craft.eu";
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
            
            var session = await _authService.TrySilentLoginAsync();
            
            if (session != null)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    UserSession = session;
                    IsLoggedIn = true;
                    OnPropertyChanged(nameof(PlayerSkinUrl));
                    Greeting = $"Vítejte, {session.Username}!";
                });
            }
            else
            {
                // Try Offline Auto-Login
                if (!string.IsNullOrEmpty(Config.LastOfflineUsername))
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        try 
                        {
                            UserSession = _authService.LoginOffline(Config.LastOfflineUsername);
                            IsLoggedIn = true;
                            OfflineUsername = Config.LastOfflineUsername;
                            OnPropertyChanged(nameof(PlayerSkinUrl));
                            Greeting = $"Vítejte zpět, {Config.LastOfflineUsername} (Offline)!";
                        }
                        catch
                        {
                            Greeting = "Vítejte ve VOID-CRAFT Launcheru!";
                        }
                    });
                }
                else
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => Greeting = "Vítejte ve VOID-CRAFT Launcheru!");
                }
            }
        }
        catch
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => Greeting = "Vítejte ve VoidCraft Launcheru!");
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
            IsLaunching = true;
            LaunchProgress = 0;

            // ---------------------------------------------------------
            // 1. CHECK & DOWNLOAD & VERIFY (ALWAYS RUN)
            // ---------------------------------------------------------
            var modpackDir = _launcherService.GetModpackPath(CurrentModpack.Name);
            var modsDir = Path.Combine(modpackDir, "mods");
            
            // Always try to verify/install to ensure file integrity (re-download missing mods/configs)
            bool attemptsInstall = true;

            if (attemptsInstall)
            {
                bool apiSuccess = false;
                ModpackManifestInfo manifestInfo = null;
                LaunchStatus = "Ověřuji integritu souborů...";
                
                // Debug.WriteLine($"[PlayModpack] ProjectId: {CurrentModpack.ProjectId}, FileId: {CurrentModpack.CurrentVersion?.FileId}");
                
                // Check if we need to install/update
                bool needsUpdate = true;
                
                // Load currently installed version info
                ModpackManifestInfo installedManifest = ModpackInstaller.LoadManifestInfo(modpackDir);
                
                int fileId = 0;
                
                // Try to resolve target FileId
                if (int.TryParse(CurrentModpack.CurrentVersion?.FileId, out var parsedId) && parsedId > 0)
                {
                    fileId = parsedId;
                }
                else if (CurrentModpack.ProjectId > 0)
                {
                    // FileId missing (custom or API error), try fetch latest
                     try
                        {
                            LaunchStatus = "Načítám nejnovější verzi...";
                            var filesJson = await _curseForgeApi.GetModpackFilesAsync(CurrentModpack.ProjectId);
                            var filesNode = JsonNode.Parse(filesJson);
                            var files = filesNode?["data"]?.AsArray();
                            
                            if (files != null && files.Count > 0)
                            {
                                // Get latest file by date
                                var latestFile = files.OrderByDescending(f => f?["fileDate"]?.ToString()).FirstOrDefault();
                                var latestFileId = latestFile?["id"]?.ToString();
                                var latestFileName = latestFile?["displayName"]?.ToString();
                                
                                if (int.TryParse(latestFileId, out fileId) && fileId > 0)
                                {
                                    // Update CurrentModpack with latest version
                                    CurrentModpack.CurrentVersion = new ModpackVersion
                                    {
                                        Name = latestFileName ?? "Latest",
                                        FileId = latestFileId,
                                        ReleaseDate = latestFile?["fileDate"]?.ToString() ?? ""
                                    };
                                    Debug.WriteLine($"[PlayModpack] Fetched latest FileId: {fileId}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            LogService.Error("[PlayModpack] Failed to fetch latest version", ex);
                        }
                }
                
                // COMPARE VERSIONS
                if (installedManifest != null && fileId > 0 && installedManifest.FileId == fileId)
                {
                    LaunchStatus = "Verze je aktuální. Přeskakuji instalaci.";
                    manifestInfo = installedManifest;
                    needsUpdate = false;
                    apiSuccess = true; // Mark as success since we are good to go
                }
                
                if (needsUpdate && CurrentModpack.ProjectId > 0 && fileId > 0)
                {
                        try 
                        {
                            LaunchStatus = $"Získávám informace o modpacku...";
                            
                            // 2. Get Download URL
                            var fileJson = await _curseForgeApi.GetModFileAsync(CurrentModpack.ProjectId, fileId);
                            var node = JsonNode.Parse(fileJson);
                            var dataNode = node?["data"];
                            var downloadUrl = dataNode?["downloadUrl"]?.ToString();
                            var fileName = dataNode?["fileName"]?.ToString() ?? "modpack.zip";
                            
                            // CurseForge often returns null downloadUrl, construct CDN URL as fallback
                            if (string.IsNullOrEmpty(downloadUrl) && !string.IsNullOrEmpty(fileName))
                            {
                                // CurseForge CDN pattern: https://edge.forgecdn.net/files/{first 4 digits}/{last digits}/{filename}
                                var fileIdStr = fileId.ToString();
                                if (fileIdStr.Length >= 4)
                                {
                                    var part1 = fileIdStr.Substring(0, 4);
                                    var part2 = fileIdStr.Substring(4);
                                    downloadUrl = $"https://edge.forgecdn.net/files/{part1}/{part2}/{fileName}";
                                }
                            }

                            if (!string.IsNullOrEmpty(downloadUrl))
                            {
                                LaunchStatus = "Stahuji manifest...";
                                var tempZipPath = Path.Combine(Path.GetTempPath(), fileName);
                                
                                using (var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                                {
                                    response.EnsureSuccessStatusCode();
                                    var data = await response.Content.ReadAsByteArrayAsync();
                                    await File.WriteAllBytesAsync(tempZipPath, data);
                                }

                                LaunchStatus = "Ověřuji mody...";
                                // This checks for missing files and re-downloads them
                                manifestInfo = await _modpackInstaller.InstallOrUpdateAsync(tempZipPath, modpackDir, fileId);
                                apiSuccess = true;
                                SaveModpacks();
                                
                                try { File.Delete(tempZipPath); } catch {}
                            }
                        }
                        catch (Exception ex)
                        {
                            LaunchStatus = $"Chyba aktualizace: {ex.Message} (Pokračuji offline)";
                            // Removed delay
                        }
                }

                if (!apiSuccess && needsUpdate)
                {
                     // Try local backups or cached manifest checking
                    var downloadsDir = Path.Combine(_launcherService.BasePath, "downloads");
                    if (Directory.Exists(downloadsDir))
                    {
                        var possibleZips = Directory.GetFiles(downloadsDir, "*.zip");
                        var localZip = possibleZips.FirstOrDefault(); 
                        if (!string.IsNullOrEmpty(localZip))
                        {
                             LaunchStatus = "Instaluji z lokální zálohy...";
                             manifestInfo = await _modpackInstaller.InstallOrUpdateAsync(localZip, modpackDir, fileId);
                        }
                    }
                }
                
                // If installation/verification failed (e.g. offline), try to load cached manifest info from disk
                if (manifestInfo == null)
                {
                    LaunchStatus = "Načítám kached informace...";
                    manifestInfo = VoidCraftLauncher.Services.ModpackInstaller.LoadManifestInfo(modpackDir);
                }

                // Store manifest info for launch
                if (manifestInfo != null)
                {
                    _lastManifestInfo = manifestInfo;
                }
                else
                {
                     LaunchStatus = "Chyba: Nepodařilo se načíst informace o verzi (Offline a žádná cache).";
                     await Task.Delay(3000); // Keep this delay so user can see the error
                     IsLaunching = false;
                     return;
                }
            }
            
            LaunchStatus = "Spouštím hru...";

            // Get Minecraft version from manifest (dynamically)
            var mcVersion = _lastManifestInfo?.MinecraftVersion ?? "1.21.1";
            var modLoaderId = _lastManifestInfo?.ModLoaderId ?? "";
            
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
                    // Experimental High-Performance ZGC (Java 21+)
                    jvmArgs.Add("-XX:+UseZGC");
                    jvmArgs.Add("-XX:+ZGenerational");
                }
                else
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
            RunningModpack = CurrentModpack;
            LaunchProgress = 100;
            
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
                });
            });
        }
        catch (Exception ex)
        {
            LaunchStatus = $"Chyba: {ex.Message}";
            await Task.Delay(3000);
            IsLaunching = false;
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
    }

    [RelayCommand]
    public void ManageMods()
    {
        if (CurrentModpack != null)
        {
            var modsPath = Path.Combine(_launcherService.GetModpackPath(CurrentModpack.Name), "mods");
            Directory.CreateDirectory(modsPath);
            Process.Start(new ProcessStartInfo
            {
                FileName = modsPath,
                UseShellExecute = true,
                Verb = "open"
            });
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
            var modsPath = Path.Combine(_launcherService.GetModpackPath(modpack.Name), "mods");
            Directory.CreateDirectory(modsPath);
            Process.Start(new ProcessStartInfo
            {
                FileName = modsPath,
                UseShellExecute = true,
                Verb = "open"
            });
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
        if (IsLoggedIn) return;
        IsLoginModalVisible = true;
        IsLoginModalVisible = true;
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
        try
        {
            IsLoginModalVisible = false; // Hide modal
            IsWebviewVisible = true;
            IsBrowserPanelVisible = false;
            
            var session = await _authService.LoginWithBrowserAsync((msg) =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => Greeting = msg);
            });
            
            UserSession = session;
            IsLoggedIn = true;
            IsWebviewVisible = false;
            OnPropertyChanged(nameof(PlayerSkinUrl));
            Greeting = $"Vítejte, {session.Username}!";
        }
        catch (System.Exception ex)
        {
            Greeting = $"Chyba přihlášení: {ex.Message}";
            IsWebviewVisible = false;
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
            _launcherService.SaveConfig(Config);

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
            await _authService.LogoutAsync();
            
            // Clear persistent session
            Config.LastOfflineUsername = null;
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
        CurrentRightView = RightViewType.Browser;
    }
    [RelayCommand]
    public void GoToSettings()
    {
        CurrentRightView = RightViewType.GlobalSettings;
    }

    [RelayCommand]
    public void GoToHome()
    {
        CurrentRightView = RightViewType.Home;
    }

    [ObservableProperty]
    private InstanceConfig _currentModpackConfig;

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

        CurrentRightView = RightViewType.ModpackSettings;
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
        
        var configPath = Path.Combine(modpackDir, "potato_mods.json");
        
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = configPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
           Greeting = $"Nelze otevřít config: {ex.Message}";
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

    [RelayCommand]
    public void OpenBrowser(string source)
    {
        BrowserSource = source;
        BrowserSearchQuery = "";
        BrowserResults.Clear();
        CurrentRightView = RightViewType.Browser;
        
        // Populate with popular modpacks initially
        SearchModpacksCommand.Execute(null);
    }

    [RelayCommand]
    public async Task SearchModpacks()
    {
        if (IsSearching) return;
        IsSearching = true;
        BrowserResults.Clear();

        try
        {
            if (BrowserSource == "CurseForge")
            {
                await SearchCurseForge();
            }
            else
            {
                await SearchModrinth();
            }
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

    private async Task SearchCurseForge()
    {
        string json;
        if (string.IsNullOrWhiteSpace(BrowserSearchQuery))
            json = await _curseForgeApi.SearchModpacksAsync(""); // Popular
        else
            json = await _curseForgeApi.SearchModpacksAsync(BrowserSearchQuery);

         var root = JsonNode.Parse(json);
         var data = root?["data"]?.AsArray();

         if (data != null)
         {
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
    }

    private async Task SearchModrinth()
    {
        string json = await _modrinthApi.SearchModpacksAsync(BrowserSearchQuery); // Modrinth handles empty query as generic search

        var root = JsonNode.Parse(json);
        var hits = root?["hits"]?.AsArray();

        if (hits != null)
        {
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
        await PlayModpack();
    }

    [RelayCommand]
    public void SelectAndConfigure(ModpackInfo modpack)
    {
        CurrentModpack = modpack;
        GoToModpackSettings(); // This properly initializes CurrentModpackConfig
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
            ProjectId = 0 // Will be updated if CF
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
                            LogoUrl = item.IconUrl,
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

    [RelayCommand]
    public void SetAutoJava()
    {
        if (Config != null)
        {
            Config.JavaPath = "";
            _launcherService.SaveConfig(Config);
            Greeting = "Java nastavena na Automaticky.";
        }
    }
}

