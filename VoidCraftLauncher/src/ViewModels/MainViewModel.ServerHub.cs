﻿using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VoidCraftLauncher.Models;
using VoidCraftLauncher.Services;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using fNbt;

namespace VoidCraftLauncher.ViewModels;

/// <summary>
/// M5 — Backend and infra: Server Hub, social feeds, export/import, and server auto-connect.
/// </summary>
public partial class MainViewModel
{
    // ╔═══════════════════════════════════════╗
    // ║        SERVER HUB                     ║
    // ╚═══════════════════════════════════════╝

    private ServerStatusService? _serverStatusService;
    private ServerDiscoveryService? _serverDiscoveryService;
    private SocialFeedService? _socialFeedService;
    private InstanceExportService? _instanceExportService;
    private SecureStorageService? _secureStorageService;
    private ObservabilityService? _observabilityService;

    /// <summary>All registered servers (pinned VOID-CRAFT + user-added).</summary>
    public ObservableCollection<ServerInfo> Servers { get; } = new();

    /// <summary>Non-pinned custom/community servers rendered in Server Hub.</summary>
    public ObservableCollection<ServerInfo> CommunityServers { get; } = new();

    /// <summary>The default pinned VOID-CRAFT server entry.</summary>
    [ObservableProperty]
    private ServerInfo? _pinnedServer;

    [ObservableProperty]
    private bool _isAddServerSheetOpen;

    [ObservableProperty]
    private string _newServerName = "";

    [ObservableProperty]
    private string _newServerAddress = "";

    [ObservableProperty]
    private int _newServerPort = 25565;

    [ObservableProperty]
    private string _newServerModpackName = "";

    [ObservableProperty]
    private int _newServerProjectId;

    [ObservableProperty]
    private bool _newServerAutoConnect = true;

    public ObservableCollection<SelectionOption> NewServerModpackOptions { get; } = new();

    [ObservableProperty]
    private SelectionOption? _selectedNewServerModpackOption;

    // ===== SOCIAL FEED =====

    public ObservableCollection<FeedItem> NewsFeed { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFeaturedMinecraftNews))]
    private FeedItem? _featuredMinecraftNews;

    [ObservableProperty]
    private bool _isFeedLoading;

    [ObservableProperty]
    private string _activeFeedFilter = "Vše";

    public ObservableCollection<FeedItem> FilteredNewsFeed { get; } = new();

    public bool HasFeedItems => FilteredNewsFeed.Count > 0;
    public bool HasFeaturedMinecraftNews => FeaturedMinecraftNews != null;
    public bool HasCommunityServers => CommunityServers.Count > 0;
    public int TotalServerCount => Servers.Count;

    // ===== EXPORT/IMPORT STATE =====

    [ObservableProperty]
    private bool _isExporting;

    [ObservableProperty]
    private bool _isImporting;

    [ObservableProperty]
    private double _exportProgress;

    [ObservableProperty]
    private double _importProgress;

    public ObservableCollection<InstanceBackupSnapshot> CurrentModpackBackupSnapshots { get; } = new();
    public ObservableCollection<ServerInfo> CurrentModpackLinkedServers { get; } = new();

    public bool HasCurrentModpackBackups => CurrentModpackBackupSnapshots.Count > 0;
    public bool HasCurrentModpackLinkedServers => CurrentModpackLinkedServers.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasImportArchivePath))]
    private string _importArchivePath = "";

    public bool HasImportArchivePath => !string.IsNullOrWhiteSpace(ImportArchivePath);

    // ╔═══════════════════════════════════════╗
    // ║    INIT — called from constructor     ║
    // ╚═══════════════════════════════════════╝

    /// <summary>
    /// Call this from the main constructor to wire up M5 services.
    /// </summary>
    private void InitializeM5Services()
    {
        var sl = ServiceLocator.Current;
        _serverStatusService = sl.Resolve<ServerStatusService>();
        _serverDiscoveryService = sl.Resolve<ServerDiscoveryService>();
        _socialFeedService = sl.Resolve<SocialFeedService>();
        _instanceExportService = sl.Resolve<InstanceExportService>();
        _secureStorageService = sl.Resolve<SecureStorageService>();
        _observabilityService = sl.Resolve<ObservabilityService>();

        // Configure social feed endpoints
        _socialFeedService.ContentFeedEndpoint = "https://api.void-craft.eu/api/feed/content?limit=12";
        _socialFeedService.DiscordFeedEndpoint = "https://api.void-craft.eu/api/discord/channel/1379020115542278187/messages";
        _socialFeedService.YouTubeFeedUrl = "https://www.youtube.com/feeds/videos.xml?channel_id=UCfQSefwt5PFVnTjYHtg064Q";
        _socialFeedService.OfficialMinecraftFeedEndpoint = "https://api.void-craft.eu/api/feed/minecraft-official";

        var cachedFeed = _socialFeedService.GetCachedFeedSnapshot();
        if (cachedFeed.Count > 0)
        {
            ApplySocialFeedItems(cachedFeed);
        }

        // Create pinned VOID-CRAFT server
        PinnedServer = new ServerInfo
        {
            Name = "VOID-CRAFT",
            Address = "mc.void-craft.eu",
            Port = 25565,
            IsPinned = true,
            LinkedModpackName = "VOID-BOX 2",
            LinkedModpackProjectId = 1402056,
            RequiredMcVersion = "1.20.1",
            RequiredModLoader = "forge",
            AutoConnect = true,
            IconUrl = "https://void-craft.eu/logo.png"
        };
        Servers.Add(PinnedServer);

        foreach (var customServer in Config.CustomServers)
        {
            if (customServer.IsPinned) continue;
            Servers.Add(CloneServer(customServer));
        }

        Servers.CollectionChanged += OnServersCollectionChanged;
        RebuildCommunityServers();
        RebuildNewServerModpackOptions();
        ResetNewServerForm();

        // Start server status polling for all servers
        _serverStatusService.StatusUpdated += OnServerStatusUpdated;
        _serverStatusService.StartPollingLoop(Servers, 60000);

        _ = RefreshAutoDiscoveredServersAsync();

        // Load social feed in background
        _ = LoadSocialFeedAsync();
    }

    private void OnServerStatusUpdated(ServerInfo server)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            // Sync to the main ViewModel's properties for legacy bindings
            if (server.Address == "mc.void-craft.eu")
            {
                IsServerOnline = server.IsOnline;
                ServerPlayerCount = server.PlayerCount;
                ServerMaxPlayers = server.MaxPlayers;
                ServerStatusText = server.StatusText;
                ServerMotd = server.Motd;
            }

            OnPropertyChanged(nameof(TotalServerCount));
            OnPropertyChanged(nameof(CreatorStudioLinkedServersLabel));
        });
    }

    private void OnServersCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildCommunityServers();
        PersistCustomServers();
        _ = RefreshCurrentModpackWorkspaceDataAsync();
    }

    private void RebuildCommunityServers()
    {
        CommunityServers.Clear();
        foreach (var server in Servers.Where(server => !server.IsPinned))
            CommunityServers.Add(server);

        OnPropertyChanged(nameof(HasCommunityServers));
        OnPropertyChanged(nameof(TotalServerCount));
        OnPropertyChanged(nameof(CreatorStudioLinkedServersLabel));
    }

    private void PersistCustomServers()
    {
        Config.CustomServers = Servers
            .Where(server => !server.IsPinned && !server.IsAutoDiscovered)
            .Select(CloneServer)
            .ToList();
        _launcherService.SaveConfig(Config);
    }

    private static ServerInfo CloneServer(ServerInfo source)
    {
        return new ServerInfo
        {
            Name = source.Name,
            Address = source.Address,
            Port = source.Port,
            Motd = source.Motd,
            IsOnline = source.IsOnline,
            PlayerCount = source.PlayerCount,
            MaxPlayers = source.MaxPlayers,
            StatusText = source.StatusText,
            IsPinned = source.IsPinned,
            LinkedModpackName = source.LinkedModpackName,
            LinkedModpackProjectId = source.LinkedModpackProjectId,
            RequiredMcVersion = source.RequiredMcVersion,
            RequiredModLoader = source.RequiredModLoader,
            AutoConnect = source.AutoConnect,
            IconUrl = source.IconUrl,
            LinkedModCount = source.LinkedModCount,
            IsAutoDiscovered = source.IsAutoDiscovered,
            DiscoverySource = source.DiscoverySource,
            LastPolled = source.LastPolled
        };
    }

    private void ResetNewServerForm()
    {
        NewServerName = "";
        NewServerAddress = "";
        NewServerPort = 25565;
        NewServerModpackName = "";
        NewServerProjectId = 0;
        NewServerAutoConnect = true;
        SelectedNewServerModpackOption = NewServerModpackOptions.FirstOrDefault(option => option.Id == CurrentModpack?.Name)
            ?? NewServerModpackOptions.FirstOrDefault();
    }

    partial void OnSelectedNewServerModpackOptionChanged(SelectionOption? value)
    {
        if (value == null)
        {
            NewServerModpackName = string.Empty;
            NewServerProjectId = 0;
            return;
        }

        var modpack = InstalledModpacks.FirstOrDefault(pack => string.Equals(pack.Name, value.Id, StringComparison.OrdinalIgnoreCase));
        NewServerModpackName = modpack?.Name ?? value.Label;
        NewServerProjectId = modpack?.ProjectId ?? 0;
    }

    private void RebuildNewServerModpackOptions()
    {
        var selectedId = SelectedNewServerModpackOption?.Id;

        NewServerModpackOptions.Clear();
        foreach (var modpack in InstalledModpacks.OrderBy(modpack => modpack.Name, StringComparer.OrdinalIgnoreCase))
        {
            NewServerModpackOptions.Add(new SelectionOption
            {
                Id = modpack.Name,
                Label = modpack.Name
            });
        }

        SelectedNewServerModpackOption = NewServerModpackOptions.FirstOrDefault(option => option.Id == selectedId)
            ?? NewServerModpackOptions.FirstOrDefault(option => option.Id == CurrentModpack?.Name)
            ?? NewServerModpackOptions.FirstOrDefault();

        RefreshServerLinkedMetadata();
    }

    [RelayCommand]
    private async Task RefreshDiscoveredServers()
    {
        await RefreshAutoDiscoveredServersAsync();
        ShowToast("Server Hub", "Detekce serverů z nainstalovaných instancí byla obnovena.", ToastSeverity.Success, 2500);
    }

    private async Task RefreshAutoDiscoveredServersAsync()
    {
        if (_serverDiscoveryService == null)
        {
            return;
        }

        var discoveredServers = _serverDiscoveryService.DiscoverInstalledServers(InstalledModpacks);
        var newlyAdded = new List<ServerInfo>();

        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            foreach (var discovered in Servers.Where(server => server.IsAutoDiscovered).ToList())
            {
                Servers.Remove(discovered);
            }

            foreach (var discovered in discoveredServers)
            {
                if (Servers.Any(existing =>
                        string.Equals(existing.Address, discovered.Address, StringComparison.OrdinalIgnoreCase) &&
                        existing.Port == discovered.Port))
                {
                    continue;
                }

                Servers.Add(discovered);
                newlyAdded.Add(discovered);
            }
        });

        if (_serverStatusService != null)
        {
            foreach (var server in newlyAdded)
            {
                await _serverStatusService.PollAsync(server);
            }
        }

        RefreshServerLinkedMetadata();
    }

    private void RefreshServerLinkedMetadata()
    {
        foreach (var server in Servers)
        {
            ApplyLinkedModpackMetadata(server);
        }

        OnPropertyChanged(nameof(TotalServerCount));
        OnPropertyChanged(nameof(CreatorStudioLinkedServersLabel));
        NotifyStreamingToolsStateChanged();
    }

    private void ApplyLinkedModpackMetadata(ServerInfo server)
    {
        var modpack = InstalledModpacks.FirstOrDefault(m =>
            (server.LinkedModpackProjectId > 0 && m.ProjectId == server.LinkedModpackProjectId) ||
            ArePackNamesEquivalent(m.Name, server.LinkedModpackName));

        if (modpack == null)
        {
            server.LinkedModCount = 0;
            return;
        }

        if (string.IsNullOrWhiteSpace(server.LinkedModpackName))
        {
            server.LinkedModpackName = modpack.Name;
        }

        if (string.IsNullOrWhiteSpace(server.IconUrl) && !string.IsNullOrWhiteSpace(modpack.LogoUrl))
        {
            server.IconUrl = modpack.LogoUrl;
        }

        var manifest = TryLoadManifestInfo(modpack);
        if (manifest != null)
        {
            if (!string.IsNullOrWhiteSpace(manifest.MinecraftVersion))
            {
                server.RequiredMcVersion = manifest.MinecraftVersion;
            }

            if (!string.IsNullOrWhiteSpace(manifest.ModLoaderType))
            {
                server.RequiredModLoader = manifest.ModLoaderType;
            }
        }
        else if (modpack.IsCustomProfile)
        {
            if (!string.IsNullOrWhiteSpace(modpack.CustomMcVersion))
            {
                server.RequiredMcVersion = modpack.CustomMcVersion;
            }

            if (!string.IsNullOrWhiteSpace(modpack.CustomModLoader))
            {
                server.RequiredModLoader = modpack.CustomModLoader;
            }
        }

        server.LinkedModCount = GetInstalledModCount(modpack);
    }

    private ModpackManifestInfo? TryLoadManifestInfo(ModpackInfo? modpack)
    {
        if (modpack == null || string.IsNullOrWhiteSpace(modpack.Name))
        {
            return null;
        }

        var instancePath = _launcherService.GetModpackPath(modpack.Name);
        if (!Directory.Exists(instancePath))
        {
            return null;
        }

        try
        {
            return ModpackInstaller.LoadManifestInfo(instancePath);
        }
        catch
        {
            return null;
        }
    }

    private int GetInstalledModCount(ModpackInfo modpack)
    {
        var manifestCount = TryLoadManifestInfo(modpack)?.ModCount ?? 0;
        if (manifestCount > 0)
        {
            return manifestCount;
        }

        var modsPath = Path.Combine(_launcherService.GetModpackPath(modpack.Name), "mods");
        if (!Directory.Exists(modsPath))
        {
            return 0;
        }

        return Directory.EnumerateFiles(modsPath, "*.jar", SearchOption.TopDirectoryOnly).Count();
    }

    private static string NormalizePackName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return new string(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    }

    private static bool ArePackNamesEquivalent(string? left, string? right)
    {
        return NormalizePackName(left) == NormalizePackName(right);
    }

    private bool IsServerLinkedToCurrentModpack(ServerInfo server)
    {
        if (CurrentModpack == null) return false;

        if (server.LinkedModpackProjectId > 0 && CurrentModpack.ProjectId > 0)
            return server.LinkedModpackProjectId == CurrentModpack.ProjectId;

        return ArePackNamesEquivalent(server.LinkedModpackName, CurrentModpack.Name);
    }

    private static InstanceBackupSnapshot CreateBackupSnapshot(string backupPath)
    {
        var createdAt = Directory.GetCreationTime(backupPath);
        var includedParts = new List<string>();
        if (Directory.Exists(Path.Combine(backupPath, "config"))) includedParts.Add("config");
        if (Directory.Exists(Path.Combine(backupPath, "saves"))) includedParts.Add("světy");
        if (Directory.Exists(Path.Combine(backupPath, "shaderpacks"))) includedParts.Add("shadery");
        if (File.Exists(Path.Combine(backupPath, "options.txt"))) includedParts.Add("options");
        if (File.Exists(Path.Combine(backupPath, "servers.dat"))) includedParts.Add("server list");

        return new InstanceBackupSnapshot
        {
            Name = Path.GetFileName(backupPath).Replace(".config_backup_", "Snapshot "),
            FullPath = backupPath,
            CreatedAt = createdAt,
            FileCount = Directory.GetFiles(backupPath, "*", SearchOption.AllDirectories).Length,
            Summary = includedParts.Count > 0 ? string.Join(", ", includedParts) : "konfigurace instance"
        };
    }

    private IEnumerable<string> GetBackupSnapshotDirectories(string modpackName, string modpackPath)
    {
        var snapshotDirectories = new List<string>();

        if (Directory.Exists(modpackPath))
        {
            snapshotDirectories.AddRange(Directory.GetDirectories(modpackPath, ".config_backup_*", SearchOption.TopDirectoryOnly));
        }

        var persistentBackupRoot = GetPersistentBackupRoot(modpackName);
        if (Directory.Exists(persistentBackupRoot))
        {
            snapshotDirectories.AddRange(Directory.GetDirectories(persistentBackupRoot, ".config_backup_*", SearchOption.TopDirectoryOnly));
        }

        return snapshotDirectories
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(Directory.Exists);
    }

    private async Task RefreshCurrentModpackWorkspaceDataAsync()
    {
        if (CurrentModpack == null || string.IsNullOrWhiteSpace(CurrentModpack.Name))
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                CurrentModpackBackupSnapshots.Clear();
                CurrentModpackLinkedServers.Clear();
                OnPropertyChanged(nameof(HasCurrentModpackBackups));
                OnPropertyChanged(nameof(HasCurrentModpackLinkedServers));
            });
            return;
        }

        var modpackPath = _launcherService.GetModpackPath(CurrentModpack.Name);
        var backupSnapshots = GetBackupSnapshotDirectories(CurrentModpack.Name, modpackPath)
            .Select(CreateBackupSnapshot)
            .OrderByDescending(snapshot => snapshot.CreatedAt)
            .ToList();

        var linkedServers = Servers
            .Where(IsServerLinkedToCurrentModpack)
            .OrderByDescending(server => server.IsPinned)
            .ThenBy(server => server.Name)
            .ToList();

        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            CurrentModpackBackupSnapshots.Clear();
            foreach (var snapshot in backupSnapshots)
                CurrentModpackBackupSnapshots.Add(snapshot);

            CurrentModpackLinkedServers.Clear();
            foreach (var server in linkedServers)
                CurrentModpackLinkedServers.Add(server);

            OnPropertyChanged(nameof(HasCurrentModpackBackups));
            OnPropertyChanged(nameof(HasCurrentModpackLinkedServers));
        });
    }

    // ╔═══════════════════════════════════════╗
    // ║          SOCIAL FEED                  ║
    // ╚═══════════════════════════════════════╝

    [RelayCommand]
    private async Task RefreshNewsFeed()
    {
        await LoadSocialFeedAsync(forceRefresh: true);
    }

    private async Task LoadSocialFeedAsync(bool forceRefresh = false)
    {
        if (_socialFeedService == null) return;

        using var operation = _observabilityService?.BeginOperation("SocialFeed.Load");
        var shouldShowLoading = NewsFeed.Count == 0;
        if (shouldShowLoading)
            IsFeedLoading = true;
        try
        {
            var items = await _socialFeedService.GetFeedAsync(forceRefresh);

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                ApplySocialFeedItems(items);
            });

            operation?.Complete();
        }
        catch (Exception ex)
        {
            LogService.Error("Failed to load social feed", ex);
            operation?.Fail(ex.Message);
        }
        finally
        {
            IsFeedLoading = false;
        }
    }

    private void ApplySocialFeedItems(IEnumerable<FeedItem> items)
    {
        NewsFeed.Clear();
        foreach (var item in items)
            NewsFeed.Add(item);

        FeaturedMinecraftNews = NewsFeed.FirstOrDefault(item => item.Source == FeedSource.Minecraft);
        ApplyFeedFilter();
        OnPropertyChanged(nameof(HasFeaturedMinecraftNews));
    }

    [RelayCommand]
    private void SetFeedFilter(string filter)
    {
        ActiveFeedFilter = filter;
        ApplyFeedFilter();
    }

    private void ApplyFeedFilter()
    {
        FilteredNewsFeed.Clear();
        var source = ActiveFeedFilter switch
        {
            "Discord" => NewsFeed.Where(f => f.Source == FeedSource.Discord),
            "YouTube" => NewsFeed.Where(f => f.Source == FeedSource.YouTube),
            "Minecraft" => NewsFeed.Where(f => f.Source == FeedSource.Minecraft),
            _ => NewsFeed.AsEnumerable()
        };
        foreach (var item in source)
            FilteredNewsFeed.Add(item);

        OnPropertyChanged(nameof(HasFeedItems));
    }

    [RelayCommand]
    private void OpenFeedItem(FeedItem? item)
    {
        if (item == null) return;

        if (!string.IsNullOrWhiteSpace(item.ExternalUrl))
        {
            OpenUrl(item.ExternalUrl);
            return;
        }

        var fallbackUrl = item.Source switch
        {
            FeedSource.Discord => "https://discord.gg/cFvCaC2KDh",
            FeedSource.YouTube => "https://www.youtube.com/@void-craft-eu",
            FeedSource.Minecraft => "https://www.minecraft.net/en-us/articles",
            _ => null
        };

        if (!string.IsNullOrWhiteSpace(fallbackUrl))
        {
            OpenUrl(fallbackUrl);
            return;
        }

        ShowToast("Novinka", "Tato položka zatím nemá veřejný odkaz.", ToastSeverity.Info, 2500);
    }

    [RelayCommand]
    private void OpenAddServer()
    {
        ResetNewServerForm();
        IsAddServerSheetOpen = true;
    }

    [RelayCommand]
    private void CloseAddServer()
    {
        IsAddServerSheetOpen = false;
        ResetNewServerForm();
    }

    [RelayCommand]
    private async Task SaveCustomServer()
    {
        if (string.IsNullOrWhiteSpace(NewServerName) || string.IsNullOrWhiteSpace(NewServerAddress))
        {
            ShowToast("Server Hub", "Vyplň název a adresu serveru.", ToastSeverity.Warning);
            return;
        }

        if (Servers.Any(server =>
                string.Equals(server.Address, NewServerAddress, StringComparison.OrdinalIgnoreCase) &&
                server.Port == NewServerPort))
        {
            ShowToast("Server Hub", "Server se stejnou adresou už existuje.", ToastSeverity.Warning);
            return;
        }

        var serverInfo = new ServerInfo
        {
            Name = NewServerName.Trim(),
            Address = NewServerAddress.Trim(),
            Port = NewServerPort,
            LinkedModpackName = string.IsNullOrWhiteSpace(NewServerModpackName) ? null : NewServerModpackName.Trim(),
            LinkedModpackProjectId = NewServerProjectId,
            AutoConnect = NewServerAutoConnect,
            StatusText = "Načítám..."
        };

        Servers.Add(serverInfo);
        IsAddServerSheetOpen = false;
        ResetNewServerForm();

        if (_serverStatusService != null)
            await _serverStatusService.PollAsync(serverInfo);

        ShowToast("Server Hub", $"Server {serverInfo.Name} byl přidán.", ToastSeverity.Success, 2500);
    }

    [RelayCommand]
    private void RemoveServer(ServerInfo? server)
    {
        if (server == null || server.IsPinned) return;

        Servers.Remove(server);
        ShowToast("Server Hub", $"Server {server.Name} byl odebrán.", ToastSeverity.Info, 2500);
    }

    // ╔═══════════════════════════════════════╗
    // ║       SERVER QUICK CONNECT            ║
    // ╚═══════════════════════════════════════╝

    /// <summary>
    /// Quick Connect flow: ensures the linked modpack is installed, then launches
    /// with --server flag for auto-connect.
    /// </summary>
    [RelayCommand]
    private async Task QuickConnect(ServerInfo? server)
    {
        using var operation = _observabilityService?.BeginOperation("QuickConnect");

        if (server == null)
        {
            operation?.Fail("Server missing");
            return;
        }

        if (server.LinkedModpackProjectId <= 0 && string.IsNullOrWhiteSpace(server.LinkedModpackName))
        {
            ShowToast("Quick Connect", "K tomuto serveru ještě není navázaný modpack.", ToastSeverity.Warning);
            operation?.Fail("Server has no linked modpack");
            return;
        }

        try
        {
            // Find or select the linked modpack
            var modpack = InstalledModpacks.FirstOrDefault(m =>
                m.ProjectId == server.LinkedModpackProjectId ||
                ArePackNamesEquivalent(m.Name, server.LinkedModpackName));

            if (modpack == null && server.LinkedModpackProjectId > 0)
            {
                _observabilityService?.RecordFallback("QuickConnect", "installed-modpack", "install-on-demand", "Linked modpack was not installed locally");
                ShowToast("Quick Connect", "Modpack " + server.LinkedModpackName + " není nainstalován. Spustí se stažení.", ToastSeverity.Info);

                CurrentModpack = new ModpackInfo
                {
                    ProjectId = server.LinkedModpackProjectId,
                    Name = server.LinkedModpackName ?? "Server Modpack"
                };
            }
            else if (modpack != null)
            {
                CurrentModpack = modpack;
            }

            if (CurrentModpack == null)
            {
                operation?.Fail("Unable to resolve modpack for quick connect");
                return;
            }

            // Store auto-connect info for post-launch injection
            _pendingAutoConnect = server.AutoConnect ? server : null;

            ShowToast("Quick Connect", $"Spouštím {CurrentModpack.Name} a připravuji připojení na {server.Address}.", ToastSeverity.Info, 2500);

            // Launch
            await PlayModpack();
            operation?.Complete();
        }
        catch (Exception ex)
        {
            LogService.Error("QuickConnect failed", ex);
            ShowToast("Quick Connect", "Nepodařilo se spustit server quick connect.", ToastSeverity.Error);
            operation?.Fail(ex.Message);
        }
    }

    private ServerInfo? _pendingAutoConnect;

    /// <summary>
    /// If a server auto-connect is pending, writes server.dat or injects --server JVM arg.
    /// Call this right before game process starts.
    /// </summary>
    public string[]? GetAutoConnectArgs()
    {
        if (_pendingAutoConnect == null) return null;

        var server = _pendingAutoConnect;
        _pendingAutoConnect = null;

        TryPrepareAutoConnectServerEntry(server);

        var endpoint = server.Port == 25565 ? server.Address : $"{server.Address}:{server.Port}";

        var args = new List<string>();
        if (CurrentInstanceSupportsQuickPlay())
        {
            args.Add("--quickPlayPath");
            args.Add("multiplayer");
            args.Add("--quickPlayMultiplayer");
            args.Add(endpoint);
        }

        args.Add("--server");
        args.Add(server.Address);
        if (server.Port != 25565)
        {
            args.Add("--port");
            args.Add(server.Port.ToString());
        }
        return args.ToArray();
    }

    private bool CurrentInstanceSupportsQuickPlay()
    {
        if (CurrentModpack == null)
            return false;

        var version = TryLoadManifestInfo(CurrentModpack)?.MinecraftVersion;
        if (string.IsNullOrWhiteSpace(version) && CurrentModpack.IsCustomProfile)
            version = CurrentModpack.CustomMcVersion;

        return Version.TryParse(version, out var parsedVersion) && parsedVersion >= new Version(1, 20);
    }

    private void TryPrepareAutoConnectServerEntry(ServerInfo server)
    {
        if (CurrentModpack == null || string.IsNullOrWhiteSpace(CurrentModpack.Name))
            return;

        try
        {
            var instancePath = _launcherService.GetModpackPath(CurrentModpack.Name);
            var serversDatPath = Path.Combine(instancePath, "servers.dat");
            var endpoint = server.Port == 25565 ? server.Address : $"{server.Address}:{server.Port}";

            var file = new NbtFile();
            NbtCompound rootTag;
            NbtList existingServers;

            if (File.Exists(serversDatPath))
            {
                file.LoadFromFile(serversDatPath);
                rootTag = file.RootTag;
                existingServers = rootTag["servers"] as NbtList ?? new NbtList("servers", NbtTagType.Compound);
            }
            else
            {
                rootTag = new NbtCompound("") { new NbtList("servers", NbtTagType.Compound) };
                file.RootTag = rootTag;
                existingServers = (NbtList)rootTag["servers"]!;
            }

            var updatedServers = new NbtList("servers", NbtTagType.Compound)
            {
                new NbtCompound
                {
                    new NbtString("name", server.Name),
                    new NbtString("ip", endpoint),
                    new NbtByte("acceptTextures", 1)
                }
            };

            foreach (var tag in existingServers)
            {
                if (tag is not NbtCompound existingServer)
                    continue;

                var existingEndpoint = existingServer.Get<NbtString>("ip")?.Value?.Trim();
                if (string.IsNullOrWhiteSpace(existingEndpoint) || string.Equals(existingEndpoint, endpoint, StringComparison.OrdinalIgnoreCase))
                    continue;

                updatedServers.Add(existingServer);
            }

            rootTag.Remove("servers");
            rootTag.Add(updatedServers);
            file.SaveToFile(serversDatPath, NbtCompression.GZip);
        }
        catch (Exception ex)
        {
            LogService.Error("QuickConnect failed to update servers.dat", ex);
        }
    }

    /// <summary>
    /// Copy server IP to clipboard.
    /// </summary>
    [RelayCommand]
    private async Task CopyServerIp(ServerInfo? server)
    {
        if (server == null) return;
        var ip = server.Port == 25565 ? server.Address : $"{server.Address}:{server.Port}";

        var clipboard = Avalonia.Application.Current?.ApplicationLifetime is
            Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow?.Clipboard : null;

        if (clipboard != null)
        {
            await clipboard.SetTextAsync(ip);
            ShowToast("Zkopírováno", $"{ip} je ve schránce.", ToastSeverity.Success, 2000);
        }
    }

    // ╔═══════════════════════════════════════╗
    // ║     COMPATIBILITY MATRIX              ║
    // ╚═══════════════════════════════════════╝

    /// <summary>
    /// Checks whether an installed modpack is compatible with a server's requirements.
    /// </summary>
    public bool IsModpackCompatible(ModpackInfo modpack, ServerInfo server)
    {
        if (string.IsNullOrEmpty(server.RequiredMcVersion)) return true;

        var instancePath = _launcherService.GetModpackPath(modpack.Name);
        var manifest = ModpackInstaller.LoadManifestInfo(instancePath);
        if (manifest == null) return false;

        var versionMatch = string.Equals(manifest.MinecraftVersion, server.RequiredMcVersion, StringComparison.OrdinalIgnoreCase);
        var loaderMatch = string.IsNullOrEmpty(server.RequiredModLoader) ||
                          (manifest.ModLoaderId?.Contains(server.RequiredModLoader, StringComparison.OrdinalIgnoreCase) ?? false);

        return versionMatch && loaderMatch;
    }

    // ╔═══════════════════════════════════════╗
    // ║       EXPORT / IMPORT                 ║
    // ╚═══════════════════════════════════════╝

    [RelayCommand]
    private async Task ExportInstance()
    {
        if (CurrentModpack == null || _instanceExportService == null) return;

        var instancePath = _launcherService.GetModpackPath(CurrentModpack.Name);
        if (!Directory.Exists(instancePath))
        {
            ShowToast("Export", "Instance adresář nenalezen.", ToastSeverity.Warning);
            return;
        }

        var defaultName = $"{CurrentModpack.Name}_{DateTime.Now:yyyyMMdd_HHmm}.voidpack";
        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var outputPath = Path.Combine(desktopPath, defaultName);

        IsExporting = true;
        ExportProgress = 0;
        ShowToast("Export", "Exportuji instanci " + CurrentModpack.Name + "...", ToastSeverity.Info);

        try
        {
            await _instanceExportService.ExportAsync(
                instancePath,
                CurrentModpack.Name,
                outputPath,
                progress: p => Avalonia.Threading.Dispatcher.UIThread.Post(() => ExportProgress = p * 100));

            ShowToast("Export dokončen", $"Uloženo na plochu: {defaultName}", ToastSeverity.Success, 5000);
        }
        catch (Exception ex)
        {
            LogService.Error("Export failed", ex);
            ShowToast("Export selhal", ex.Message, ToastSeverity.Error);
        }
        finally
        {
            IsExporting = false;
        }
    }

    [RelayCommand]
    private async Task BrowseImportArchive()
    {
        var storageProvider = (Avalonia.Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)
            ?.MainWindow?.StorageProvider;

        if (storageProvider == null)
        {
            ShowToast("Import", "Souborový picker není v tomto režimu dostupný.", ToastSeverity.Warning);
            return;
        }

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Vyber .voidpack archiv",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("VOID-CRAFT Instance Pack")
                {
                    Patterns = new[] { "*.voidpack" }
                }
            }
        });

        var selectedPath = files.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(selectedPath))
            ImportArchivePath = selectedPath;
    }

    [RelayCommand]
    private void ClearImportArchive()
    {
        ImportArchivePath = "";
    }

    [RelayCommand]
    private async Task CreateInstanceBackup()
    {
        if (CurrentModpack == null)
            return;

        var instancePath = _launcherService.GetModpackPath(CurrentModpack.Name);
        if (!Directory.Exists(instancePath))
        {
            ShowToast("Snapshot", "Adresář instance nebyl nalezen.", ToastSeverity.Warning);
            return;
        }

        try
        {
            var backupPath = await CreatePersistentBackupSnapshotAsync(CurrentModpack, "manual-snapshot");
            if (backupPath == null)
            {
                ShowToast("Snapshot", "V instanci nejsou žádná uživatelská data vhodná k záloze.", ToastSeverity.Info, 2500);
            }
        }
        catch (Exception ex)
        {
            LogService.Error("Creating instance backup failed", ex);
            ShowToast("Snapshot selhal", ex.Message, ToastSeverity.Error);
        }
    }

    [RelayCommand]
    private void OpenBackupSnapshot(InstanceBackupSnapshot? snapshot)
    {
        if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.FullPath) || !Directory.Exists(snapshot.FullPath))
            return;

        Process.Start(new ProcessStartInfo
        {
            FileName = snapshot.FullPath,
            UseShellExecute = true,
            Verb = "open"
        });
    }

    [RelayCommand]
    private void OpenSavesFolder()
    {
        if (CurrentModpack == null)
            return;

        var savesPath = Path.Combine(_launcherService.GetModpackPath(CurrentModpack.Name), "saves");
        Directory.CreateDirectory(savesPath);

        Process.Start(new ProcessStartInfo
        {
            FileName = savesPath,
            UseShellExecute = true,
            Verb = "open"
        });
    }

    [RelayCommand]
    private async Task RefreshInstanceWorkspaceData()
    {
        await RefreshCurrentModpackWorkspaceDataAsync();
    }

    [RelayCommand]
    private async Task ImportInstance(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath) || _instanceExportService == null) return;

        if (!File.Exists(filePath) || !filePath.EndsWith(".voidpack", StringComparison.OrdinalIgnoreCase))
        {
            ShowToast("Import", "Neplatný soubor. Očekáván .voidpack.", ToastSeverity.Warning);
            return;
        }

        IsImporting = true;
        ImportProgress = 0;
        ShowToast("Import", "Importuji instanci...", ToastSeverity.Info);

        try
        {
            // Determine instance name from the archive
            var tempName = Path.GetFileNameWithoutExtension(filePath);
            var instancePath = _launcherService.GetModpackPath(tempName);

            var manifest = await _instanceExportService.ImportAsync(
                filePath,
                instancePath,
                progress: p => Avalonia.Threading.Dispatcher.UIThread.Post(() => ImportProgress = p * 100));

            if (manifest != null)
            {
                var importedName = string.IsNullOrWhiteSpace(manifest.InstanceName) ? tempName : manifest.InstanceName;

                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var existing = InstalledModpacks.FirstOrDefault(m => string.Equals(m.Name, importedName, StringComparison.OrdinalIgnoreCase));
                    if (existing == null)
                    {
                        InstalledModpacks.Add(new ModpackInfo
                        {
                            Name = importedName,
                            Source = "Custom",
                            Author = "VOID-CRAFT Import",
                            Description = "Importováno z .voidpack archivu.",
                            IsCustomProfile = true,
                            IsDeletable = true,
                            CustomMcVersion = manifest.MinecraftVersion,
                            CustomModLoader = manifest.ModLoader,
                            CurrentVersion = new ModpackVersion
                            {
                                Name = string.IsNullOrWhiteSpace(manifest.MinecraftVersion) ? "Import" : manifest.MinecraftVersion
                            }
                        });
                    }
                });

                ImportArchivePath = "";
                ShowToast("Import dokončen", "Instance " + manifest.InstanceName + " importována.", ToastSeverity.Success, 5000);
            }
        }
        catch (Exception ex)
        {
            LogService.Error("Import failed", ex);
            ShowToast("Import selhal", ex.Message, ToastSeverity.Error);
        }
        finally
        {
            IsImporting = false;
            await RefreshCurrentModpackWorkspaceDataAsync();
        }
    }
}
