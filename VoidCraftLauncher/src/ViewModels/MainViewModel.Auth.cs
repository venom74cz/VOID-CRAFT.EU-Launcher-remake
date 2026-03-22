using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VoidCraftLauncher.Models;
using VoidCraftLauncher.Services;
using CmlLib.Core.Auth;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace VoidCraftLauncher.ViewModels;

/// <summary>
/// Authentication, login/logout, multi-account management.
/// </summary>
public partial class MainViewModel
{
    // ===== AUTH STATE =====

    // Properties are declared in the core file as they are referenced by constructor init.
    // This file contains only the METHODS.

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
                    var existingMicrosoftProfile = Accounts.FirstOrDefault(account =>
                        account.Type == AccountType.Microsoft &&
                        !string.IsNullOrWhiteSpace(account.Uuid) &&
                        string.Equals(account.Uuid, fallbackSession.UUID, StringComparison.OrdinalIgnoreCase));

                    if (existingMicrosoftProfile != null)
                    {
                        existingMicrosoftProfile.DisplayName = fallbackSession.Username;
                        existingMicrosoftProfile.Uuid = fallbackSession.UUID;
                        existingMicrosoftProfile.LastUsed = DateTime.UtcNow;
                        ActiveAccount = existingMicrosoftProfile;
                    }
                    else
                    {
                        var recoveredProfile = new AccountProfile
                        {
                            DisplayName = fallbackSession.Username,
                            Uuid = fallbackSession.UUID,
                            Type = AccountType.Microsoft,
                            LastUsed = DateTime.UtcNow
                        };
                        Accounts.Add(recoveredProfile);
                        ActiveAccount = recoveredProfile;
                    }

                    UserSession = fallbackSession;
                    IsLoggedIn = true;
                    SaveAccountProfiles();
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
        Config.Accounts = new System.Collections.Generic.List<AccountProfile>(Accounts);
        Config.ActiveAccountId = ActiveAccount?.Id;
        _launcherService.SaveConfig(Config);
    }
}
