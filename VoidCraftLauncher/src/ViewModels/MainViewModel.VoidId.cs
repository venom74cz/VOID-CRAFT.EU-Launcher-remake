using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
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

    public bool HasVoidIdLoginUrl => !string.IsNullOrWhiteSpace(VoidIdVerificationUrl);

    public bool HasVoidIdSession => CreatorVoidIdSession?.IsAuthenticated == true;

    public string VoidIdDisplayName => CreatorVoidIdSession?.Profile?.DisplayName ?? "Nepřihlášen";

    public string VoidIdRoleLabel => CreatorVoidIdSession?.Profile?.Role ?? "guest";

    public string VoidIdMinecraftLabel => CreatorVoidIdSession?.Profile?.HasMinecraftLink == true
        ? $"MC: {FirstNonEmpty(CreatorVoidIdSession.Profile.MinecraftName, CreatorVoidIdSession.Profile.MinecraftUuid, "linked") }"
        : "MC účet zatím není propojený";

    public string VoidIdAvatarUrl => CreatorVoidIdSession?.Profile?.AvatarUrl ?? string.Empty;

    public bool HasVoidIdVerificationCode => !string.IsNullOrWhiteSpace(VoidIdUserCode);

    partial void OnVoidIdVerificationUrlChanged(string value) => OnPropertyChanged(nameof(HasVoidIdLoginUrl));

    partial void OnVoidIdUserCodeChanged(string value) => OnPropertyChanged(nameof(HasVoidIdVerificationCode));

    partial void OnCreatorVoidIdSessionChanged(VoidIdSession? value)
    {
        NotifyVoidIdStateChanged();
        HandleAchievementIdentityChanged();
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
            VoidIdLoginStatus = "Spouštím VOID ID OAuth přes VOID-CRAFT.EU API...";

            var loginAttempt = await _voidIdAuthService.StartOAuthLoginAsync();
            VoidIdVerificationUrl = loginAttempt.AuthorizeUrl;
            VoidIdLoginStatus = "Otevírám VOID ID autorizaci v prohlížeči...";

            try
            {
                OpenUrl(VoidIdVerificationUrl);
            }
            catch (Exception openBrowserEx)
            {
                LogService.Error("VOID ID OAuth browser open failed", openBrowserEx);
                VoidIdLoginStatus = "Prohlížeč se neotevřel automaticky. Použij tlačítko Otevřít znovu.";
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
        ShowToast("VOID ID", "Creator logout dokončen.", ToastSeverity.Success, 2200);
        HandleAchievementIdentityChanged();
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
        OnPropertyChanged(nameof(HasVoidIdLoginUrl));
        OnPropertyChanged(nameof(HasVoidIdVerificationCode));
        OnPropertyChanged(nameof(CanCreatorPublishRelease));
        OnPropertyChanged(nameof(CreatorPublishActionLabel));
        OnPropertyChanged(nameof(CreatorPublishReadinessLabel));
    }
}