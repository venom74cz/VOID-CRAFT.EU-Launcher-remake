using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using VoidCraftLauncher.Models;
using VoidCraftLauncher.Services;

namespace VoidCraftLauncher.ViewModels;

public partial class MainViewModel
{
    public ObservableCollection<CreatorWorkbenchFile> CreatorWorkbenchFiles { get; } = new();

    public ObservableCollection<CreatorWorkbenchFile> FilteredCreatorWorkbenchFiles { get; } = new();

    [ObservableProperty]
    private CreatorWorkbenchFile? _selectedCreatorWorkbenchFile;

    [ObservableProperty]
    private bool _isCreatorWorkbenchLoading;

    [ObservableProperty]
    private bool _isCreatorWorkbenchSaving;

    [ObservableProperty]
    private string _creatorWorkbenchContent = string.Empty;

    [ObservableProperty]
    private string _creatorWorkbenchSearchQuery = string.Empty;

    [ObservableProperty]
    private string _creatorWorkbenchStatus = "Vyber pracovní instanci a otevři soubor k úpravě.";

    private string _creatorWorkbenchLoadedContent = string.Empty;

    public string CreatorStudioInstanceLabel => GetCreatorStudioSelectedModpack()?.DisplayLabel ?? SelectedSkinStudioInstance?.Label ?? "Bez zvolené instance";

    public string CreatorStudioInstancePath => SkinStudioSelectedInstancePath;

    public bool HasCreatorStudioInstances => HasSkinStudioInstances;

    public bool HasCreatorWorkbenchFiles => CreatorWorkbenchFiles.Count > 0;

    public bool HasVisibleCreatorWorkbenchFiles => FilteredCreatorWorkbenchFiles.Count > 0;

    public bool HasSelectedCreatorWorkbenchFile => SelectedCreatorWorkbenchFile != null;

    public string CreatorWorkbenchSearchSummary => string.IsNullOrWhiteSpace(CreatorWorkbenchSearchQuery)
        ? (HasCreatorWorkbenchFiles ? $"{FilteredCreatorWorkbenchFiles.Count} souborů k úpravě" : "Žádné soubory k úpravě")
        : (HasVisibleCreatorWorkbenchFiles
            ? $"{FilteredCreatorWorkbenchFiles.Count} výsledků pro \"{CreatorWorkbenchSearchQuery.Trim()}\""
            : $"Žádný soubor neodpovídá \"{CreatorWorkbenchSearchQuery.Trim()}\"");

    public string CreatorWorkbenchEmptyTitle => HasCreatorWorkbenchFiles
        ? "Filtr nenašel žádný soubor"
        : "Žádné editorové soubory";

    public string CreatorWorkbenchEmptySubtitle => HasCreatorWorkbenchFiles
        ? "Zkus jiný název souboru, příponu nebo část cesty."
        : "Ve vybrané instanci zatím nebyl nalezen vhodný malý textový soubor.";

    public string CreatorWorkbenchSelectionLabel => SelectedCreatorWorkbenchFile?.RelativePath ?? "Žádný otevřený soubor";

    public string CreatorWorkbenchSelectionMeta => SelectedCreatorWorkbenchFile == null
        ? "Vyber soubor z levého seznamu."
        : $"{SelectedCreatorWorkbenchFile.Category} • {CreatorWorkbenchDocumentKindLabel} • {SelectedCreatorWorkbenchFile.SizeLabel}";

    public string CreatorWorkbenchBreadcrumb => SelectedCreatorWorkbenchFile == null
        ? "workspace / vyber soubor"
        : string.Join(" / ", SelectedCreatorWorkbenchFile.RelativePath
            .Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries));

    public string CreatorWorkbenchDirtyStateLabel => !HasSelectedCreatorWorkbenchFile
        ? "Bez souboru"
        : CanSaveCreatorWorkbenchFile
            ? "Neulozeno"
            : "Ulozeno";

    public bool CanSaveCreatorWorkbenchFile =>
        IsCreatorWorkspaceEditable &&
        SelectedCreatorWorkbenchFile != null &&
        !IsCreatorWorkbenchLoading &&
        !IsCreatorWorkbenchSaving &&
        !string.Equals(_creatorWorkbenchLoadedContent, CreatorWorkbenchContent ?? string.Empty, StringComparison.Ordinal);

    public string CreatorStudioMinecraftVersion => CreatorWorkspaceContext.MinecraftVersion;

    public string CreatorStudioModLoader => CreatorWorkspaceContext.LoaderLabel;

    public string CreatorStudioModCountLabel
    {
        get
        {
            return CreatorWorkspaceContext.ModCount > 0
                ? $"{CreatorWorkspaceContext.ModCount} modů"
                : "Počet modů není k dispozici";
        }
    }

    public string CreatorStudioLinkedServersLabel => CreatorWorkspaceContext.LinkedServersLabel;

    public bool CanManageCreatorModsInPlace => ResolveModManagementTargetModpack()?.IsCustomProfile == true && IsCreatorWorkspaceEditable;

    public string CreatorModsWorkspaceStatus => !HasCreatorWorkspaceContext
        ? "Nejdřív vyber pracovní workspace, pak se načte kompletní správa modů."
        : CanManageCreatorModsInPlace
            ? "V tomhle tabu řešíš vyhledání, lokální .jar, multi-select, zapínání i mazání modů bez dalšího manageru."
            : "Vybraný workspace je jen pro čtení. Přehled modů uvidíš tady, ale měnit je můžeš jen v custom/dev workspace.";

    public string CreatorModsFolderPath
    {
        get
        {
            var targetPath = ResolveModManagementTargetPath();
            return string.IsNullOrWhiteSpace(targetPath)
                ? string.Empty
                : Path.Combine(targetPath, "mods");
        }
    }

    public string CreatorInstalledModsSummary => string.IsNullOrWhiteSpace(InstalledModsSearchQuery)
        ? HasSelectedInstalledMods
            ? $"{FilteredInstalledMods.Count} modů v aktuálním workspace • vybráno {SelectedInstalledModsCount}"
            : $"{FilteredInstalledMods.Count} modů v aktuálním workspace"
        : HasSelectedInstalledMods
            ? $"{FilteredInstalledMods.Count}/{InstalledMods.Count} modů po filtru • vybráno {SelectedInstalledModsCount}"
            : $"{FilteredInstalledMods.Count}/{InstalledMods.Count} modů po filtru";

    public string CreatorModsRuntimeLabel => string.IsNullOrWhiteSpace(ProfileModSearchRuntimeLabel)
        ? BuildModSearchRuntimeLabel(ResolveModManagementTargetModpack())
        : ProfileModSearchRuntimeLabel;

    public string CreatorModsCatalogModeLabel => IsProfileModSearchLoading
        ? "Hledání"
        : ProfileModSearchUsedFallback
            ? "Širší katalog"
            : "Kompatibilní katalog";

    public string CreatorModsSearchActionLabel => IsProfileModSearchLoading ? "Hledám..." : "Vyhledat";

    public string CreatorModsSearchResultsSummary => string.IsNullOrWhiteSpace(ProfileModSearchQuery)
        ? "Vyhledávání bere CurseForge i Modrinth. Začíná přesnou kompatibilitou pro vybraný runtime a když je prázdná, zkusí širší katalog."
        : IsProfileModSearchLoading
            ? $"Hledám \"{ProfileModSearchQuery.Trim()}\" napříč katalogy..."
            : HasProfileModSearchResults
                ? ProfileModSearchUsedFallback
                    ? $"{ProfileModSearchResults.Count} výsledků pro \"{ProfileModSearchQuery.Trim()}\" po rozšíření mimo přesný runtime filtr."
                    : $"{ProfileModSearchResults.Count} kompatibilních výsledků pro \"{ProfileModSearchQuery.Trim()}\""
                : $"Pro dotaz \"{ProfileModSearchQuery.Trim()}\" se nenašlo nic ani po rozšíření hledání.";

    public bool ShowCreatorModsSearchEmptyState =>
        !IsProfileModSearchLoading &&
        !string.IsNullOrWhiteSpace(ProfileModSearchQuery) &&
        !HasProfileModSearchResults;

    public bool IsDiscordPresenceReady => _discordRpcService.IsInitialized;

    public string DiscordPresenceDetails => string.IsNullOrWhiteSpace(_discordRpcService.CurrentDetails)
        ? "VOID-CRAFT Launcher"
        : _discordRpcService.CurrentDetails;

    public string DiscordPresenceState => string.IsNullOrWhiteSpace(_discordRpcService.CurrentState)
        ? "V hlavní nabídce"
        : _discordRpcService.CurrentState;

    public string StreamingSessionHeadline => IsGameRunning
        ? $"Ve hře: {RunningModpack?.Name ?? "Minecraft"}"
        : IsLaunching
            ? "Launcher připravuje herní session"
            : "Creator workflow je připraven";

    public string StreamingSessionStatus => IsGameRunning
        ? "Hra běží. Creator Studio udržuje pracovní kontext nad vybranou instancí."
        : IsLaunching
            ? (string.IsNullOrWhiteSpace(LaunchStatus) ? "Spouštění probíhá." : LaunchStatus)
            : "Vyber pracovní instanci a spravuj soubory, screenshoty, logy a serverové vazby.";

    public string StreamingContextModpackName => (RunningModpack ?? CurrentModpack)?.Name ?? "Žádný aktivní modpack";

    public string StreamingAccountLabel => IsLoggedIn
        ? (string.IsNullOrWhiteSpace(UserSession?.Username) ? "Přihlášený účet" : UserSession.Username)
        : "Offline / Guest režim";

    public bool HasStreamingContextModpack => RunningModpack != null || CurrentModpack != null;

    private ModpackInfo? GetStreamingContextModpack() => RunningModpack ?? CurrentModpack;

    private void NotifyStreamingToolsStateChanged()
    {
        OnPropertyChanged(nameof(IsDiscordPresenceReady));
        OnPropertyChanged(nameof(DiscordPresenceDetails));
        OnPropertyChanged(nameof(DiscordPresenceState));
        OnPropertyChanged(nameof(StreamingSessionHeadline));
        OnPropertyChanged(nameof(StreamingSessionStatus));
        OnPropertyChanged(nameof(StreamingContextModpackName));
        OnPropertyChanged(nameof(StreamingAccountLabel));
        OnPropertyChanged(nameof(HasStreamingContextModpack));
        OnPropertyChanged(nameof(CreatorStudioInstanceLabel));
        OnPropertyChanged(nameof(CreatorStudioInstancePath));
        OnPropertyChanged(nameof(HasCreatorStudioInstances));
        OnPropertyChanged(nameof(CreatorStudioMinecraftVersion));
        OnPropertyChanged(nameof(CreatorStudioModLoader));
        OnPropertyChanged(nameof(CreatorStudioModCountLabel));
        OnPropertyChanged(nameof(CreatorStudioLinkedServersLabel));
        OnPropertyChanged(nameof(CanManageCreatorModsInPlace));
        OnPropertyChanged(nameof(CreatorModsWorkspaceStatus));
        OnPropertyChanged(nameof(CreatorModsFolderPath));
        OnPropertyChanged(nameof(CreatorInstalledModsSummary));
        OnPropertyChanged(nameof(CreatorModsRuntimeLabel));
        OnPropertyChanged(nameof(CreatorModsCatalogModeLabel));
        OnPropertyChanged(nameof(CreatorModsSearchActionLabel));
        OnPropertyChanged(nameof(CreatorModsSearchResultsSummary));
        OnPropertyChanged(nameof(ShowCreatorModsSearchEmptyState));
        OnPropertyChanged(nameof(HasCreatorWorkbenchFiles));
        OnPropertyChanged(nameof(HasVisibleCreatorWorkbenchFiles));
        OnPropertyChanged(nameof(HasSelectedCreatorWorkbenchFile));
        OnPropertyChanged(nameof(CreatorWorkbenchSearchSummary));
        OnPropertyChanged(nameof(CreatorWorkbenchEmptyTitle));
        OnPropertyChanged(nameof(CreatorWorkbenchEmptySubtitle));
        OnPropertyChanged(nameof(CreatorWorkbenchSelectionLabel));
        OnPropertyChanged(nameof(CreatorWorkbenchSelectionMeta));
        OnPropertyChanged(nameof(CreatorWorkbenchBreadcrumb));
        OnPropertyChanged(nameof(CreatorWorkbenchDirtyStateLabel));
        OnPropertyChanged(nameof(CanSaveCreatorWorkbenchFile));
        NotifyCreatorWorkbenchEditorVisualStateChanged();
        NotifyCreatorShellVisualStateChanged();
    }

    partial void OnIsLaunchingChanged(bool value) => NotifyStreamingToolsStateChanged();

    partial void OnIsGameRunningChanged(bool value) => NotifyStreamingToolsStateChanged();

    partial void OnRunningModpackChanged(ModpackInfo? value) => NotifyStreamingToolsStateChanged();

    partial void OnLaunchStatusChanged(string value) => NotifyStreamingToolsStateChanged();

    partial void OnIsLoggedInChanged(bool value) => NotifyStreamingToolsStateChanged();

    partial void OnUserSessionChanged(CmlLib.Core.Auth.MSession value)
    {
        NotifyStreamingToolsStateChanged();
        NotifySkinStudioStateChanged();
        NotifyVoidIdStateChanged();
        _ = LoadSkinHistoryAsync(true);
        _ = LoadAchievementSnapshotAsync();
    }

    [RelayCommand]
    private void RefreshDiscordPresence()
    {
        if (IsGameRunning)
            _discordRpcService.SetPlayingState(GetStreamingContextModpack()?.Name ?? "Minecraft");
        else
            UpdateDiscordPresence();

        NotifyStreamingToolsStateChanged();
        ShowToast("Streaming Tools", "Discord Rich Presence byl obnoven.", ToastSeverity.Info, 2000);
    }

    [RelayCommand]
    private void OpenStreamingWorkspace()
    {
        var modpack = GetCreatorStudioSelectedModpack() ?? GetStreamingContextModpack();
        if (modpack == null)
        {
            ShowToast("Streaming Tools", "Nejdřív vyber nebo spusť modpack.", ToastSeverity.Warning);
            return;
        }

        OpenFolder(_launcherService.GetModpackPath(modpack.Name));
    }

    [RelayCommand]
    private void OpenStreamingLogsFolder()
    {
        var modpack = GetCreatorStudioSelectedModpack() ?? GetStreamingContextModpack();
        if (modpack == null)
        {
            ShowToast("Streaming Tools", "Nejdřív vyber nebo spusť modpack.", ToastSeverity.Warning);
            return;
        }

        var logsPath = Path.Combine(_launcherService.GetModpackPath(modpack.Name), "logs");
        Directory.CreateDirectory(logsPath);
        OpenFolder(logsPath);
    }

    [RelayCommand]
    private void OpenStreamingScreenshotsFolder()
    {
        var modpack = GetCreatorStudioSelectedModpack() ?? GetStreamingContextModpack();
        if (modpack == null)
        {
            ShowToast("Streaming Tools", "Nejdřív vyber nebo spusť modpack.", ToastSeverity.Warning);
            return;
        }

        var basePath = _launcherService.GetModpackPath(modpack.Name);
        var targetPath = _creatorAssetsService.GetScreenshotGalleryPath(basePath, ensureExists: true);
        Directory.CreateDirectory(targetPath);
        OpenFolder(targetPath);
    }

    [RelayCommand]
    private void OpenCreatorModsFolder()
    {
        var modsPath = CreatorModsFolderPath;
        if (string.IsNullOrWhiteSpace(modsPath))
        {
            ShowToast("Creator Studio", "Nejdřív vyber pracovní instance pro Mods workflow.", ToastSeverity.Warning);
            return;
        }

        if (!Directory.Exists(modsPath))
        {
            if (!CanManageCreatorModsInPlace)
            {
                ShowToast("Creator Studio", "Vybraný workspace zatím nemá mods složku.", ToastSeverity.Warning, 2600);
                return;
            }

            Directory.CreateDirectory(modsPath);
        }

        OpenFolder(modsPath);
        TrackCreatorActivity("Otevrena mods slozka workspace.");
    }

    [RelayCommand]
    private void OpenCreatorModManager()
    {
        var modpack = ResolveModManagementTargetModpack();
        if (modpack == null)
        {
            ShowToast("Creator Studio", "Nejdřív vyber pracovní instance pro správu modů.", ToastSeverity.Warning);
            return;
        }

        var modpackPath = _launcherService.GetModpackPath(modpack.Name);
        var vm = new ModManagerViewModel(modpack.Name, modpackPath);
        var window = new VoidCraftLauncher.Views.ModManagerWindow { DataContext = vm };
        vm.RequestClose += window.Close;

        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            window.ShowDialog(desktop.MainWindow);
            TrackCreatorActivity($"Otevren pokrocily mod manager pro {modpack.DisplayLabel}.");
        }
    }

    [RelayCommand]
    private async Task CopyCreatorSessionSummary()
    {
        var topLevel = Avalonia.Controls.TopLevel.GetTopLevel(
            Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null);

        if (topLevel?.Clipboard == null)
        {
            ShowToast("Streaming Tools", "Schránka není dostupná.", ToastSeverity.Warning);
            return;
        }

        var summary =
            $"VOID-CRAFT | účet: {StreamingAccountLabel}\n" +
            $"Modpack: {CreatorStudioInstanceLabel}\n" +
            $"Pack: {(string.IsNullOrWhiteSpace(CreatorMetadataPackName) ? CreatorStudioInstanceLabel : CreatorMetadataPackName)}\n" +
            $"Release: {CreatorMetadataSlug} • v{CreatorMetadataVersion} • {CreatorMetadataReleaseChannel}\n" +
            $"Runtime: {CreatorStudioMinecraftVersion} / {CreatorStudioModLoader}\n" +
            $"Server: {(string.IsNullOrWhiteSpace(CreatorMetadataPrimaryServer) ? "mc.void-craft.eu" : CreatorMetadataPrimaryServer)}\n" +
            $"Soubor: {CreatorWorkbenchSelectionLabel}";

        await topLevel.Clipboard.SetTextAsync(summary);
        ShowToast("Streaming Tools", "Creator summary je ve schránce.", ToastSeverity.Success, 2500);
    }

    partial void OnSelectedCreatorWorkbenchFileChanged(CreatorWorkbenchFile? value)
    {
        OnPropertyChanged(nameof(HasSelectedCreatorWorkbenchFile));
        OnPropertyChanged(nameof(CreatorWorkbenchSelectionLabel));
        OnPropertyChanged(nameof(CreatorWorkbenchSelectionMeta));
        OnPropertyChanged(nameof(CreatorWorkbenchBreadcrumb));
        OnPropertyChanged(nameof(CreatorWorkbenchDirtyStateLabel));
        OnPropertyChanged(nameof(CanSaveCreatorWorkbenchFile));
        _ = LoadSelectedCreatorWorkbenchFileAsync(value);
        SyncCreatorWorkbenchFocus(value);
    }

    partial void OnCreatorWorkbenchContentChanged(string value)
    {
        OnPropertyChanged(nameof(CanSaveCreatorWorkbenchFile));
        OnPropertyChanged(nameof(CreatorWorkbenchDirtyStateLabel));
        HandleCreatorWorkbenchContentChanged();
        UpdateCreatorDirtyIndicators();
    }

    partial void OnCreatorWorkbenchSearchQueryChanged(string value)
    {
        ApplyCreatorWorkbenchFilter();

        if (SelectedCreatorWorkbenchFile != null &&
            !FilteredCreatorWorkbenchFiles.Any(file => string.Equals(file.FullPath, SelectedCreatorWorkbenchFile.FullPath, StringComparison.OrdinalIgnoreCase)))
        {
            SelectedCreatorWorkbenchFile = FilteredCreatorWorkbenchFiles.FirstOrDefault();
        }
        else if (SelectedCreatorWorkbenchFile == null)
        {
            SelectedCreatorWorkbenchFile = FilteredCreatorWorkbenchFiles.FirstOrDefault();
        }
    }

    partial void OnIsCreatorWorkbenchLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanSaveCreatorWorkbenchFile));
    }

    partial void OnIsCreatorWorkbenchSavingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanSaveCreatorWorkbenchFile));
    }

    [RelayCommand]
    private async Task RefreshCreatorWorkbench()
    {
        await RefreshCreatorWorkbenchAsync();
        ShowToast("Creator Studio", "Seznam upravitelných souborů byl obnoven.", ToastSeverity.Success, 2000);
        TrackCreatorActivity("Obnoven seznam creator workbench souboru.");
    }

    [RelayCommand]
    private async Task SaveCreatorWorkbenchFile()
    {
        if (!IsCreatorWorkspaceEditable)
        {
            ShowToast("Creator Studio", CreatorWorkspaceEditabilityMessage, ToastSeverity.Warning, 3200);
            return;
        }

        if (SelectedCreatorWorkbenchFile == null)
        {
            ShowToast("Creator Studio", "Nejdřív otevři soubor k úpravě.", ToastSeverity.Warning);
            return;
        }

        IsCreatorWorkbenchSaving = true;
        try
        {
            await _creatorWorkbenchService.SaveFileAsync(SelectedCreatorWorkbenchFile.FullPath, CreatorWorkbenchContent ?? string.Empty);
            _creatorWorkbenchLoadedContent = CreatorWorkbenchContent ?? string.Empty;
            CreatorWorkbenchStatus = $"Uloženo: {SelectedCreatorWorkbenchFile.RelativePath}";
            OnPropertyChanged(nameof(CanSaveCreatorWorkbenchFile));
            ShowToast("Creator Studio", "Soubor byl uložen přímo do instance.", ToastSeverity.Success, 2200);
            TrackCreatorActivity($"Ulozen soubor {SelectedCreatorWorkbenchFile.RelativePath}.");
            RefreshCreatorWorkspaceContext();
        }
        catch (Exception ex)
        {
            LogService.Error("Creator workbench save failed", ex);
            CreatorWorkbenchStatus = "Uložení souboru se nepodařilo.";
            ShowToast("Creator Studio", "Soubor se nepodařilo uložit.", ToastSeverity.Error);
        }
        finally
        {
            IsCreatorWorkbenchSaving = false;
        }
    }

    private async Task RefreshCreatorWorkbenchAsync(bool preserveSelection = true)
    {
        var previousPath = preserveSelection ? SelectedCreatorWorkbenchFile?.FullPath : null;
        var workspaceId = SelectedSkinStudioInstance?.Id ?? CreatorPreferences.SelectedWorkspaceId;
        var instancePath = string.IsNullOrWhiteSpace(workspaceId)
            ? null
            : _launcherService.GetModpackPath(workspaceId);

        var files = string.IsNullOrWhiteSpace(instancePath)
            ? Array.Empty<CreatorWorkbenchFile>()
            : _creatorWorkbenchService.DiscoverFiles(instancePath).ToArray();

        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            CreatorWorkbenchFiles.Clear();
            foreach (var file in files)
            {
                CreatorWorkbenchFiles.Add(file);
            }

            ApplyCreatorWorkbenchFilter();

            SelectedCreatorWorkbenchFile = FilteredCreatorWorkbenchFiles.FirstOrDefault(file => string.Equals(file.FullPath, previousPath, StringComparison.OrdinalIgnoreCase))
                ?? FilteredCreatorWorkbenchFiles.FirstOrDefault();

            if (SelectedCreatorWorkbenchFile == null)
            {
                _creatorWorkbenchLoadedContent = string.Empty;
                CreatorWorkbenchContent = string.Empty;
                CreatorWorkbenchStatus = HasCreatorStudioInstances
                    ? "Ve vybrané instanci nebyly nalezeny žádné malé textové soubory vhodné pro editaci."
                    : "Creator Studio čeká na nainstalovanou instanci.";
                ClearCreatorWorkbenchEditorState();
            }

            OnPropertyChanged(nameof(HasCreatorWorkbenchFiles));
            OnPropertyChanged(nameof(HasVisibleCreatorWorkbenchFiles));
            OnPropertyChanged(nameof(HasSelectedCreatorWorkbenchFile));
            OnPropertyChanged(nameof(CreatorWorkbenchSearchSummary));
            OnPropertyChanged(nameof(CreatorWorkbenchEmptyTitle));
            OnPropertyChanged(nameof(CreatorWorkbenchEmptySubtitle));
            OnPropertyChanged(nameof(CreatorWorkbenchSelectionLabel));
            OnPropertyChanged(nameof(CreatorWorkbenchSelectionMeta));
            OnPropertyChanged(nameof(CreatorWorkbenchBreadcrumb));
            OnPropertyChanged(nameof(CreatorWorkbenchDirtyStateLabel));
            OnPropertyChanged(nameof(CanSaveCreatorWorkbenchFile));
            RefreshCreatorWorkspaceContext();
        });
    }

    private void ApplyCreatorWorkbenchFilter()
    {
        var query = CreatorWorkbenchSearchQuery?.Trim();
        var filtered = string.IsNullOrWhiteSpace(query)
            ? CreatorWorkbenchFiles
            : new ObservableCollection<CreatorWorkbenchFile>(CreatorWorkbenchFiles.Where(file =>
                file.RelativePath.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                file.Category.Contains(query, StringComparison.OrdinalIgnoreCase)));

        FilteredCreatorWorkbenchFiles.Clear();
        foreach (var file in filtered)
        {
            FilteredCreatorWorkbenchFiles.Add(file);
        }

        OnPropertyChanged(nameof(HasVisibleCreatorWorkbenchFiles));
        OnPropertyChanged(nameof(CreatorWorkbenchSearchSummary));
        OnPropertyChanged(nameof(CreatorWorkbenchEmptyTitle));
        OnPropertyChanged(nameof(CreatorWorkbenchEmptySubtitle));
    }

    private async Task LoadSelectedCreatorWorkbenchFileAsync(CreatorWorkbenchFile? file)
    {
        if (file == null)
        {
            _creatorWorkbenchLoadedContent = string.Empty;
            CreatorWorkbenchContent = string.Empty;
            CreatorWorkbenchStatus = HasCreatorStudioInstances
                ? "Vyber soubor z levého seznamu."
                : "Creator Studio čeká na nainstalovanou instanci.";
            ClearCreatorWorkbenchEditorState();
            return;
        }

        IsCreatorWorkbenchLoading = true;
        try
        {
            var content = await _creatorWorkbenchService.ReadFileAsync(file.FullPath);
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                _suppressCreatorWorkbenchRawAnalysis = true;
                _creatorWorkbenchLoadedContent = content;
                CreatorWorkbenchContent = content;
                _suppressCreatorWorkbenchRawAnalysis = false;
                RebuildCreatorWorkbenchEditorState(file, content, preferRecommendedMode: true);
                CreatorWorkbenchStatus = $"Načteno: {file.RelativePath}";
                OnPropertyChanged(nameof(CanSaveCreatorWorkbenchFile));
                RefreshCreatorWorkspaceContext();
            });
        }
        catch (Exception ex)
        {
            LogService.Error("Creator workbench load failed", ex);
            CreatorWorkbenchStatus = "Soubor se nepodařilo načíst.";
            ShowToast("Creator Studio", "Soubor se nepodařilo načíst.", ToastSeverity.Error);
        }
        finally
        {
            IsCreatorWorkbenchLoading = false;
        }
    }

    private static void OpenFolder(string path)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true,
            Verb = "open"
        });
    }

    private ModpackInfo? GetCreatorStudioSelectedModpack()
    {
        var workspaceId = SelectedSkinStudioInstance?.Id ?? CreatorPreferences.SelectedWorkspaceId;
        if (!string.IsNullOrWhiteSpace(workspaceId))
        {
            return InstalledModpacks.FirstOrDefault(modpack => string.Equals(modpack.Name, workspaceId, System.StringComparison.OrdinalIgnoreCase))
                ?? GetStreamingContextModpack();
        }

        return GetStreamingContextModpack();
    }

    private ModpackManifestInfo? GetCreatorStudioSelectedManifest()
    {
        var modpack = GetCreatorStudioSelectedModpack();
        if (modpack == null)
        {
            return null;
        }

        return TryLoadManifestInfo(modpack);
    }

    private static string FormatCreatorStudioLoaderLabel(string? modLoaderId, ModpackInfo? modpack)
    {
        if (!string.IsNullOrWhiteSpace(modLoaderId))
        {
            return modLoaderId;
        }

        if (modpack?.IsCustomProfile == true)
        {
            return string.IsNullOrWhiteSpace(modpack.CustomModLoader)
                ? "Nezjištěno"
                : modpack.CustomModLoader;
        }

        return "Nezjištěno";
    }
}