using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using VoidCraftLauncher.Models;
using VoidCraftLauncher.Services;

namespace VoidCraftLauncher.ViewModels;

public partial class MainViewModel
{
    public ObservableCollection<SelectionOption> SkinStudioInstanceOptions { get; } = new();

    public ObservableCollection<SkinHistoryItem> SkinHistoryItems { get; } = new();

    [ObservableProperty]
    private SelectionOption? _selectedSkinStudioInstance;

    [ObservableProperty]
    private SkinHistoryItem? _selectedSkinHistoryItem;

    [ObservableProperty]
    private bool _isSkinHistoryLoading;

    public string SkinStudioDisplayName =>
        !string.IsNullOrWhiteSpace(UserSession?.Username)
            ? UserSession.Username
            : ActiveAccount?.DisplayName ?? "Offline relace";

    public string SkinStudioUuid => CurrentSkinUuid ?? "Nedostupné pro offline relaci";

    public string SkinStudioAccountTypeLabel => ActiveAccount?.Type switch
    {
        AccountType.Microsoft => "Microsoft účet",
        AccountType.Offline => "Offline účet",
        _ => IsEffectiveMicrosoftSkinAccount ? "Microsoft účet" : !string.IsNullOrWhiteSpace(UserSession?.Username) ? "Offline účet" : "Nepřihlášená relace"
    };

    public bool HasSkinUuid => !string.IsNullOrWhiteSpace(CurrentSkinUuid);

    public bool IsMicrosoftSkinAccount => IsEffectiveMicrosoftSkinAccount && HasSkinUuid;

    private bool IsEffectiveMicrosoftSkinAccount =>
        ActiveAccount?.Type == AccountType.Microsoft ||
        (IsLoggedIn && ActiveAccount == null &&
         !string.IsNullOrWhiteSpace(UserSession?.Username) &&
         !string.Equals(UserSession.Username, "Guest", StringComparison.OrdinalIgnoreCase));

    public bool HasPublicSkinProfile => IsMicrosoftSkinAccount && !string.IsNullOrWhiteSpace(GetPreferredSkinProfileIdentifier());

    public string SkinStudioHeadUrl => HasSkinUuid
        ? $"https://mc-heads.net/avatar/{CurrentSkinUuidNoDashes}/160"
        : "https://mc-heads.net/avatar/MHF_Steve/160";

    public string SkinStudioBodyUrl => HasSkinUuid
        ? $"https://mc-heads.net/body/{CurrentSkinUuidNoDashes}/left"
        : "https://mc-heads.net/body/MHF_Steve/left";

    public string SkinStudioLargeRenderUrl => HasSkinUuid
        ? $"https://mc-heads.net/body/{CurrentSkinUuidNoDashes}/right"
        : "https://mc-heads.net/body/MHF_Steve/right";

    public string SkinStudioIdentitySummary =>
        $"{SkinStudioDisplayName} | {SkinStudioAccountTypeLabel}";

    public bool HasSkinStudioInstances => SkinStudioInstanceOptions.Count > 0;

    public bool HasSkinHistory => SkinHistoryItems.Count > 0;

    public string SkinStudioSelectedSkinTitle => SelectedSkinHistoryItem?.IsCurrent == true
        ? "Aktuálně použitý skin"
        : SelectedSkinHistoryItem != null
            ? "Vybraný skin z historie"
            : IsMicrosoftSkinAccount
                ? "Historie skinů z NameMC"
                : "Lokální profil";

    public string SkinStudioSelectedSkinSubtitle => SelectedSkinHistoryItem?.SeenAtLabel
        ?? (IsMicrosoftSkinAccount
            ? "Vyber skin z historie nebo obnov data."
            : "Offline účet nemá veřejný NameMC profil.");

    public string SkinStudioHeroDescription => IsMicrosoftSkinAccount
        ? "Veřejná historie skinů, render aktivní postavy a identita účtu. Pro změnu skinu použij NameMC nebo Minecraft.net."
        : "Render postavy a identita aktuálního účtu. Veřejná historie skinů je dostupná po přihlášení přes Microsoft účet.";

    public string SkinStudioIdentityDescription => IsMicrosoftSkinAccount
        ? "Přehled účtu, UUID a veřejné skin historie z NameMC profilu."
        : "Přehled účtu a UUID. Veřejná historie skinů je dostupná po přihlášení přes Microsoft účet.";

    public string SkinHistoryEmptyTitle => IsMicrosoftSkinAccount
        ? "Historie skinů zatím není dostupná"
        : "Přihlas se pro zobrazení historie";

    public string SkinHistoryEmptySubtitle => IsMicrosoftSkinAccount
        ? "NameMC zatím pro tento profil nemá veřejnou historii. Zkus obnovit načtení za moment."
        : "Historie skinů vyžaduje přihlášení přes Microsoft účet. Offline účty nemají veřejný NameMC profil.";

    public string SkinStudioSelectedInstancePath
    {
        get
        {
            var workspaceId = SelectedSkinStudioInstance?.Id ?? Config.CreatorStudio?.SelectedWorkspaceId;
            return string.IsNullOrWhiteSpace(workspaceId)
                ? "Vyber instanci, ve které chceš pracovat se skin toolingem a screenshoty."
                : _launcherService.GetModpackPath(workspaceId);
        }
    }

    private string? CurrentSkinUuid => ActiveAccount?.Uuid ?? UserSession?.UUID;

    private string? CurrentSkinUuidNoDashes => CurrentSkinUuid?.Replace("-", string.Empty, StringComparison.Ordinal);

    partial void OnSelectedSkinStudioInstanceChanged(SelectionOption? value)
    {
        OnPropertyChanged(nameof(SkinStudioSelectedInstancePath));
        NotifyStreamingToolsStateChanged();
        _ = RefreshCreatorWorkbenchAsync();
        OnCreatorWorkspaceSelectionChanged(value);
    }

    partial void OnSelectedSkinHistoryItemChanged(SkinHistoryItem? value)
    {
        OnPropertyChanged(nameof(SkinStudioSelectedSkinTitle));
        OnPropertyChanged(nameof(SkinStudioSelectedSkinSubtitle));
    }

    [RelayCommand]
    private void OpenSkinAvatarPreview()
    {
        if (!HasSkinUuid)
        {
            ShowToast("Skin Studio", "Aktivní účet zatím nemá dostupné UUID pro skin preview.", ToastSeverity.Warning);
            return;
        }

        OpenUrl($"https://mc-heads.net/avatar/{CurrentSkinUuidNoDashes}/256");
    }

    [RelayCommand]
    private void OpenSkinProfilePage()
    {
        if (!HasPublicSkinProfile)
        {
            OpenLoginModal();
            return;
        }

        var profileIdentifier = GetPreferredSkinProfileIdentifier();
        if (!string.IsNullOrWhiteSpace(profileIdentifier))
        {
            OpenUrl($"https://namemc.com/profile/{Uri.EscapeDataString(profileIdentifier)}");
            return;
        }

        ShowToast("Skin Studio", "Profil pro aktivní relaci není dostupný.", ToastSeverity.Warning);
    }

    [RelayCommand]
    private async Task CopySkinIdentity()
    {
        var topLevel = Avalonia.Controls.TopLevel.GetTopLevel(
            Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null);

        if (topLevel?.Clipboard == null)
        {
            ShowToast("Skin Studio", "Schránka není dostupná.", ToastSeverity.Warning);
            return;
        }

        var payload =
            $"VOID-CRAFT Skin Studio\n" +
            $"Jméno: {SkinStudioDisplayName}\n" +
            $"Typ účtu: {SkinStudioAccountTypeLabel}\n" +
            $"UUID: {SkinStudioUuid}";

        await topLevel.Clipboard.SetTextAsync(payload);
        ShowToast("Skin Studio", "Identita hráče je ve schránce.", ToastSeverity.Success, 2500);
    }

    [RelayCommand]
    private void OpenSkinAccountTools()
    {
        if (!IsMicrosoftSkinAccount)
        {
            OpenLoginModal();
            return;
        }

        OpenUrl("https://namemc.com/minecraft-skins");
    }

    [RelayCommand]
    private async Task RefreshSkinHistory()
    {
        await LoadSkinHistoryAsync(true);
    }

    [RelayCommand]
    private void OpenSelectedSkinPage()
    {
        if (SelectedSkinHistoryItem == null)
        {
            OpenSkinProfilePage();
            return;
        }

        OpenUrl(SelectedSkinHistoryItem.PageUrl);
    }

    [RelayCommand]
    private void OpenSkinStudioInstanceWorkspace()
    {
        if (SelectedSkinStudioInstance == null)
        {
            ShowToast("Skin Studio", "Nejdřív vyber instanci, se kterou chceš pracovat.", ToastSeverity.Warning);
            return;
        }

        OpenFolder(_launcherService.GetModpackPath(SelectedSkinStudioInstance.Id));
    }

    private async Task LoadSkinHistoryAsync(bool forceRefresh = false)
    {
        if (IsSkinHistoryLoading)
        {
            return;
        }

        IsSkinHistoryLoading = true;
        try
        {
            if (!IsMicrosoftSkinAccount)
            {
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    SkinHistoryItems.Clear();
                    SelectedSkinHistoryItem = null;
                    OnPropertyChanged(nameof(HasSkinHistory));
                    OnPropertyChanged(nameof(SkinStudioSelectedSkinTitle));
                    OnPropertyChanged(nameof(SkinStudioSelectedSkinSubtitle));
                    OnPropertyChanged(nameof(SkinHistoryEmptyTitle));
                    OnPropertyChanged(nameof(SkinHistoryEmptySubtitle));
                });
                return;
            }

            var identifiers = new[]
            {
                GetPreferredSkinProfileIdentifier(),
                UserSession?.Username,
                ActiveAccount?.DisplayName,
                CurrentSkinUuidNoDashes
            }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

            var items = identifiers.Count == 0
                ? Array.Empty<SkinHistoryItem>()
                : await _skinStudioService.GetSkinHistoryAsync(identifiers, forceRefresh);

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                SkinHistoryItems.Clear();
                foreach (var item in items)
                {
                    SkinHistoryItems.Add(item);
                }

                SelectedSkinHistoryItem = SkinHistoryItems.FirstOrDefault(item => item.IsCurrent)
                    ?? SkinHistoryItems.FirstOrDefault();

                OnPropertyChanged(nameof(HasSkinHistory));
                OnPropertyChanged(nameof(SkinStudioSelectedSkinTitle));
                OnPropertyChanged(nameof(SkinStudioSelectedSkinSubtitle));
            });
        }
        catch (Exception ex)
        {
            LogService.Error("Skin Studio history load failed", ex);
        }
        finally
        {
            IsSkinHistoryLoading = false;
        }
    }

    private void RebuildSkinStudioInstanceOptions()
    {
        var persistedSelectedId = Config.CreatorStudio?.SelectedWorkspaceId;
        var selectedId = !string.IsNullOrWhiteSpace(persistedSelectedId)
            ? persistedSelectedId
            : SelectedSkinStudioInstance?.Id;
        var currentModpackName = CurrentModpack?.Name;

        SkinStudioInstanceOptions.Clear();
        foreach (var modpack in InstalledModpacks
            .OrderByDescending(modpack => string.Equals(modpack.Name, currentModpackName, StringComparison.OrdinalIgnoreCase))
            .ThenBy(modpack => modpack.DisplayLabel, StringComparer.OrdinalIgnoreCase))
        {
            SkinStudioInstanceOptions.Add(new SelectionOption
            {
                Id = modpack.Name,
                Label = modpack.DisplayLabel
            });
        }

        SelectedSkinStudioInstance = SkinStudioInstanceOptions.FirstOrDefault(option => option.Id == selectedId)
            ?? SkinStudioInstanceOptions.FirstOrDefault(option => option.Id == currentModpackName)
            ?? SkinStudioInstanceOptions.FirstOrDefault();

        OnPropertyChanged(nameof(HasSkinStudioInstances));
        OnPropertyChanged(nameof(SkinStudioSelectedInstancePath));
    }

    private void NotifySkinStudioStateChanged()
    {
        OnPropertyChanged(nameof(SkinStudioDisplayName));
        OnPropertyChanged(nameof(SkinStudioUuid));
        OnPropertyChanged(nameof(SkinStudioAccountTypeLabel));
        OnPropertyChanged(nameof(HasSkinUuid));
        OnPropertyChanged(nameof(IsMicrosoftSkinAccount));
        OnPropertyChanged(nameof(HasPublicSkinProfile));
        OnPropertyChanged(nameof(SkinStudioHeadUrl));
        OnPropertyChanged(nameof(SkinStudioBodyUrl));
        OnPropertyChanged(nameof(SkinStudioLargeRenderUrl));
        OnPropertyChanged(nameof(SkinStudioIdentitySummary));
        OnPropertyChanged(nameof(SkinStudioHeroDescription));
        OnPropertyChanged(nameof(SkinStudioIdentityDescription));
        OnPropertyChanged(nameof(HasSkinHistory));
        OnPropertyChanged(nameof(SkinStudioSelectedSkinTitle));
        OnPropertyChanged(nameof(SkinStudioSelectedSkinSubtitle));
        OnPropertyChanged(nameof(SkinHistoryEmptyTitle));
        OnPropertyChanged(nameof(SkinHistoryEmptySubtitle));
    }

    private string? GetPreferredSkinProfileIdentifier()
    {
        if (!string.IsNullOrWhiteSpace(UserSession?.Username))
        {
            return UserSession.Username;
        }

        if (!string.IsNullOrWhiteSpace(ActiveAccount?.DisplayName))
        {
            return ActiveAccount.DisplayName;
        }

        return CurrentSkinUuidNoDashes;
    }

    partial void OnActiveAccountChanged(AccountProfile? value)
    {
        NotifySkinStudioStateChanged();
        NotifyVoidIdStateChanged();
        _ = LoadSkinHistoryAsync(true);
        _ = LoadAchievementSnapshotAsync();
    }
}