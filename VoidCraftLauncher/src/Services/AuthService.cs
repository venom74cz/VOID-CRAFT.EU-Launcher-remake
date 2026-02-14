using CmlLib.Core.Auth;
using Microsoft.Identity.Client;
using System.IO;
using System.Threading.Tasks;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Linq;
using System.Diagnostics;

namespace VoidCraftLauncher.Services;

public class AuthService
{
    // Azure Client ID for VoidCraft Launcher
    private const string ClientId = "a12295b0-3505-46f1-a299-88ae9cc80174";
    private readonly IPublicClientApplication _msalApp;
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly string _tokenCacheFile;

    public AuthService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var cachePath = Path.Combine(appData, ".voidcraft", "auth_cache");
        Directory.CreateDirectory(cachePath);
        _tokenCacheFile = Path.Combine(cachePath, "msal_token_cache.bin");

        _msalApp = PublicClientApplicationBuilder.Create(ClientId)
            .WithAuthority("https://login.microsoftonline.com/consumers")
            .WithDefaultRedirectUri()
            .Build();
        
        // Enable token cache persistence
        _msalApp.UserTokenCache.SetBeforeAccess(BeforeAccessNotification);
        _msalApp.UserTokenCache.SetAfterAccess(AfterAccessNotification);
            
        _httpClient = new HttpClient();
        _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    }

    private void BeforeAccessNotification(TokenCacheNotificationArgs args)
    {
        if (File.Exists(_tokenCacheFile))
        {
            args.TokenCache.DeserializeMsalV3(File.ReadAllBytes(_tokenCacheFile));
        }
    }

    private void AfterAccessNotification(TokenCacheNotificationArgs args)
    {
        if (args.HasStateChanged)
        {
            File.WriteAllBytes(_tokenCacheFile, args.TokenCache.SerializeMsalV3());
        }
    }

    /// <summary>
    /// Try to login silently using cached tokens (for startup)
    /// </summary>
    public async Task<MSession?> TrySilentLoginAsync()
    {
        var scopes = new[] { "XboxLive.signin", "XboxLive.offline_access" };
        
        try
        {
            var accounts = await _msalApp.GetAccountsAsync();
            var account = accounts.FirstOrDefault();
            
            if (account == null)
                return null;
            
            var authResult = await _msalApp.AcquireTokenSilent(scopes, account)
                .ExecuteAsync();
            
            // Got MS token, now get Xbox/MC tokens
            var userToken = await RequestXboxUserToken(authResult.AccessToken);
            var xsts = await RequestXstsToken(userToken.Token);
            string userHash = ExtractUserHash(xsts);
            var mcToken = await LoginToMinecraftAsync(userHash, xsts.Token);
            var profile = await GetMinecraftProfileAsync(mcToken);

            var mSession = new MSession(profile.Name, mcToken, profile.Id);
            mSession.UserType = "msa";
            return mSession;
        }
        catch
        {
            return null; // Silent login failed, need interactive
        }
    }

    /// <summary>
    /// Browser OAuth flow - opens browser, auto-redirects back
    /// </summary>
    public async Task<MSession> LoginWithBrowserAsync(Action<string> statusCallback)
    {
        statusCallback("Otevírám přihlášení...");
        
        var scopes = new[] { "XboxLive.signin", "XboxLive.offline_access" };
        
        AuthenticationResult authResult;
        try
        {
            // Try silent login first (cached tokens)
            var accounts = await _msalApp.GetAccountsAsync();
            authResult = await _msalApp.AcquireTokenSilent(scopes, accounts.FirstOrDefault())
                .ExecuteAsync();
        }
        catch (MsalUiRequiredException)
        {
            if (OperatingSystem.IsLinux())
            {
                authResult = await AcquireTokenWithDeviceCodeAsync(scopes, statusCallback);
            }
            else
            {
                try
                {
                    statusCallback("Čekám na přihlášení v prohlížeči...");
                    authResult = await _msalApp.AcquireTokenInteractive(scopes)
                        .WithSystemWebViewOptions(new SystemWebViewOptions
                        {
                            HtmlMessageSuccess = "<html><body style='font-family:sans-serif;text-align:center;padding:50px;background:#1a1a1a;color:white;'><h1>✅ Přihlášení úspěšné!</h1><p>Můžeš zavřít toto okno a vrátit se do launcheru.</p></body></html>",
                            HtmlMessageError = "<html><body style='font-family:sans-serif;text-align:center;padding:50px;background:#1a1a1a;color:white;'><h1>❌ Chyba</h1><p>Přihlášení selhalo.</p></body></html>"
                        })
                        .ExecuteAsync();
                }
                catch (MsalClientException)
                {
                    authResult = await AcquireTokenWithDeviceCodeAsync(scopes, statusCallback);
                }
            }
        }

        statusCallback("Ověřuji Xbox Live...");
        
        // Xbox authentication chain with proper headers
        var userToken = await RequestXboxUserToken(authResult.AccessToken);
        
        statusCallback("Ověřuji XSTS...");
        var xsts = await RequestXstsToken(userToken.Token);
        
        string userHash = ExtractUserHash(xsts);
        if (string.IsNullOrEmpty(userHash)) 
            throw new Exception("Nepodařilo se získat UserHash z Xbox tokenu");

        statusCallback("Přihlašuji do Minecraftu...");
        var mcToken = await LoginToMinecraftAsync(userHash, xsts.Token);

        statusCallback("Načítám profil...");
        var profile = await GetMinecraftProfileAsync(mcToken);

        var mSession = new MSession(profile.Name, mcToken, profile.Id);
        mSession.UserType = "msa";
        // Note: Xuid stored in userHash but MSession v3 doesn't have this property
        return mSession;
    }

    private async Task<AuthenticationResult> AcquireTokenWithDeviceCodeAsync(string[] scopes, Action<string> statusCallback)
    {
        statusCallback("Linux login fallback: Device Code...");

        return await _msalApp.AcquireTokenWithDeviceCode(scopes, deviceCodeResult =>
        {
            statusCallback($"Otevři {deviceCodeResult.VerificationUrl} a zadej kód: {deviceCodeResult.UserCode}");
            TryOpenBrowser(deviceCodeResult.VerificationUrl);
            return Task.CompletedTask;
        }).ExecuteAsync();
    }

    private static void TryOpenBrowser(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;

        try
        {
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                return;
            }

            if (OperatingSystem.IsLinux())
            {
                Process.Start("xdg-open", url);
                return;
            }

            if (OperatingSystem.IsMacOS())
            {
                Process.Start("open", url);
            }
        }
        catch
        {
        }
    }

    private string ExtractUserHash(XboxAuthResponse xsts)
    {
        if (xsts.DisplayClaims != null && xsts.DisplayClaims.TryGetValue("xui", out var xui))
        {
            var claim = xui.FirstOrDefault();
            if (claim != null && claim.TryGetValue("uhs", out var uhs))
            {
                return uhs;
            }
        }
        return null;
    }

    private async Task<XboxAuthResponse> RequestXboxUserToken(string accessToken)
    {
        var url = "https://user.auth.xboxlive.com/user/authenticate";
        var payloadObj = new 
        {
            Properties = new 
            {
                AuthMethod = "RPS",
                SiteName = "user.auth.xboxlive.com",
                RpsTicket = $"d={accessToken}"
            },
            RelyingParty = "http://auth.xboxlive.com",
            TokenType = "JWT"
        };
        
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = new StringContent(JsonSerializer.Serialize(payloadObj), System.Text.Encoding.UTF8, "application/json");
        request.Headers.Add("Accept", "application/json");
        request.Headers.Add("x-xbl-contract-version", "1");
        
        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new Exception($"Xbox User Auth failed ({response.StatusCode}): {errorBody}");
        }
        return await response.Content.ReadFromJsonAsync<XboxAuthResponse>(_jsonOptions)
            ?? throw new Exception("Xbox User Auth response deserialization returned null");
    }

    private async Task<XboxAuthResponse> RequestXstsToken(string userToken)
    {
        var url = "https://xsts.auth.xboxlive.com/xsts/authorize";
        var payloadObj = new 
        {
            Properties = new 
            {
                SandboxId = "RETAIL",
                UserTokens = new[] { userToken }
            },
            RelyingParty = "rp://api.minecraftservices.com/",
            TokenType = "JWT"
        };

        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = new StringContent(JsonSerializer.Serialize(payloadObj), System.Text.Encoding.UTF8, "application/json");
        request.Headers.Add("Accept", "application/json");
        request.Headers.Add("x-xbl-contract-version", "1");

        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new Exception($"XSTS failed ({response.StatusCode}): {errorBody}");
        }
        return await response.Content.ReadFromJsonAsync<XboxAuthResponse>(_jsonOptions) 
            ?? throw new Exception("XSTS response deserialization returned null");
    }

    private async Task<string> LoginToMinecraftAsync(string userHash, string xstsToken)
    {
        var url = "https://api.minecraftservices.com/authentication/login_with_xbox";
        var payload = new 
        { 
            identityToken = $"XBL3.0 x={userHash};{xstsToken}" 
        };

        var response = await _httpClient.PostAsJsonAsync(url, payload);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new Exception($"Minecraft Login failed ({response.StatusCode}): {errorBody}");
        }
        
        var result = await response.Content.ReadFromJsonAsync<McLoginResponse>(_jsonOptions);
        return result?.AccessToken ?? throw new Exception("Nepodařilo se získat Minecraft token");
    }

    private async Task<McProfileResponse> GetMinecraftProfileAsync(string accessToken)
    {
        var url = "https://api.minecraftservices.com/minecraft/profile";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new Exception($"Profile fetch failed ({response.StatusCode}): {errorBody}");
        }

        var result = await response.Content.ReadFromJsonAsync<McProfileResponse>(_jsonOptions);
        return result ?? throw new Exception("Nepodařilo se načíst Minecraft profil");
    }

    public async Task LogoutAsync()
    {
        var accounts = await _msalApp.GetAccountsAsync();
        foreach (var account in accounts)
        {
            await _msalApp.RemoveAsync(account);
        }

        if (File.Exists(_tokenCacheFile))
        {
            File.Delete(_tokenCacheFile);
        }
    }

    public MSession LoginOffline(string username)
    {
        return MSession.CreateOfflineSession(username);
    }
}

// DTO Classes
public class XboxAuthResponse
{
    public string Token { get; set; }
    public Dictionary<string, List<Dictionary<string, string>>> DisplayClaims { get; set; }
}

public class McLoginResponse 
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; }
}

public class McProfileResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; }
    
    [JsonPropertyName("name")]
    public string Name { get; set; }
}
