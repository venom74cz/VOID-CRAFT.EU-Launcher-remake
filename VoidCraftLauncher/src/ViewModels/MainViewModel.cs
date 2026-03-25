using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VoidCraftLauncher.Services;
using VoidCraftLauncher.Models;
using System.ComponentModel;
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
using VoidCraftLauncher.Models.CreatorStudio;
using VoidCraftLauncher.Services.CreatorStudio;

namespace VoidCraftLauncher.ViewModels;

// ===== NAVIGATION ENUM (namespace-level for cross-file access) =====
public enum MainViewType
{
    Dashboard,
    Library,
    Discover,
    Settings,
    InstanceDetail,
    SkinStudio,
    Achievements,
    ServerHub,
    News,
    StreamingTools,
    Future,
    ThemeSwitcher,
    Localization
}

public enum BackupPromptDecision
{
    Cancel,
    ContinueWithoutBackup,
    BackupAndContinue
}

/// <summary>
/// Core orchestrator – fields, constructor, navigation, modpack lifecycle.
/// All business logic lives in partial class files:
///   MainViewModel.Auth.cs        – login, logout, multi-account
///   MainViewModel.Launch.cs      – game launch, JVM args, GTNH
///   MainViewModel.Browser.cs     – CurseForge/Modrinth search, install
///   MainViewModel.Settings.cs    – config, options presets, potato mode
///   MainViewModel.CustomProfile.cs – custom profiles, mod management
///   MainViewModel.Updates.cs     – update checks, server status, changelog
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    // ===== COLLECTIONS =====
    public ObservableCollection<ModpackInfo> InstalledModpacks { get; private set; }
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRecentModpacks))]
    private ObservableCollection<ModpackInfo> _recentModpacks = new();

    public bool HasRecentModpacks => RecentModpacks.Count > 0;

    // ===== SERVICES =====
    private readonly AuthService _authService;
    private readonly LauncherService _launcherService;
    private readonly CurseForgeApi _curseForgeApi;
    private readonly ModrinthApi _modrinthApi;
    private readonly HttpClient _httpClient;
    private readonly ModpackInstaller _modpackInstaller;
    private readonly DiscordRpcService _discordRpcService;
    private readonly NavigationService _navigationService;
    private readonly ThemeEngine _themeEngine;
    private readonly LocalizationService _localizationService;
    private readonly AchievementHubService _achievementHubService;
    private readonly SkinStudioService _skinStudioService;
    private readonly CreatorWorkbenchService _creatorWorkbenchService;
    private readonly CreatorWorkspaceService _creatorWorkspaceService;
    private readonly CreatorManifestService _creatorManifestService;
    private ModpackManifestInfo _lastManifestInfo;
    private readonly SemaphoreSlim _modpackUpdateCheckLock = new(1, 1);
    private static readonly TimeSpan ModpackUpdateCheckInterval = TimeSpan.FromSeconds(5);
    private ModpackInfo? _observedCurrentModpack;

    // ===== CORE STATE =====

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

    public string PlayerSkinUrl => UserSession?.UUID != null 
        ? $"https://mc-heads.net/avatar/{UserSession.UUID.Replace("-", "")}/40" 
        : "https://mc-heads.net/avatar/MHF_Steve/40";

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

    // ===== NAVIGATION =====

    [ObservableProperty]
    private MainViewType _currentMainView = MainViewType.Dashboard;

    public bool IsDashboardView => CurrentMainView == MainViewType.Dashboard;
    public bool IsLibraryView => CurrentMainView == MainViewType.Library;
    public bool IsDiscoverView => CurrentMainView == MainViewType.Discover;
    public bool IsSettingsView => CurrentMainView == MainViewType.Settings;
    public bool IsInstanceDetailView => CurrentMainView == MainViewType.InstanceDetail;
    public bool IsSkinStudioView => CurrentMainView == MainViewType.SkinStudio;
    public bool IsAchievementsView => CurrentMainView == MainViewType.Achievements;
    public bool IsServerHubView => CurrentMainView == MainViewType.ServerHub;
    public bool IsNewsView => CurrentMainView == MainViewType.News;
    public bool IsStreamingToolsView => CurrentMainView == MainViewType.StreamingTools;
    public bool IsFutureView => CurrentMainView == MainViewType.Future;
    public bool IsThemeSwitcherView => CurrentMainView == MainViewType.ThemeSwitcher;
    public bool IsLocalizationView => CurrentMainView == MainViewType.Localization;

    partial void OnCurrentMainViewChanged(MainViewType value)
    {
        OnPropertyChanged(nameof(IsDashboardView));
        OnPropertyChanged(nameof(IsLibraryView));
        OnPropertyChanged(nameof(IsDiscoverView));
        OnPropertyChanged(nameof(IsSettingsView));
        OnPropertyChanged(nameof(IsInstanceDetailView));
        OnPropertyChanged(nameof(IsSkinStudioView));
        OnPropertyChanged(nameof(IsAchievementsView));
        OnPropertyChanged(nameof(IsServerHubView));
        OnPropertyChanged(nameof(IsNewsView));
        OnPropertyChanged(nameof(IsStreamingToolsView));
        OnPropertyChanged(nameof(IsFutureView));
        OnPropertyChanged(nameof(IsThemeSwitcherView));
        OnPropertyChanged(nameof(IsLocalizationView));
        OnPropertyChanged(nameof(MainPanelTitle));

        if (value == MainViewType.InstanceDetail)
        {
            LoadInstalledMods();
            _ = FetchFullDescriptionAsync();
        }

        if (value == MainViewType.Achievements)
        {
            _ = LoadAchievementSnapshotAsync();
        }

        if (value == MainViewType.StreamingTools)
        {
            _ = RefreshCreatorWorkbenchAsync();
        }

        if (value == MainViewType.Future)
        {
            _ = LoadFutureRoadmapAsync();
        }

        UpdateDiscordPresence();
    }

    public string MainPanelTitle => CurrentMainView switch
    {
        MainViewType.Dashboard => L("Shell.Title.Dashboard"),
        MainViewType.Library => L("Shell.Title.Library"),
        MainViewType.Discover => LF("Shell.Title.Discover", BrowserSource),
        MainViewType.Settings => L("Shell.Title.Settings"),
        MainViewType.InstanceDetail => CurrentModpack?.Name ?? L("Shell.Title.InstanceDetailFallback"),
        MainViewType.SkinStudio => L("Shell.Title.SkinStudio"),
        MainViewType.Achievements => L("Shell.Title.Achievements"),
        MainViewType.ServerHub => L("Shell.Title.ServerHub"),
        MainViewType.News => L("Shell.Title.News"),
        MainViewType.StreamingTools => L("Shell.Title.Streaming"),
        MainViewType.Future => L("Shell.Title.Future"),
        MainViewType.ThemeSwitcher => L("Shell.Title.Themes"),
        MainViewType.Localization => L("Shell.Title.Localization"),
        _ => L("Shell.Title.Default")
    };

    [RelayCommand]
    private void GoToSkinStudio() => NavigateToView(MainViewType.SkinStudio);

    [RelayCommand]
    private void GoToAchievements() => NavigateToView(MainViewType.Achievements);

    [RelayCommand]
    private void GoToServerHub() => NavigateToView(MainViewType.ServerHub);

    [RelayCommand]
    private void GoToNews() => NavigateToView(MainViewType.News);

    [RelayCommand]
    private void GoToStreamingTools() => NavigateToView(MainViewType.StreamingTools);

    [RelayCommand]
    private void GoToFuture() => NavigateToView(MainViewType.Future);

    [RelayCommand]
    private void GoToThemeSwitcher() => NavigateToView(MainViewType.ThemeSwitcher);

    [RelayCommand]
    private void GoToLocalization() => NavigateToView(MainViewType.Localization);

    [RelayCommand]
    private void GoBack() => _navigationService.GoBack();

    // ===== BACKUP PROMPT =====

    [ObservableProperty]
    private bool _isBackupPromptVisible = false;

    [ObservableProperty]
    private string _backupTargetName = "";

    [ObservableProperty]
    private string _backupTargetPath = "";

    private TaskCompletionSource<BackupPromptDecision>? _backupPromptTcs;

    private static readonly string[] ProtectedBackupEntries = { "config", "saves", "shaderpacks", "options.txt", "servers.dat" };

    public Task<BackupPromptDecision> ShowBackupPrompt(string instanceName)
    {
        BackupTargetName = instanceName;
        BackupTargetPath = GetPersistentBackupRoot(instanceName);
        IsBackupPromptVisible = true;
        _backupPromptTcs = new TaskCompletionSource<BackupPromptDecision>();
        return _backupPromptTcs.Task;
    }

    [RelayCommand]
    private void ConfirmBackup()
    {
        IsBackupPromptVisible = false;
        _backupPromptTcs?.TrySetResult(BackupPromptDecision.BackupAndContinue);
    }

    [RelayCommand]
    private void SkipBackup()
    {
        IsBackupPromptVisible = false;
        _backupPromptTcs?.TrySetResult(BackupPromptDecision.ContinueWithoutBackup);
    }

    [RelayCommand]
    private void DismissBackupPrompt()
    {
        IsBackupPromptVisible = false;
        _backupPromptTcs?.TrySetResult(BackupPromptDecision.Cancel);
    }

    private string GetPersistentBackupRoot(string instanceName)
    {
        return Path.Combine(_launcherService.BackupsPath, BuildCreateProfileDirectoryName(instanceName));
    }

    private bool InstanceHasProtectedContent(string instancePath)
    {
        return ProtectedBackupEntries.Any(relativePath =>
        {
            var fullPath = Path.Combine(instancePath, relativePath);
            return Directory.Exists(fullPath) || File.Exists(fullPath);
        });
    }

    private async Task<string?> CreatePersistentBackupSnapshotAsync(ModpackInfo modpack, string reason, bool notifyUser = true)
    {
        var instancePath = _launcherService.GetModpackPath(modpack.Name);
        if (!Directory.Exists(instancePath) || !InstanceHasProtectedContent(instancePath))
        {
            return null;
        }

        var backupPath = ModpackInstaller.BackupUserConfigs(instancePath, GetPersistentBackupRoot(modpack.Name));
        await RefreshCurrentModpackWorkspaceDataAsync();

        StructuredLog.Event("Backup", "Protective snapshot created", new
        {
            Modpack = modpack.Name,
            Reason = reason,
            BackupPath = backupPath
        });

        if (notifyUser)
        {
            ShowToast("Snapshot vytvořen", Path.GetFileName(backupPath), ToastSeverity.Success, 3000);
        }

        return backupPath;
    }

    private async Task<bool> ConfirmBackupBeforeDestructiveActionAsync(ModpackInfo modpack, string reason)
    {
        var instancePath = _launcherService.GetModpackPath(modpack.Name);
        if (!Directory.Exists(instancePath) || !InstanceHasProtectedContent(instancePath))
        {
            return true;
        }

        var decision = await ShowBackupPrompt(modpack.Name);
        switch (decision)
        {
            case BackupPromptDecision.Cancel:
                Greeting = $"Akce pro {modpack.Name} byla zrušena.";
                return false;
            case BackupPromptDecision.ContinueWithoutBackup:
                ShowToast("Bez snapshotu", $"{modpack.Name} pokračuje bez ochranné zálohy.", ToastSeverity.Warning, 2500);
                return true;
            case BackupPromptDecision.BackupAndContinue:
                try
                {
                    var backupPath = await CreatePersistentBackupSnapshotAsync(modpack, reason);
                    if (backupPath == null)
                    {
                        ShowToast("Snapshot přeskočen", "V instanci nebyla nalezena data vhodná k záloze.", ToastSeverity.Info, 2500);
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    LogService.Error($"Protective backup failed before {reason}", ex);
                    Greeting = $"Ochranná záloha pro {modpack.Name} selhala. Destruktivní akce byla zastavena.";
                    ShowToast("Snapshot selhal", ex.Message, ToastSeverity.Error, 4000);
                    return false;
                }
            default:
                return false;
        }
    }

    // ===== CRASH DRAWER =====

    [ObservableProperty]
    private bool _isCrashDrawerOpen = false;

    [ObservableProperty]
    private string _crashModpackName = "";

    [ObservableProperty]
    private string _crashSummary = "";

    [ObservableProperty]
    private string _crashExitCode = "";

    [ObservableProperty]
    private string _crashRuntime = "";

    [ObservableProperty]
    private string _crashLogTail = "";

    private string? _crashLogPath;

    public void ShowCrashDrawer(string modpackName, int exitCode, TimeSpan runtime, string logTail, string? logPath = null)
    {
        CrashModpackName = modpackName;
        CrashExitCode = exitCode.ToString();
        CrashRuntime = runtime.TotalMinutes < 1 ? $"{runtime.Seconds}s" : $"{(int)runtime.TotalMinutes}m {runtime.Seconds}s";
        CrashLogTail = logTail;
        CrashSummary = exitCode switch
        {
            -1 => "Hra byla násilně ukončena (out of memory nebo kill).",
            1 => "Hra skončila s obecnou chybou. Zkontrolujte log.",
            _ => $"Hra skončila s kódem {exitCode}."
        };
        _crashLogPath = logPath;
        IsCrashDrawerOpen = true;
    }

    [RelayCommand]
    private void CloseCrashDrawer() => IsCrashDrawerOpen = false;

    [RelayCommand]
    private void OpenCrashLog()
    {
        if (_crashLogPath != null && System.IO.File.Exists(_crashLogPath))
            Process.Start(new ProcessStartInfo(_crashLogPath) { UseShellExecute = true });
    }

    [RelayCommand]
    private async Task CopyCrashLog()
    {
        if (!string.IsNullOrEmpty(CrashLogTail))
        {
            var clipboard = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow?.Clipboard : null;
            if (clipboard != null)
                await clipboard.SetTextAsync(CrashLogTail);
        }
    }

    [RelayCommand]
    private void RestartAfterCrash()
    {
        IsCrashDrawerOpen = false;
        if (CurrentModpack != null)
            SelectAndPlayCommand.Execute(CurrentModpack);
    }

    // ===== LAUNCH STATE =====

    [ObservableProperty]
    private bool _isLaunching = false;

    [ObservableProperty]
    private double _launchProgress = 0;

    [ObservableProperty]
    private ModpackInfo? _targetModpack;

    [ObservableProperty]
    private string _launchStatus = "";

    [ObservableProperty]
    private string _currentFile = "";

    [ObservableProperty]
    private bool _isGameRunning = false;

    [ObservableProperty]
    private ModpackInfo? _runningModpack;

    // ===== AUTH STATE =====

    [ObservableProperty]
    private bool _isLoginModalVisible = false;

    [ObservableProperty]
    private string _offlineUsername = "";

    [ObservableProperty]
    private string _appVersion = "v1.0.0";

    [ObservableProperty]
    private ObservableCollection<ChangelogEntry> _changelogEntries = new();

    [ObservableProperty]
    private ObservableCollection<AccountProfile> _accounts = new();

    [ObservableProperty]
    private AccountProfile? _activeAccount;

    [ObservableProperty]
    private bool _isAccountPickerOpen = false;

    // ===== SYSTEM INFO =====
    public int SystemRamMb => (int)(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024 / 1024);

    // ===== TOAST NOTIFICATIONS =====
    public ObservableCollection<ToastItem> Toasts { get; } = new();

    public void ShowToast(string title, string message, ToastSeverity severity = ToastSeverity.Info, int durationMs = 4000)
    {
        var toast = new ToastItem { Title = title, Message = message, Severity = severity, DurationMs = durationMs };
        Avalonia.Threading.Dispatcher.UIThread.Post(() => Toasts.Add(toast));
        Task.Delay(durationMs).ContinueWith(_ =>
            Avalonia.Threading.Dispatcher.UIThread.Post(() => Toasts.Remove(toast)));
    }

    [RelayCommand]
    private void DismissToast(ToastItem toast)
    {
        Toasts.Remove(toast);
    }

    // ╔═══════════════════════════════════════╗
    // ║           CONSTRUCTOR                  ║
    // ╚═══════════════════════════════════════╝

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

        // Init services (resolved from DI container)
        var sl = ServiceLocator.Current;
        _authService = sl.Resolve<AuthService>();
        _launcherService = sl.Resolve<LauncherService>();
        _curseForgeApi = sl.Resolve<CurseForgeApi>();
        _modrinthApi = sl.Resolve<ModrinthApi>();
        _modpackInstaller = sl.Resolve<ModpackInstaller>();
        _discordRpcService = sl.Resolve<DiscordRpcService>();
        _navigationService = sl.Resolve<NavigationService>();
        _themeEngine = sl.Resolve<ThemeEngine>();
        _localizationService = sl.Resolve<LocalizationService>();
        _achievementHubService = sl.Resolve<AchievementHubService>();
        _skinStudioService = sl.Resolve<SkinStudioService>();
        _creatorWorkbenchService = sl.Resolve<CreatorWorkbenchService>();
        _creatorWorkspaceService = sl.Resolve<CreatorWorkspaceService>();
        _creatorManifestService = sl.Resolve<CreatorManifestService>();
        _discordRpcService.Initialize();
        _discordRpcService.PresenceChanged += () => Avalonia.Threading.Dispatcher.UIThread.Post(NotifyStreamingToolsStateChanged);
        
        // Forward installer events to UI
        _modpackInstaller.StatusChanged += (status) => LaunchStatus = status;
        _modpackInstaller.ProgressChanged += (progress) => 
        {
            LaunchProgress = progress * 100;
            IsLaunchIndeterminate = false;
        };

        _currentModpack = new ModpackInfo();
        InstalledModpacks = new ObservableCollection<ModpackInfo>();
        InstalledModpacks.CollectionChanged += OnInstalledModpacksCollectionChanged;
        CurrentModpackScreenshots.CollectionChanged += OnCurrentModpackScreenshotsChanged;
        
        _httpClient = sl.Resolve<HttpClient>();
        
        // Load Config
        try 
        {
            Config = _launcherService.LoadConfig();
        } 
        catch 
        {
            Config = new LauncherConfig();
        }

        if (string.IsNullOrWhiteSpace(Config.CurrentThemeId))
        {
            Config.CurrentThemeId = "obsidian";
        }

        if (string.IsNullOrWhiteSpace(Config.PreferredLanguageCode))
        {
            Config.PreferredLanguageCode = LocalizationService.SystemLanguageCode;
        }

        if (string.IsNullOrWhiteSpace(Config.MotionPreference))
        {
            Config.MotionPreference = ThemeEngine.MotionPreferenceSystem;
        }

        Config.CreatorStudio ??= new CreatorStudioPreferences();

        InitializeThemeSurface();
        InitializeLocalizationSurface();
        InitializeAchievementSurface();
        InitializeCreatorStudioShell();

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

        // Background tasks (instrumented via ObservabilityService)
        var obs = sl.Resolve<ObservabilityService>();
        Task.Run(async () => { using var op = obs.BeginOperation("LoadModpackData"); try { await LoadModpackData(); op.Complete(); } catch (Exception ex) { op.Fail(ex.Message); } });
        Task.Run(async () => { using var op = obs.BeginOperation("AutoLogin"); try { await TryAutoLogin(); op.Complete(); } catch (Exception ex) { op.Fail(ex.Message); } });
        Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(1.5));
            using var op = obs.BeginOperation("LoadChangelog");
            try { await LoadChangelogAsync(); op.Complete(); } catch (Exception ex) { op.Fail(ex.Message); }
        });
        Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(8));
            await ModpackUpdateLoop();
        });
        Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(4));
            using var op = obs.BeginOperation("CheckForUpdates");
            try { await CheckForUpdates(); op.Complete(); } catch (Exception ex) { op.Fail(ex.Message); }
        });
        
        // Wire navigation service → ViewModel sync
        _navigationService.ViewChanged += view =>
            Avalonia.Threading.Dispatcher.UIThread.Post(() => CurrentMainView = view);

        // Initialize M5 backend services (Server Hub, Social Feeds, Export/Import, Secure Storage)
        InitializeM5Services();
        HandleInstalledModpacksChanged();

        // Initialize structured logging
        StructuredLog.Initialize(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".voidcraft"));
        StructuredLog.Event("Startup", "Launcher initialized", new { Version = AppVersion });
    }

    // ===== CURRENT MODPACK EVENTS =====

    partial void OnCurrentModpackChanged(ModpackInfo value)
    {
        if (!ReferenceEquals(_observedCurrentModpack, value))
        {
            if (_observedCurrentModpack != null)
            {
                _observedCurrentModpack.PropertyChanged -= OnCurrentModpackPropertyChanged;
            }

            _observedCurrentModpack = value;

            if (_observedCurrentModpack != null)
            {
                _observedCurrentModpack.PropertyChanged += OnCurrentModpackPropertyChanged;
            }
        }

        NotifyCurrentModpackDerivedStateChanged();
        _ = LoadCurrentModpackScreenshotsAsync();
        _ = RefreshCurrentModpackWorkspaceDataAsync();
        RefreshCurrentModpackCreatorManifest();
        RebuildSkinStudioInstanceOptions();
        RebuildNewServerModpackOptions();
        _ = RefreshCreatorWorkbenchAsync();
        NotifyStreamingToolsStateChanged();
        RefreshCreatorWorkspaceContext();
    }

    private void OnCurrentModpackPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        NotifyCurrentModpackDerivedStateChanged();

        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(ModpackInfo.Name))
        {
            _ = LoadCurrentModpackScreenshotsAsync();
            _ = RefreshCurrentModpackWorkspaceDataAsync();
            RefreshCurrentModpackCreatorManifest();
            RefreshCreatorWorkspaceContext();
        }
    }

    private void NotifyCurrentModpackDerivedStateChanged()
    {
        OnPropertyChanged(nameof(MainPanelTitle));
        OnPropertyChanged(nameof(CurrentWorkspaceDisplayName));
        OnPropertyChanged(nameof(HasCurrentWorkspaceDescription));
        OnPropertyChanged(nameof(CurrentWorkspaceDescription));
        OnPropertyChanged(nameof(CurrentWorkspaceAuthorLabel));
        OnPropertyChanged(nameof(CurrentWorkspaceMetadataSummary));
        OnPropertyChanged(nameof(CurrentWorkspacePrimaryServerLabel));
        OnPropertyChanged(nameof(CurrentWorkspaceRecommendedRamLabel));
        UpdateDiscordPresence();
    }

    private void OnInstalledModpacksCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        HandleInstalledModpacksChanged();
    }

    private void HandleInstalledModpacksChanged()
    {
        RebuildSkinStudioInstanceOptions();
        RebuildNewServerModpackOptions();
        _ = RefreshCreatorWorkbenchAsync();
        _ = RefreshAutoDiscoveredServersAsync();
        RefreshCreatorWorkspaceContext();
        RefreshRecentModpacks();
    }

    public void RefreshRecentModpacks()
    {
        if (Config == null) return;
        var newRecents = new List<ModpackInfo>();
        foreach (var recentName in Config.RecentInstances)
        {
            var mp = InstalledModpacks.FirstOrDefault(m => string.Equals(m.Name, recentName, StringComparison.OrdinalIgnoreCase));
            if (mp != null)
                newRecents.Add(mp);
        }

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            RecentModpacks.Clear();
            foreach (var r in newRecents)
                RecentModpacks.Add(r);
                
            OnPropertyChanged(nameof(HasRecentModpacks));
        });
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

    // ===== NAVIGATION COMMANDS =====

    [RelayCommand]
    public void GoToDashboard()
    {
        NavigateToView(MainViewType.Dashboard, true);
    }

    [RelayCommand]
    public void GoToHome()
    {
        NavigateToView(MainViewType.Library, true);
    }

    [RelayCommand]
    public void GoToBrowser()
    {
        NavigateToView(MainViewType.Discover, true);
        SearchModpacksCommand.Execute(null);
    }

    [RelayCommand]
    public void GoToSettings()
    {
        NavigateToView(MainViewType.Settings, true);
    }

    [RelayCommand]
    public void GoToInstanceDetail(ModpackInfo? modpack = null)
    {
        if (modpack != null) CurrentModpack = modpack;
        NavigateToView(MainViewType.InstanceDetail);
    }

    private void NavigateToView(MainViewType target, bool root = false)
    {
        if (root)
            _navigationService.NavigateRoot(target);
        else
            _navigationService.Navigate(target);
    }

    [RelayCommand]
    public void ToggleModpackBrowser()
    {
        IsWebviewVisible = !IsWebviewVisible;
        IsBrowserPanelVisible = !IsBrowserPanelVisible;
    }

    // ===== MISC UTILITIES =====

    private void UpdateDiscordPresence()
    {
        string details = MainPanelTitle;
        string state = CurrentMainView switch
        {
            MainViewType.Dashboard => "Na dashboardu",
            MainViewType.Library => "Prohlíží si knihovnu",
            MainViewType.Discover => $"Hledá nové modpacky ({BrowserSource})",
            MainViewType.Settings => "Upravuje nastavení",
            MainViewType.InstanceDetail => $"Detail: {CurrentModpack?.Name}",
            MainViewType.Future => "Prohlíží si future roadmapu",
            _ => "V hlavní nabídce"
        };

        _discordRpcService.SetState(details, state);
        NotifyStreamingToolsStateChanged();
    }

    [RelayCommand]
    public async Task CopyIp()
    {
        try
        {
            var topLevel = Avalonia.Controls.TopLevel.GetTopLevel(
                Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                    ? desktop.MainWindow
                    : null);

            if (topLevel?.Clipboard != null)
            {
                await topLevel.Clipboard.SetTextAsync("mc.void-craft.eu");
                Greeting = "IP zkopírována!";
            }
        }
        catch { }
    }

    [RelayCommand]
    public void OpenUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
    }

    [RelayCommand]
    public void OpenScreenshot(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            var localPath = new Uri(path).LocalPath;
            Process.Start(new ProcessStartInfo { FileName = localPath, UseShellExecute = true });
        }
        catch { }
    }
}
