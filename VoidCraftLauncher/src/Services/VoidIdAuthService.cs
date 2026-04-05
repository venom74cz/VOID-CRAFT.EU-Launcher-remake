using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using VoidCraftLauncher.Models;

namespace VoidCraftLauncher.Services;

public sealed class VoidIdAuthService
{
    private const string BaseApiUrl = "https://api.void-craft.eu";
    private const string AccessTokenKey = "voidid.access_token";
    private const string RefreshTokenKey = "voidid.refresh_token";
    private const string ProfileKey = "voidid.profile";
    private const string AccessTokenExpiresAtKey = "voidid.access_token_expires_at";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient;
    private readonly SecureStorageService _secureStorage;

    public VoidIdAuthService(HttpClient httpClient, SecureStorageService secureStorage)
    {
        _httpClient = httpClient;
        _secureStorage = secureStorage;
    }

    public async Task<VoidIdSession?> LoadCachedSessionAsync()
    {
        var accessToken = _secureStorage.Get(AccessTokenKey) ?? string.Empty;
        var refreshToken = _secureStorage.Get(RefreshTokenKey) ?? string.Empty;
        var profileJson = _secureStorage.Get(ProfileKey);
        var expiresAtUtc = ReadStoredAccessTokenExpiry() ?? ParseJwtExpirationUtc(accessToken);

        VoidIdProfile? profile = null;
        if (!string.IsNullOrWhiteSpace(profileJson))
        {
            try
            {
                profile = JsonSerializer.Deserialize<VoidIdProfile>(profileJson, JsonOptions);
            }
            catch (Exception ex)
            {
                LogService.Error("VOID ID: failed to deserialize cached profile", ex);
            }
        }

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return string.IsNullOrWhiteSpace(refreshToken) ? null : await TryRefreshAsync();
        }

        if (expiresAtUtc.HasValue && expiresAtUtc <= DateTimeOffset.UtcNow.AddMinutes(1) && !string.IsNullOrWhiteSpace(refreshToken))
        {
            return await TryRefreshAsync();
        }

        if (profile == null)
        {
            profile = await GetProfileAsync(accessToken);
            if (profile == null && !string.IsNullOrWhiteSpace(refreshToken))
            {
                return await TryRefreshAsync();
            }
        }

        if (profile == null)
        {
            return null;
        }

        var session = new VoidIdSession
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            AccessTokenExpiresAtUtc = expiresAtUtc,
            Profile = profile
        };

        PersistSession(session);
        return session;
    }

    public async Task<VoidIdOAuthLoginAttempt> StartOAuthLoginAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseApiUrl}/api/auth/launcher/start");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(ParseApiError(body, "VOID ID OAuth start selhal."));
        }

        var attempt = JsonSerializer.Deserialize<VoidIdOAuthLoginAttempt>(body, JsonOptions);
        if (attempt == null ||
            string.IsNullOrWhiteSpace(attempt.LoginId) ||
            string.IsNullOrWhiteSpace(attempt.PollToken) ||
            string.IsNullOrWhiteSpace(attempt.AuthorizeUrl))
        {
            throw new InvalidOperationException("VOID ID OAuth start nevrátil platná data.");
        }

        if (attempt.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            attempt.ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(10);
        }

        attempt.PollIntervalMs = Math.Max(1000, attempt.PollIntervalMs);
        return attempt;
    }

    public async Task<VoidIdSession> CompleteOAuthLoginAsync(VoidIdOAuthLoginAttempt loginAttempt, Action<string>? statusCallback = null, CancellationToken cancellationToken = default)
    {
        if (loginAttempt == null ||
            string.IsNullOrWhiteSpace(loginAttempt.LoginId) ||
            string.IsNullOrWhiteSpace(loginAttempt.PollToken))
        {
            throw new InvalidOperationException("VOID ID OAuth login attempt je neplatný.");
        }

        return await PollOAuthLoginAsync(loginAttempt, statusCallback, cancellationToken);
    }

    public async Task<VoidIdSession?> TryRefreshAsync()
    {
        var refreshToken = _secureStorage.Get(RefreshTokenKey);
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return null;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseApiUrl}/api/auth/refresh")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { refresh_token = refreshToken, device_label = "VoidCraftLauncher" }),
                Encoding.UTF8,
                "application/json")
        };

        using var response = await _httpClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            await ClearSessionAsync();
            return null;
        }

        var session = BuildSessionFromJson(body, refreshToken);
        if (session?.Profile == null && !string.IsNullOrWhiteSpace(session?.AccessToken))
        {
            session.Profile = await GetProfileAsync(session.AccessToken);
        }

        if (session?.Profile == null)
        {
            await ClearSessionAsync();
            return null;
        }

        PersistSession(session);
        return session;
    }

    public async Task<VoidIdProfile?> GetProfileAsync(string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return null;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseApiUrl}/api/me");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var body = await response.Content.ReadAsStringAsync();
        var node = JsonNode.Parse(body);
        var profileNode = node?["user"] ?? node?["profile"] ?? node;
        return ParseProfile(profileNode);
    }

    public async Task<VoidIdProfile?> RefreshProfileAsync(string accessToken, bool reconcileMinecraft = false, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return null;
        }

        if (reconcileMinecraft)
        {
            using var reconcileRequest = new HttpRequestMessage(HttpMethod.Post, $"{BaseApiUrl}/api/me/minecraft/reconcile");
            reconcileRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            reconcileRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var reconcileResponse = await _httpClient.SendAsync(reconcileRequest, cancellationToken);
            if (!reconcileResponse.IsSuccessStatusCode)
            {
                var reconcileBody = await reconcileResponse.Content.ReadAsStringAsync(cancellationToken);
                LogService.Log($"VOID ID reconcile returned {reconcileResponse.StatusCode}: {ParseApiError(reconcileBody, "reconcile_failed")}");
            }
        }

        return await GetProfileAsync(accessToken);
    }

    public async Task LogoutAsync(string? accessToken = null)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseApiUrl}/api/auth/logout")
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new { refresh_token = _secureStorage.Get(RefreshTokenKey) ?? string.Empty }),
                    Encoding.UTF8,
                    "application/json")
            };

            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            }

            using var response = await _httpClient.SendAsync(request);
            _ = response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            LogService.Error("VOID ID logout request failed", ex);
        }
        finally
        {
            await ClearSessionAsync();
        }
    }

    public Task ClearSessionAsync()
    {
        _secureStorage.RemoveMany(AccessTokenKey, RefreshTokenKey, ProfileKey, AccessTokenExpiresAtKey);
        return Task.CompletedTask;
    }

    private async Task<VoidIdSession> PollOAuthLoginAsync(VoidIdOAuthLoginAttempt loginAttempt, Action<string>? statusCallback, CancellationToken cancellationToken)
    {
        var pollDelayMs = Math.Max(1000, loginAttempt.PollIntervalMs);
        statusCallback?.Invoke("Čekám na potvrzení VOID ID loginu v prohlížeči...");

        while (DateTimeOffset.UtcNow < loginAttempt.ExpiresAtUtc)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{BaseApiUrl}/api/auth/launcher/poll?login_id={Uri.EscapeDataString(loginAttempt.LoginId)}&poll_token={Uri.EscapeDataString(loginAttempt.PollToken)}");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var bodyNode = string.IsNullOrWhiteSpace(body) ? null : JsonNode.Parse(body);
            var pollResponse = string.IsNullOrWhiteSpace(body)
                ? null
                : JsonSerializer.Deserialize<VoidIdOAuthPollResponse>(body, JsonOptions);

            if (response.IsSuccessStatusCode && string.Equals(pollResponse?.Status, "completed", StringComparison.OrdinalIgnoreCase))
            {
                var accessToken = pollResponse?.AccessToken ?? string.Empty;
                var refreshToken = pollResponse?.RefreshToken ?? string.Empty;
                var profile = ParseProfile(bodyNode?["user"] ?? bodyNode?["profile"]);

                if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(refreshToken) || profile == null)
                {
                    throw new InvalidOperationException("VOID ID OAuth poll vrátil neplatnou session.");
                }

                var session = new VoidIdSession
                {
                    AccessToken = accessToken,
                    RefreshToken = refreshToken,
                    Profile = profile,
                    AccessTokenExpiresAtUtc = ParseJwtExpirationUtc(accessToken) ?? DateTimeOffset.UtcNow.AddMinutes(15)
                };

                if (session.Profile == null)
                {
                    throw new InvalidOperationException("VOID ID login nevrátil profil uživatele.");
                }

                PersistSession(session);
                return session;
            }

            if (response.StatusCode == HttpStatusCode.Accepted && string.Equals(pollResponse?.Status, "pending", StringComparison.OrdinalIgnoreCase))
            {
                pollDelayMs = Math.Max(1000, pollResponse?.PollIntervalMs ?? pollDelayMs);
                if (pollResponse?.ExpiresAtUtc is DateTimeOffset expiresAtUtc && expiresAtUtc > DateTimeOffset.UtcNow)
                {
                    loginAttempt.ExpiresAtUtc = expiresAtUtc;
                }

                statusCallback?.Invoke(string.IsNullOrWhiteSpace(pollResponse?.Message)
                    ? "Čekám na potvrzení VOID ID loginu v prohlížeči..."
                    : pollResponse.Message);
            }
            else
            {
                throw new InvalidOperationException(ParseApiError(body, "VOID ID OAuth login selhal."));
            }

            await Task.Delay(TimeSpan.FromMilliseconds(pollDelayMs), cancellationToken);
        }

        throw new TimeoutException("VOID ID OAuth přihlášení vypršelo. Spusť login znovu.");
    }

    private static VoidIdSession? BuildSessionFromJson(string json, string fallbackRefreshToken)
    {
        var node = JsonNode.Parse(json);
        var accessToken = node?["access_token"]?.ToString() ?? node?["accessToken"]?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return null;
        }

        DateTimeOffset? expiresAtUtc = null;
        if (int.TryParse(node?["expires_in"]?.ToString() ?? node?["expiresIn"]?.ToString(), out var expiresInSeconds) && expiresInSeconds > 0)
        {
            expiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(expiresInSeconds);
        }

        expiresAtUtc ??= ParseJwtExpirationUtc(accessToken);

        var profileNode = node?["user"] ?? node?["profile"];
        var profile = ParseProfile(profileNode);

        return new VoidIdSession
        {
            AccessToken = accessToken,
            RefreshToken = node?["refresh_token"]?.ToString() ?? node?["refreshToken"]?.ToString() ?? fallbackRefreshToken,
            AccessTokenExpiresAtUtc = expiresAtUtc,
            Profile = profile
        };
    }

    private static VoidIdProfile? ParseProfile(JsonNode? profileNode)
    {
        if (profileNode == null)
        {
            return null;
        }

        var profile = new VoidIdProfile
        {
            VoidId = profileNode["void_id"]?.ToString() ?? profileNode["voidId"]?.ToString() ?? profileNode["id"]?.ToString() ?? string.Empty,
            DisplayName = profileNode["display_name"]?.ToString() ?? profileNode["displayName"]?.ToString() ?? string.Empty,
            AvatarUrl = profileNode["avatar_url"]?.ToString() ?? profileNode["avatarUrl"]?.ToString() ?? string.Empty,
            Role = profileNode["role"]?.ToString() ?? "player",
            DiscordId = profileNode["discord_id"]?.ToString() ?? profileNode["discordId"]?.ToString() ?? profileNode["discord"]?["id"]?.ToString() ?? string.Empty,
            MinecraftUuid = profileNode["minecraft_uuid"]?.ToString() ?? profileNode["minecraftUuid"]?.ToString() ?? profileNode["minecraftLink"]?["uuid"]?.ToString() ?? string.Empty,
            MinecraftName = profileNode["minecraft_name"]?.ToString() ?? profileNode["minecraftName"]?.ToString() ?? profileNode["minecraftLink"]?["username"]?.ToString() ?? string.Empty
        };

        return string.IsNullOrWhiteSpace(profile.DisplayName) && string.IsNullOrWhiteSpace(profile.VoidId)
            ? null
            : profile;
    }

    private static string ParseApiError(string? body, string fallback)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return fallback;
        }

        try
        {
            var node = JsonNode.Parse(body);
            return node?["message"]?.ToString()
                ?? node?["error"]?.ToString()
                ?? fallback;
        }
        catch
        {
            return body.Length <= 300 ? body : body[..300];
        }
    }

    private void PersistSession(VoidIdSession session)
    {
        var values = new Dictionary<string, string>();

        if (!string.IsNullOrWhiteSpace(session.AccessToken))
        {
            values[AccessTokenKey] = session.AccessToken;
        }

        if (!string.IsNullOrWhiteSpace(session.RefreshToken))
        {
            values[RefreshTokenKey] = session.RefreshToken;
        }

        if (session.Profile != null)
        {
            values[ProfileKey] = JsonSerializer.Serialize(session.Profile, JsonOptions);
        }

        if (session.AccessTokenExpiresAtUtc.HasValue)
        {
            values[AccessTokenExpiresAtKey] = session.AccessTokenExpiresAtUtc.Value.ToString("O", CultureInfo.InvariantCulture);
            _secureStorage.SetMany(values);
        }
        else
        {
            _secureStorage.Remove(AccessTokenExpiresAtKey);
            if (values.Count > 0)
            {
                _secureStorage.SetMany(values);
            }
        }
    }

    private DateTimeOffset? ReadStoredAccessTokenExpiry()
    {
        var rawValue = _secureStorage.Get(AccessTokenExpiresAtKey);
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        return DateTimeOffset.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expiresAtUtc)
            ? expiresAtUtc
            : null;
    }

    private static DateTimeOffset? ParseJwtExpirationUtc(string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return null;
        }

        var parts = accessToken.Split('.');
        if (parts.Length < 2)
        {
            return null;
        }

        try
        {
            var payloadBytes = DecodeBase64Url(parts[1]);
            using var document = JsonDocument.Parse(payloadBytes);
            if (!document.RootElement.TryGetProperty("exp", out var expElement))
            {
                return null;
            }

            long expUnixSeconds;
            if (expElement.ValueKind == JsonValueKind.Number)
            {
                if (!expElement.TryGetInt64(out expUnixSeconds))
                {
                    return null;
                }
            }
            else if (expElement.ValueKind == JsonValueKind.String && long.TryParse(expElement.GetString(), out var parsedValue))
            {
                expUnixSeconds = parsedValue;
            }
            else
            {
                return null;
            }

            return DateTimeOffset.FromUnixTimeSeconds(expUnixSeconds);
        }
        catch
        {
            return null;
        }
    }

    private static byte[] DecodeBase64Url(string value)
    {
        var normalized = value.Replace('-', '+').Replace('_', '/');
        var padding = normalized.Length % 4;
        if (padding > 0)
        {
            normalized = normalized.PadRight(normalized.Length + (4 - padding), '=');
        }

        return Convert.FromBase64String(normalized);
    }
}