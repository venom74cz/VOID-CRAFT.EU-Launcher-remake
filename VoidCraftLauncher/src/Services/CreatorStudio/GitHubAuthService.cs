using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using VoidCraftLauncher.Models;
using VoidCraftLauncher.Services;

namespace VoidCraftLauncher.Services.CreatorStudio;

public sealed class GitHubAuthService
{
    private const string BaseApiUrl = "https://api.void-craft.eu";
    private const string AccessTokenKey = "github.access_token";
    private const string ScopeKey = "github.scope";
    private const string ProfileKey = "github.profile";
    private const string ApiVersion = "2022-11-28";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient;
    private readonly SecureStorageService _secureStorage;

    public GitHubAuthService(HttpClient httpClient, SecureStorageService secureStorage)
    {
        _httpClient = httpClient;
        _secureStorage = secureStorage;
    }

    public async Task<GitHubSession?> LoadCachedSessionAsync(CancellationToken cancellationToken = default)
    {
        var accessToken = _secureStorage.Get(AccessTokenKey) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return null;
        }

        GitHubUserProfile? profile = null;
        var profileJson = _secureStorage.Get(ProfileKey);
        if (!string.IsNullOrWhiteSpace(profileJson))
        {
            try
            {
                profile = JsonSerializer.Deserialize<GitHubUserProfile>(profileJson, JsonOptions);
            }
            catch (Exception ex)
            {
                LogService.Error("GitHub: failed to deserialize cached profile", ex);
            }
        }

        profile ??= await GetCurrentUserAsync(accessToken, cancellationToken);
        if (profile == null)
        {
            await LogoutAsync();
            return null;
        }

        var session = new GitHubSession
        {
            AccessToken = accessToken,
            Scope = _secureStorage.Get(ScopeKey) ?? string.Empty,
            Profile = profile
        };

        PersistSession(session);
        return session;
    }

    public async Task<GitHubOAuthLoginAttempt> StartOAuthLoginAsync(System.Threading.CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseApiUrl}/api/github/oauth/start");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(ParseApiError(body, "GitHub OAuth start selhal."));
        }

        var attempt = JsonSerializer.Deserialize<GitHubOAuthLoginAttempt>(body, JsonOptions);
        if (attempt == null ||
            string.IsNullOrWhiteSpace(attempt.LoginId) ||
            string.IsNullOrWhiteSpace(attempt.PollToken) ||
            string.IsNullOrWhiteSpace(attempt.AuthorizeUrl))
        {
            throw new InvalidOperationException("GitHub OAuth start nevrátil platná data.");
        }

        if (attempt.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            attempt.ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(10);
        }

        attempt.PollIntervalMs = Math.Max(1000, attempt.PollIntervalMs);
        return attempt;
    }

    public async Task<GitHubSession> CompleteOAuthLoginAsync(GitHubOAuthLoginAttempt loginAttempt, Action<string>? statusCallback = null, System.Threading.CancellationToken cancellationToken = default)
    {
        if (loginAttempt == null ||
            string.IsNullOrWhiteSpace(loginAttempt.LoginId) ||
            string.IsNullOrWhiteSpace(loginAttempt.PollToken))
        {
            throw new InvalidOperationException("GitHub OAuth login attempt je neplatný.");
        }

        return await PollOAuthLoginAsync(loginAttempt, statusCallback, cancellationToken);
    }

    public async Task<GitHubUserProfile?> GetCurrentUserAsync(string accessToken, System.Threading.CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return null;
        }

        using var request = CreateApiRequest(HttpMethod.Get, "https://api.github.com/user", accessToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var root = JsonNode.Parse(body);
        return new GitHubUserProfile
        {
            Login = root?["login"]?.ToString() ?? string.Empty,
            Name = root?["name"]?.ToString() ?? string.Empty,
            AvatarUrl = root?["avatar_url"]?.ToString() ?? string.Empty,
            HtmlUrl = root?["html_url"]?.ToString() ?? string.Empty,
            Email = root?["email"]?.ToString() ?? string.Empty
        };
    }

    public async Task<GitHubRepositoryInfo> CreateRepositoryAsync(string accessToken, GitHubRepositoryCreationRequest request, System.Threading.CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException("GitHub access token chybí.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("Název GitHub repozitáře je povinný.");
        }

        var profile = await GetCurrentUserAsync(accessToken, cancellationToken)
            ?? throw new InvalidOperationException("Nepodařilo se načíst GitHub profil přihlášeného uživatele.");

        var owner = string.IsNullOrWhiteSpace(request.Owner) ? profile.Login : request.Owner.Trim();
        var endpoint = string.Equals(owner, profile.Login, StringComparison.OrdinalIgnoreCase)
            ? "https://api.github.com/user/repos"
            : $"https://api.github.com/orgs/{Uri.EscapeDataString(owner)}/repos";

        var payload = new JsonObject
        {
            ["name"] = request.Name.Trim(),
            ["description"] = NullIfWhiteSpace(request.Description),
            ["homepage"] = NullIfWhiteSpace(request.Homepage),
            ["private"] = request.IsPrivate,
            ["has_issues"] = true,
            ["has_projects"] = true,
            ["has_wiki"] = true,
            ["has_discussions"] = false,
            ["auto_init"] = request.AutoInitializeWithReadme
        };

        using var apiRequest = CreateApiRequest(HttpMethod.Post, endpoint, accessToken);
        apiRequest.Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(apiRequest, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(BuildRepositoryCreationError(response, body, owner, request.Name.Trim()));
        }

        var root = JsonNode.Parse(body);
        return ParseRepository(root, owner, request.Name.Trim(), request.IsPrivate);
    }

    public async Task<List<GitHubRepositoryInfo>> ListAccessibleRepositoriesAsync(string accessToken, System.Threading.CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException("GitHub access token chybí.");
        }

        using var request = CreateApiRequest(
            HttpMethod.Get,
            "https://api.github.com/user/repos?visibility=all&affiliation=owner,collaborator,organization_member&sort=updated&per_page=100",
            accessToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(ParseApiError(body, "Nepodařilo se načíst GitHub repozitáře."));
        }

        var root = JsonNode.Parse(body)?.AsArray();
        var repositories = new List<GitHubRepositoryInfo>();
        if (root == null)
        {
            return repositories;
        }

        foreach (var item in root)
        {
            if (item == null)
            {
                continue;
            }

            repositories.Add(ParseRepository(
                item,
                item["owner"]?["login"]?.ToString() ?? string.Empty,
                item["name"]?.ToString() ?? string.Empty,
                item["private"]?.GetValue<bool?>() ?? false));
        }

        return repositories;
    }

    public Task LogoutAsync()
    {
        _secureStorage.RemoveMany(AccessTokenKey, ScopeKey, ProfileKey);
        return Task.CompletedTask;
    }

    private async Task<GitHubSession> PollOAuthLoginAsync(GitHubOAuthLoginAttempt loginAttempt, Action<string>? statusCallback, System.Threading.CancellationToken cancellationToken)
    {
        var pollDelayMs = Math.Max(1000, loginAttempt.PollIntervalMs);
        statusCallback?.Invoke("Čekám na potvrzení GitHub loginu v prohlížeči...");

        while (DateTimeOffset.UtcNow < loginAttempt.ExpiresAtUtc)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{BaseApiUrl}/api/github/oauth/poll?login_id={Uri.EscapeDataString(loginAttempt.LoginId)}&poll_token={Uri.EscapeDataString(loginAttempt.PollToken)}");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var pollResponse = string.IsNullOrWhiteSpace(body)
                ? null
                : JsonSerializer.Deserialize<GitHubOAuthPollResponse>(body, JsonOptions);

            if (response.IsSuccessStatusCode && string.Equals(pollResponse?.Status, "completed", StringComparison.OrdinalIgnoreCase))
            {
                var accessToken = pollResponse?.AccessToken ?? string.Empty;
                var profile = pollResponse?.User;

                if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(profile?.Login))
                {
                    throw new InvalidOperationException("GitHub OAuth poll vrátil neplatnou session.");
                }

                var session = new GitHubSession
                {
                    AccessToken = accessToken,
                    Scope = pollResponse?.Scope ?? string.Empty,
                    Profile = profile,
                    CreatedAtUtc = DateTimeOffset.UtcNow
                };

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
                    ? "Čekám na potvrzení GitHub loginu v prohlížeči..."
                    : pollResponse.Message);
            }
            else
            {
                throw new InvalidOperationException(ParseApiError(body, "GitHub OAuth login selhal."));
            }

            await Task.Delay(TimeSpan.FromMilliseconds(pollDelayMs), cancellationToken);
        }

        throw new TimeoutException("GitHub OAuth přihlášení vypršelo. Spusť login znovu.");
    }

    private void PersistSession(GitHubSession session)
    {
        _secureStorage.SetMany(new Dictionary<string, string>
        {
            [AccessTokenKey] = session.AccessToken,
            [ScopeKey] = session.Scope ?? string.Empty,
            [ProfileKey] = JsonSerializer.Serialize(session.Profile, JsonOptions)
        });
    }

    private static GitHubRepositoryInfo ParseRepository(JsonNode? root, string fallbackOwner, string fallbackName, bool fallbackPrivate)
    {
        return new GitHubRepositoryInfo
        {
            Owner = root?["owner"]?["login"]?.ToString() ?? fallbackOwner,
            Name = root?["name"]?.ToString() ?? fallbackName,
            Description = root?["description"]?.ToString() ?? string.Empty,
            HtmlUrl = root?["html_url"]?.ToString() ?? string.Empty,
            CloneUrl = root?["clone_url"]?.ToString() ?? string.Empty,
            SshUrl = root?["ssh_url"]?.ToString() ?? string.Empty,
            DefaultBranch = root?["default_branch"]?.ToString() ?? "main",
            IsPrivate = root?["private"]?.GetValue<bool?>() ?? fallbackPrivate,
            UpdatedAtUtc = DateTimeOffset.TryParse(root?["updated_at"]?.ToString(), out var updatedAt) ? updatedAt : null
        };
    }

    private static HttpRequestMessage CreateApiRequest(HttpMethod method, string url, string accessToken)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.UserAgent.ParseAdd("VOID-CRAFT.EU-Launcher");
        request.Headers.Add("X-GitHub-Api-Version", ApiVersion);
        return request;
    }

    private static string ParseApiError(string? body, string fallback)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return fallback;
        }

        try
        {
            var root = JsonNode.Parse(body);
            var message = root?["message"]?.ToString();
            if (!string.IsNullOrWhiteSpace(message))
            {
                return message;
            }

            var error = root?["error"]?.ToString();
            if (!string.IsNullOrWhiteSpace(error))
            {
                return error;
            }
        }
        catch
        {
        }

        return fallback;
    }

    private static string BuildRepositoryCreationError(HttpResponseMessage response, string? body, string owner, string repoName)
    {
        var message = ParseApiError(body, "GitHub repo creation selhala.");
        var acceptedPermissions = TryGetAcceptedPermissions(response);

        if (message.Contains("Resource not accessible by integration", StringComparison.OrdinalIgnoreCase))
        {
            var details = new List<string>
            {
                "GitHub odmítl vytvoření repa kvůli nedostatečným oprávněním tokenu.",
                $"GitHub vrátil: {message}."
            };

            if (!string.IsNullOrWhiteSpace(acceptedPermissions))
            {
                details.Add($"Požadovaná oprávnění endpointu: {acceptedPermissions}.");
            }

            details.Add("Tohle typicky znamená, že použitá GitHub App nebo fine-grained token nemá repository Administration: write.");
            details.Add("Pokud VOID-CRAFT.EU používá GitHub App, samotné zapnutí permission v registraci appky nestačí. App musí být zároveň nainstalovaná na cílovém účtu nebo organizaci a tahle instalace musí mít schválené updated permissions.");
            details.Add("Po změně GitHub App permissions udělej v launcheru GitHub odhlásit a znovu přihlásit, aby vznikl nový user token s aktuálními právy.");
            details.Add($"Dočasný fallback: vytvoř repo {owner}/{repoName} ručně v browseru a pak v launcheru klikni Načíst repa -> Použít jako origin.");
            return string.Join(" ", details);
        }

        if (!string.IsNullOrWhiteSpace(acceptedPermissions))
        {
            return $"{message} GitHub požaduje oprávnění: {acceptedPermissions}.";
        }

        return message;
    }

    private static string? TryGetAcceptedPermissions(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("X-Accepted-GitHub-Permissions", out var values))
        {
            return null;
        }

        var headerValue = string.Join(", ", values).Trim();
        return string.IsNullOrWhiteSpace(headerValue) ? null : headerValue;
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}