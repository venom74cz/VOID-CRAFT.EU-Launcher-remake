using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using VoidCraftLauncher.Models;
using VoidCraftLauncher.Services;

namespace VoidCraftLauncher.ViewModels;

public partial class MainViewModel
{
    [ObservableProperty]
    private VoidIdSession? _creatorVoidIdSession;

    [ObservableProperty]
    private bool _isVoidIdLoginInProgress;

    [ObservableProperty]
    private string _voidIdLoginStatus = "Creator není přihlášený do VOID ID.";

    [ObservableProperty]
    private string _voidIdVerificationUrl = string.Empty;

    [ObservableProperty]
    private string _voidIdUserCode = string.Empty;

    [ObservableProperty]
    private bool _isVoidIdControlPlaneLoading;

    [ObservableProperty]
    private string _voidIdControlPlaneStatus = "Přehled identity čeká na přihlášení do VOID ID.";

    [ObservableProperty]
    private bool _isVoidIdGitHubProviderOperationRunning;

    [ObservableProperty]
    private string _voidIdGitHubProviderStatus = "GitHub zatím není propojený s VOID ID.";

    public ObservableCollection<VoidIdRefreshSessionInfo> VoidIdActiveSessions { get; } = new();

    public ObservableCollection<VoidRegistryProjectSummary> VoidIdMembershipProjects { get; } = new();

    public bool HasVoidIdLoginUrl => !string.IsNullOrWhiteSpace(VoidIdVerificationUrl);

    public bool HasVoidIdSession => CreatorVoidIdSession?.IsAuthenticated == true;

    public bool HasVoidIdActiveSessions => VoidIdActiveSessions.Count > 0;

    public bool HasVoidIdMembershipProjects => VoidIdMembershipProjects.Count > 0;

    public bool CanRefreshVoidIdControlPlane =>
        HasVoidIdSession &&
        !IsVoidIdControlPlaneLoading &&
        !IsVoidIdGitHubProviderOperationRunning;

    public string VoidIdControlPlaneRefreshLabel => IsVoidIdControlPlaneLoading
        ? "Obnovuji přehled..."
        : "Obnovit přehled";

    public string VoidIdDisplayName => CreatorVoidIdSession?.Profile?.DisplayName ?? "Nepřihlášen";

    public string VoidIdRoleLabel => CreatorVoidIdSession?.Profile?.Role ?? "guest";

    public string VoidIdMinecraftLabel => CreatorVoidIdSession?.Profile?.HasMinecraftLink == true
        ? $"MC: {FirstNonEmpty(CreatorVoidIdSession.Profile.MinecraftName, CreatorVoidIdSession.Profile.MinecraftUuid, "linked") }"
        : "MC účet zatím není propojený";

    public string VoidIdAvatarUrl => CreatorVoidIdSession?.Profile?.AvatarUrl ?? string.Empty;

    public string VoidIdLastLoginLabel => CreatorVoidIdSession?.Profile?.LastLoginAtUtc is { } lastLoginAtUtc
        ? $"Poslední přihlášení {VoidIdModelFormatting.FormatDateTime(lastLoginAtUtc)}"
        : "Poslední přihlášení zatím není k dispozici.";

    public string VoidIdSessionSummary => HasVoidIdActiveSessions
        ? $"Aktivní relace: {VoidIdActiveSessions.Count}"
        : "Aktivní relace se načtou po přihlášení do VOID ID.";

    public string VoidIdMembershipSummary => HasVoidIdMembershipProjects
        ? $"Projektová oprávnění: {VoidIdMembershipProjects.Count}"
        : "Zatím bez projektových oprávnění nebo vlastních projektů.";

    public string VoidIdSecuritySummary => CreatorVoidIdSession?.Profile?.Security?.Summary
        ?? "Stav zabezpečení bude dostupný po přihlášení do VOID ID.";

    public bool HasVoidIdAdminRole => string.Equals(CreatorVoidIdSession?.Profile?.Role, "admin", StringComparison.OrdinalIgnoreCase);

    public bool HasVoidIdTeamAccess => CreatorVoidIdSession?.Profile?.Access?.IsTeamMember == true;

    public bool CanAccessVoidIdAdminSurface => HasVoidIdAdminRole || CreatorVoidIdSession?.Profile?.Access?.CanAccessAdminSurface == true;

    public string VoidIdAccessLevelLabel => HasVoidIdAdminRole
        ? "Administrátor"
        : HasVoidIdTeamAccess
            ? "Týmový přístup z Discordu"
            : "Standardní profil";

    public string VoidIdAccessSummary
    {
        get
        {
            if (!HasVoidIdSession)
            {
                return "Po přihlášení se ukáže, jestli má účet běžný profil, týmový přístup nebo administrátorská oprávnění.";
            }

            if (HasVoidIdAdminRole)
            {
                return "Launcher rozpoznal plná administrátorská oprávnění. Citlivé zásahy na webu mohou dál vyžadovat dodatečné TOTP ověření.";
            }

            if (HasVoidIdTeamAccess)
            {
                return "Launcher rozpoznal týmový přístup z Discordu. Interní část VOID-CRAFT.EU je dostupná i bez změny databázové role účtu.";
            }

            return "Launcher běží ve standardním profilu. Webový profil, Minecraft propojení a běžná správa účtu zůstávají plně dostupné.";
        }
    }

    public string VoidIdProfileEntryUrl => CanAccessVoidIdAdminSurface
        ? "https://void-craft.eu/admin"
        : "https://void-craft.eu/profil";

    public string VoidIdProfileEntryLabel => CanAccessVoidIdAdminSurface
        ? "Otevřít interní správu"
        : "Otevřít webový profil";

    public bool HasMicrosoftLauncherAccount =>
        ActiveAccount?.Type == AccountType.Microsoft &&
        !string.IsNullOrWhiteSpace(FirstNonEmpty(ActiveAccount?.DisplayName, UserSession?.Username));

    public string MicrosoftLauncherAccountName => HasMicrosoftLauncherAccount
        ? FirstNonEmpty(ActiveAccount?.DisplayName, UserSession?.Username, "Microsoft")
        : "Microsoft účet není aktivní";

    public string MicrosoftLauncherAccountDetail => HasMicrosoftLauncherAccount
        ? $"UUID {FirstNonEmpty(ActiveAccount?.Uuid, UserSession?.UUID, "-")}"
        : "Launcher teď nemá aktivní Microsoft účet pro Minecraft.";

    public string MicrosoftLauncherSyncLabel
    {
        get
        {
            if (!HasMicrosoftLauncherAccount)
            {
                return "Přihlas Microsoft účet, aby launcher mohl porovnat herní identitu s VOID ID.";
            }

            var profile = CreatorVoidIdSession?.Profile;
            if (profile?.HasMinecraftLink != true)
            {
                return "VOID ID zatím nemá propojený Minecraft účet. Samotné propojení dál spravuje stávající herní tok.";
            }

            return DoesMicrosoftAccountMatchVoidIdLink(profile)
                ? "Aktivní Microsoft účet odpovídá Minecraft propojení ve VOID ID."
                : "VOID ID má jiné Minecraft propojení než aktuální Microsoft účet v launcheru.";
        }
    }

    public bool CanLoginMicrosoftFromIdentity => !HasMicrosoftLauncherAccount && !IsLoginInProgress;

    public string VoidIdDiscordAccountName
    {
        get
        {
            var provider = GetVoidIdProviderState("discord");
            if (provider?.Connected == true)
            {
                return $"@{FirstNonEmpty(provider.Username, provider.UserId, "discord")}";
            }

            return "Discord není dostupný";
        }
    }

    public string VoidIdDiscordAccountDetail
    {
        get
        {
            var provider = GetVoidIdProviderState("discord");
            return provider?.Connected == true
                ? "Hlavní přihlašovací metoda pro VOID ID a webový profil."
                : "Discord se načte z aktivní relace VOID ID.";
        }
    }

    public bool IsVoidIdGitHubLinked => GetVoidIdProviderState("github")?.Connected == true;

    public string VoidIdGitHubAccountName
    {
        get
        {
            var provider = GetVoidIdProviderState("github");
            if (provider?.Connected == true)
            {
                return $"@{FirstNonEmpty(provider.Username, provider.UserId, "github")}";
            }

            return "GitHub není propojený";
        }
    }

    public string VoidIdGitHubAccountDetail => HasCreatorGitHubSession
        ? $"Aktivní GitHub účet v launcheru: @{FirstNonEmpty(CreatorGitHubSession?.Profile?.Login, CreatorGitHubSession?.Profile?.DisplayName, "github")}" 
        : "Nejdřív aktivuj GitHub v launcheru. Pak ho půjde propojit s VOID ID bez návratu přes web.";

    public bool CanLinkCreatorGitHubToVoidId =>
        HasVoidIdSession &&
        HasCreatorGitHubSession &&
        !IsVoidIdGitHubLinked &&
        !IsVoidIdGitHubProviderOperationRunning;

    public bool CanUnlinkCreatorGitHubFromVoidId =>
        HasVoidIdSession &&
        IsVoidIdGitHubLinked &&
        GetVoidIdProviderState("github")?.CanUnlink == true &&
        !IsVoidIdGitHubProviderOperationRunning;

    public bool HasVoidIdVerificationCode => !string.IsNullOrWhiteSpace(VoidIdUserCode);

    partial void OnVoidIdVerificationUrlChanged(string value) => OnPropertyChanged(nameof(HasVoidIdLoginUrl));

    partial void OnVoidIdUserCodeChanged(string value) => OnPropertyChanged(nameof(HasVoidIdVerificationCode));

    partial void OnIsVoidIdControlPlaneLoadingChanged(bool value) => NotifyVoidIdStateChanged();

    partial void OnIsVoidIdGitHubProviderOperationRunningChanged(bool value) => NotifyVoidIdStateChanged();

    partial void OnCreatorVoidIdSessionChanged(VoidIdSession? value)
    {
        NotifyVoidIdStateChanged();
        HandleAchievementIdentityChanged();
        QueueRefreshCreatorCollaborators(value?.IsAuthenticated == true
            ? "VOID ID session je aktivní, collaborator list se načítá z backendu."
            : "Správa spolupracovníků čeká na aktivní relaci VOID ID.");

        if (value?.IsAuthenticated == true)
        {
            VoidIdControlPlaneStatus = "VOID ID je aktivní. Načítám propojené účty, relace a projektová oprávnění.";
            VoidIdGitHubProviderStatus = BuildGitHubProviderStatus();
            _ = RefreshVoidIdControlPlaneAsync(showToastOnSuccess: false);
        }
        else
        {
            ClearVoidIdControlPlane();
        }
    }

    private async Task InitializeVoidIdSessionAsync()
    {
        try
        {
            var session = await _voidIdAuthService.LoadCachedSessionAsync();
            if (session?.IsAuthenticated == true)
            {
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    CreatorVoidIdSession = session;
                    VoidIdLoginStatus = $"VOID ID aktivní: {session.Profile?.DisplayName}";
                });
            }
        }
        catch (Exception ex)
        {
            LogService.Error("VOID ID restore failed", ex);
        }
    }

    private async Task<bool> EnsureFreshVoidIdSessionAsync(TimeSpan? minimumRemainingLifetime = null)
    {
        var requiredLifetime = minimumRemainingLifetime ?? TimeSpan.FromMinutes(1);

        if (CreatorVoidIdSession?.IsAuthenticated == true)
        {
            var expiresAt = CreatorVoidIdSession.AccessTokenExpiresAtUtc;
            if (expiresAt.HasValue && expiresAt > DateTimeOffset.UtcNow.Add(requiredLifetime))
            {
                return true;
            }
        }

        var refreshed = await _voidIdAuthService.TryRefreshAsync();
        if (refreshed?.IsAuthenticated == true)
        {
            CreatorVoidIdSession = refreshed;
            VoidIdLoginStatus = $"VOID ID obnoveno: {refreshed.Profile?.DisplayName}";
            return true;
        }

        if (CreatorVoidIdSession?.IsAuthenticated == true)
        {
            var profile = await _voidIdAuthService.GetProfileAsync(CreatorVoidIdSession.AccessToken);
            if (profile != null)
            {
                CreatorVoidIdSession = new VoidIdSession
                {
                    AccessToken = CreatorVoidIdSession.AccessToken,
                    RefreshToken = CreatorVoidIdSession.RefreshToken,
                    AccessTokenExpiresAtUtc = CreatorVoidIdSession.AccessTokenExpiresAtUtc,
                    Profile = profile
                };

                return true;
            }
        }

        return false;
    }

    private VoidIdProviderState? GetVoidIdProviderState(string provider)
    {
        var providers = CreatorVoidIdSession?.Profile?.Providers;
        if (providers == null || !providers.TryGetValue(provider, out var state))
        {
            return null;
        }

        return state;
    }

    private bool DoesMicrosoftAccountMatchVoidIdLink(VoidIdProfile profile)
    {
        var activeUuid = FirstNonEmpty(ActiveAccount?.Uuid, UserSession?.UUID, string.Empty)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Trim()
            .ToLowerInvariant();
        var linkedUuid = FirstNonEmpty(profile.MinecraftUuid, string.Empty)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Trim()
            .ToLowerInvariant();

        if (!string.IsNullOrWhiteSpace(activeUuid) && !string.IsNullOrWhiteSpace(linkedUuid))
        {
            return string.Equals(activeUuid, linkedUuid, StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(
            FirstNonEmpty(ActiveAccount?.DisplayName, UserSession?.Username, string.Empty),
            FirstNonEmpty(profile.MinecraftName, string.Empty),
            StringComparison.OrdinalIgnoreCase);
    }

    private string BuildGitHubProviderStatus()
    {
        if (!HasVoidIdSession)
        {
            return "GitHub čeká na aktivní relaci VOID ID.";
        }

        if (IsVoidIdGitHubLinked)
        {
            var provider = GetVoidIdProviderState("github");
            return $"GitHub je propojený jako @{FirstNonEmpty(provider?.Username, provider?.UserId, "github")}.";
        }

        if (HasCreatorGitHubSession)
        {
            return "GitHub v launcheru je aktivní a může se jedním kliknutím propojit s VOID ID.";
        }

        return "GitHub zatím není propojený s VOID ID.";
    }

    private string BuildVoidIdControlPlaneStatus()
    {
        if (!HasVoidIdSession)
        {
            return "Přehled identity čeká na přihlášení do VOID ID.";
        }

        return $"Synchronizace dokončena: {VoidIdActiveSessions.Count} relací, {VoidIdMembershipProjects.Count} projektových oprávnění a GitHub {(IsVoidIdGitHubLinked ? "propojený" : "nepropojený")}.";
    }

    private void ClearVoidIdControlPlane()
    {
        ReplaceCollectionItems(VoidIdActiveSessions, Array.Empty<VoidIdRefreshSessionInfo>());
        ReplaceCollectionItems(VoidIdMembershipProjects, Array.Empty<VoidRegistryProjectSummary>());
        VoidIdControlPlaneStatus = "Přehled identity čeká na přihlášení do VOID ID.";
        VoidIdGitHubProviderStatus = "GitHub zatím není propojený s VOID ID.";
        NotifyVoidIdStateChanged();
    }

    private async Task RefreshVoidIdControlPlaneAsync(bool showToastOnSuccess)
    {
        if (IsVoidIdControlPlaneLoading)
        {
            return;
        }

        if (!HasVoidIdSession)
        {
            ClearVoidIdControlPlane();
            return;
        }

        IsVoidIdControlPlaneLoading = true;
        try
        {
            if (!await EnsureFreshVoidIdSessionAsync(TimeSpan.FromMinutes(2)))
            {
                ClearVoidIdControlPlane();
                return;
            }

            var accessToken = CreatorVoidIdSession?.AccessToken;
            var refreshToken = CreatorVoidIdSession?.RefreshToken;
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                ClearVoidIdControlPlane();
                return;
            }

            var providersTask = _voidIdAuthService.GetProvidersAsync(accessToken);
            var sessionsTask = _voidIdAuthService.GetSessionsAsync(accessToken, refreshToken);
            var projectsTask = _voidRegistryService.GetProjectsForActorAsync(accessToken);

            await Task.WhenAll(providersTask, sessionsTask, projectsTask);

            if (CreatorVoidIdSession?.Profile != null)
            {
                CreatorVoidIdSession.Profile.Providers = providersTask.Result;
            }

            ReplaceCollectionItems(VoidIdActiveSessions, sessionsTask.Result);
            ReplaceCollectionItems(VoidIdMembershipProjects, projectsTask.Result);
            VoidIdControlPlaneStatus = BuildVoidIdControlPlaneStatus();
            VoidIdGitHubProviderStatus = BuildGitHubProviderStatus();
            NotifyVoidIdStateChanged();

            if (showToastOnSuccess)
            {
                ShowToast("VOID ID", "Přehled identity byl obnoven.", ToastSeverity.Success, 2200);
            }
        }
        catch (Exception ex)
        {
            VoidIdControlPlaneStatus = $"Obnovení přehledu identity selhalo: {ex.Message}";
            VoidIdGitHubProviderStatus = BuildGitHubProviderStatus();
            LogService.Error("VOID ID control plane refresh failed", ex);
            if (showToastOnSuccess)
            {
                ShowToast("VOID ID", ex.Message, ToastSeverity.Error, 3600);
            }
        }
        finally
        {
            IsVoidIdControlPlaneLoading = false;
            NotifyVoidIdStateChanged();
        }
    }

    [RelayCommand]
    private async Task LoginVoidId()
    {
        if (IsVoidIdLoginInProgress)
        {
            return;
        }

        IsVoidIdLoginInProgress = true;
        VoidIdVerificationUrl = string.Empty;
        VoidIdUserCode = string.Empty;

        try
        {
            VoidIdLoginStatus = "Spouštím přihlášení do VOID ID přes VOID-CRAFT.EU...";

            var loginAttempt = await _voidIdAuthService.StartOAuthLoginAsync();
            VoidIdVerificationUrl = loginAttempt.AuthorizeUrl;
            VoidIdLoginStatus = "Otevírám autorizaci VOID ID v prohlížeči...";

            try
            {
                OpenUrl(VoidIdVerificationUrl);
            }
            catch (Exception openBrowserEx)
            {
                LogService.Error("VOID ID OAuth browser open failed", openBrowserEx);
                VoidIdLoginStatus = "Prohlížeč se neotevřel automaticky. Použij tlačítko pro opětovné otevření.";
            }

            var session = await _voidIdAuthService.CompleteOAuthLoginAsync(loginAttempt, message =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    VoidIdLoginStatus = message;
                });
            });

            CreatorVoidIdSession = session;
            VoidIdVerificationUrl = string.Empty;
            VoidIdLoginStatus = $"VOID ID přihlášení hotovo: {session.Profile?.DisplayName}";
            ShowToast("VOID ID", $"Přihlášen {session.Profile?.DisplayName}", ToastSeverity.Success, 2600);
            TrackCreatorActivity($"VOID ID přihlášení: {session.Profile?.DisplayName}", "Release");
            await LoadAchievementSnapshotAsync(true);
        }
        catch (Exception ex)
        {
            VoidIdLoginStatus = $"VOID ID login selhal: {ex.Message}";
            ShowToast("VOID ID", ex.Message, ToastSeverity.Error, 3600);
        }
        finally
        {
            IsVoidIdLoginInProgress = false;
        }
    }

    [RelayCommand]
    private async Task LogoutVoidId()
    {
        var accessToken = CreatorVoidIdSession?.AccessToken;
        await _voidIdAuthService.LogoutAsync(accessToken);
        CreatorVoidIdSession = null;
        VoidIdLoginStatus = "Creator byl odhlášen z VOID ID.";
        VoidIdVerificationUrl = string.Empty;
        VoidIdUserCode = string.Empty;
        ClearVoidIdControlPlane();
        ShowToast("VOID ID", "Creator logout dokončen.", ToastSeverity.Success, 2200);
        HandleAchievementIdentityChanged();
    }

    [RelayCommand]
    private async Task RefreshVoidIdControlPlane()
    {
        await RefreshVoidIdControlPlaneAsync(showToastOnSuccess: true);
    }

    [RelayCommand]
    private async Task LinkCreatorGitHubToVoidId()
    {
        if (!CanLinkCreatorGitHubToVoidId)
        {
            return;
        }

        var accessToken = CreatorVoidIdSession?.AccessToken;
        var gitHubAccessToken = CreatorGitHubSession?.AccessToken;
        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(gitHubAccessToken))
        {
            return;
        }

        IsVoidIdGitHubProviderOperationRunning = true;
        VoidIdGitHubProviderStatus = "Napojim aktivni GitHub session na VOID ID provider...";
        try
        {
            var providers = await _voidIdAuthService.LinkGitHubProviderAsync(accessToken, gitHubAccessToken);
            if (CreatorVoidIdSession?.Profile != null)
            {
                CreatorVoidIdSession.Profile.Providers = providers;
            }

            VoidIdGitHubProviderStatus = "GitHub provider byl uspesne napojeny na VOID ID.";
            await RefreshVoidIdControlPlaneAsync(showToastOnSuccess: false);
            ShowToast("VOID ID", "GitHub provider byl napojeny na tvuj VOID ID ucet.", ToastSeverity.Success, 2600);
        }
        catch (Exception ex)
        {
            VoidIdGitHubProviderStatus = $"GitHub provider link selhal: {ex.Message}";
            ShowToast("VOID ID", ex.Message, ToastSeverity.Error, 3600);
        }
        finally
        {
            IsVoidIdGitHubProviderOperationRunning = false;
            NotifyVoidIdStateChanged();
        }
    }

    [RelayCommand]
    private async Task UnlinkCreatorGitHubFromVoidId()
    {
        if (!CanUnlinkCreatorGitHubFromVoidId)
        {
            return;
        }

        var accessToken = CreatorVoidIdSession?.AccessToken;
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return;
        }

        IsVoidIdGitHubProviderOperationRunning = true;
        VoidIdGitHubProviderStatus = "Odpojuju GitHub provider z VOID ID...";
        try
        {
            var providers = await _voidIdAuthService.UnlinkProviderAsync(accessToken, "github");
            if (CreatorVoidIdSession?.Profile != null)
            {
                CreatorVoidIdSession.Profile.Providers = providers;
            }

            VoidIdGitHubProviderStatus = "GitHub provider byl odpojeny z VOID ID.";
            await RefreshVoidIdControlPlaneAsync(showToastOnSuccess: false);
            ShowToast("VOID ID", "GitHub provider byl odpojeny z tvyho VOID ID uctu.", ToastSeverity.Success, 2600);
        }
        catch (Exception ex)
        {
            VoidIdGitHubProviderStatus = $"GitHub provider unlink selhal: {ex.Message}";
            ShowToast("VOID ID", ex.Message, ToastSeverity.Error, 3600);
        }
        finally
        {
            IsVoidIdGitHubProviderOperationRunning = false;
            NotifyVoidIdStateChanged();
        }
    }

    [RelayCommand]
    private async Task RevokeVoidIdSession(string? sessionId)
    {
        if (!HasVoidIdSession || string.IsNullOrWhiteSpace(sessionId))
        {
            return;
        }

        if (!await EnsureFreshVoidIdSessionAsync(TimeSpan.FromMinutes(2)))
        {
            return;
        }

        var accessToken = CreatorVoidIdSession?.AccessToken;
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return;
        }

        try
        {
            var currentSessionId = VoidIdAuthService.GetRefreshSessionId(CreatorVoidIdSession?.RefreshToken);
            var isCurrentSession = string.Equals(currentSessionId, sessionId, StringComparison.OrdinalIgnoreCase);

            await _voidIdAuthService.RevokeSessionAsync(accessToken, sessionId);

            if (isCurrentSession)
            {
                await LogoutVoidId();
                return;
            }

            await RefreshVoidIdControlPlaneAsync(showToastOnSuccess: false);
            ShowToast("VOID ID", "Vybrana session byla odpojena.", ToastSeverity.Success, 2400);
        }
        catch (Exception ex)
        {
            ShowToast("VOID ID", ex.Message, ToastSeverity.Error, 3600);
        }
    }

    [RelayCommand]
    private async Task CopyVoidIdCode()
    {
        if (string.IsNullOrWhiteSpace(VoidIdUserCode))
        {
            return;
        }

        try
        {
            var topLevel = Avalonia.Controls.TopLevel.GetTopLevel(
                Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                    ? desktop.MainWindow
                    : null);

            if (topLevel?.Clipboard != null)
            {
                await topLevel.Clipboard.SetTextAsync(VoidIdUserCode);
                ShowToast("VOID ID", "Device code zkopírován.", ToastSeverity.Success, 1800);
            }
        }
        catch
        {
        }
    }

    private void NotifyVoidIdStateChanged()
    {
        OnPropertyChanged(nameof(HasVoidIdSession));
        OnPropertyChanged(nameof(VoidIdDisplayName));
        OnPropertyChanged(nameof(VoidIdRoleLabel));
        OnPropertyChanged(nameof(VoidIdMinecraftLabel));
        OnPropertyChanged(nameof(VoidIdAvatarUrl));
        OnPropertyChanged(nameof(VoidIdLastLoginLabel));
        OnPropertyChanged(nameof(VoidIdSessionSummary));
        OnPropertyChanged(nameof(VoidIdMembershipSummary));
        OnPropertyChanged(nameof(VoidIdSecuritySummary));
        OnPropertyChanged(nameof(HasVoidIdAdminRole));
        OnPropertyChanged(nameof(HasVoidIdTeamAccess));
        OnPropertyChanged(nameof(CanAccessVoidIdAdminSurface));
        OnPropertyChanged(nameof(VoidIdAccessLevelLabel));
        OnPropertyChanged(nameof(VoidIdAccessSummary));
        OnPropertyChanged(nameof(VoidIdProfileEntryUrl));
        OnPropertyChanged(nameof(VoidIdProfileEntryLabel));
        OnPropertyChanged(nameof(HasVoidIdLoginUrl));
        OnPropertyChanged(nameof(HasVoidIdVerificationCode));
        OnPropertyChanged(nameof(HasVoidIdActiveSessions));
        OnPropertyChanged(nameof(HasVoidIdMembershipProjects));
        OnPropertyChanged(nameof(CanRefreshVoidIdControlPlane));
        OnPropertyChanged(nameof(VoidIdControlPlaneRefreshLabel));
        OnPropertyChanged(nameof(HasMicrosoftLauncherAccount));
        OnPropertyChanged(nameof(MicrosoftLauncherAccountName));
        OnPropertyChanged(nameof(MicrosoftLauncherAccountDetail));
        OnPropertyChanged(nameof(MicrosoftLauncherSyncLabel));
        OnPropertyChanged(nameof(CanLoginMicrosoftFromIdentity));
        OnPropertyChanged(nameof(VoidIdDiscordAccountName));
        OnPropertyChanged(nameof(VoidIdDiscordAccountDetail));
        OnPropertyChanged(nameof(IsVoidIdGitHubLinked));
        OnPropertyChanged(nameof(VoidIdGitHubAccountName));
        OnPropertyChanged(nameof(VoidIdGitHubAccountDetail));
        OnPropertyChanged(nameof(CanLinkCreatorGitHubToVoidId));
        OnPropertyChanged(nameof(CanUnlinkCreatorGitHubFromVoidId));
        OnPropertyChanged(nameof(CanCreatorPublishRelease));
        OnPropertyChanged(nameof(CreatorPublishActionLabel));
        OnPropertyChanged(nameof(CreatorPublishReadinessLabel));
        NotifyCreatorCollaboratorStateChanged();
    }
}