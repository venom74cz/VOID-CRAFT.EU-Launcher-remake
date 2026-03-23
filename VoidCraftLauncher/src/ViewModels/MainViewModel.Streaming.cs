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

    public string CreatorStudioInstanceLabel => SelectedSkinStudioInstance?.Label ?? "Bez zvolené instance";

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
        : $"{SelectedCreatorWorkbenchFile.Category} • {SelectedCreatorWorkbenchFile.SizeLabel}";

    public bool CanSaveCreatorWorkbenchFile =>
        SelectedCreatorWorkbenchFile != null &&
        !IsCreatorWorkbenchLoading &&
        !IsCreatorWorkbenchSaving &&
        !string.Equals(_creatorWorkbenchLoadedContent, CreatorWorkbenchContent ?? string.Empty, StringComparison.Ordinal);

    public string CreatorStudioMinecraftVersion => GetCreatorStudioSelectedManifest()?.MinecraftVersion switch
    {
        { Length: > 0 } version => version,
        _ => GetCreatorStudioSelectedModpack()?.CurrentVersion?.Name ?? "Nezjištěno"
    };

    public string CreatorStudioModLoader => FormatCreatorStudioLoaderLabel(GetCreatorStudioSelectedManifest()?.ModLoaderId, GetCreatorStudioSelectedModpack());

    public string CreatorStudioModCountLabel
    {
        get
        {
            var modpack = GetCreatorStudioSelectedModpack();
            var manifest = GetCreatorStudioSelectedManifest();
            var modCount = manifest?.ModCount ?? 0;

            if (modCount <= 0 && modpack != null)
            {
                modCount = GetInstalledModCount(modpack);
            }

            return modCount > 0 ? $"{modCount} modů" : "Počet modů není k dispozici";
        }
    }

    public string CreatorStudioLinkedServersLabel
    {
        get
        {
            var modpack = GetCreatorStudioSelectedModpack();
            if (modpack == null)
            {
                return "Bez navázané instance";
            }

            var count = Servers.Where(server =>
                server.LinkedModpackProjectId == modpack.ProjectId ||
                ArePackNamesEquivalent(server.LinkedModpackName, modpack.Name)).Count();

            return count == 0 ? "Žádné navázané servery" : $"{count} navázaných serverů";
        }
    }

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
        OnPropertyChanged(nameof(HasCreatorWorkbenchFiles));
        OnPropertyChanged(nameof(HasVisibleCreatorWorkbenchFiles));
        OnPropertyChanged(nameof(HasSelectedCreatorWorkbenchFile));
        OnPropertyChanged(nameof(CreatorWorkbenchSearchSummary));
        OnPropertyChanged(nameof(CreatorWorkbenchEmptyTitle));
        OnPropertyChanged(nameof(CreatorWorkbenchEmptySubtitle));
        OnPropertyChanged(nameof(CreatorWorkbenchSelectionLabel));
        OnPropertyChanged(nameof(CreatorWorkbenchSelectionMeta));
        OnPropertyChanged(nameof(CanSaveCreatorWorkbenchFile));
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
        var screenshotsPath = Path.Combine(basePath, "screenshots");
        var screenshotyPath = Path.Combine(basePath, "screenshoty");
        var targetPath = Directory.Exists(screenshotyPath) ? screenshotyPath : screenshotsPath;
        Directory.CreateDirectory(targetPath);
        OpenFolder(targetPath);
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
        OnPropertyChanged(nameof(CanSaveCreatorWorkbenchFile));
        _ = LoadSelectedCreatorWorkbenchFileAsync(value);
        SyncCreatorWorkbenchFocus(value);
    }

    partial void OnCreatorWorkbenchContentChanged(string value)
    {
        OnPropertyChanged(nameof(CanSaveCreatorWorkbenchFile));
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
        var instancePath = SelectedSkinStudioInstance == null
            ? null
            : _launcherService.GetModpackPath(SelectedSkinStudioInstance.Id);

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
            }

            OnPropertyChanged(nameof(HasCreatorWorkbenchFiles));
            OnPropertyChanged(nameof(HasVisibleCreatorWorkbenchFiles));
            OnPropertyChanged(nameof(HasSelectedCreatorWorkbenchFile));
            OnPropertyChanged(nameof(CreatorWorkbenchSearchSummary));
            OnPropertyChanged(nameof(CreatorWorkbenchEmptyTitle));
            OnPropertyChanged(nameof(CreatorWorkbenchEmptySubtitle));
            OnPropertyChanged(nameof(CreatorWorkbenchSelectionLabel));
            OnPropertyChanged(nameof(CreatorWorkbenchSelectionMeta));
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
            return;
        }

        IsCreatorWorkbenchLoading = true;
        try
        {
            var content = await _creatorWorkbenchService.ReadFileAsync(file.FullPath);
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                _creatorWorkbenchLoadedContent = content;
                CreatorWorkbenchContent = content;
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
        if (SelectedSkinStudioInstance == null)
        {
            return GetStreamingContextModpack();
        }

        return InstalledModpacks.FirstOrDefault(modpack => string.Equals(modpack.Name, SelectedSkinStudioInstance.Id, System.StringComparison.OrdinalIgnoreCase))
            ?? GetStreamingContextModpack();
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