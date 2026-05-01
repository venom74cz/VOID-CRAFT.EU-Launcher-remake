using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using VoidCraftLauncher.Models;
using VoidCraftLauncher.Models.CreatorStudio;
using VoidCraftLauncher.Services.CreatorStudio;

namespace VoidCraftLauncher.Services;

public sealed class VoidRegistryService
{
    private const string BaseApiUrl = "https://api.void-craft.eu";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient;

    public VoidRegistryService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<VoidRegistryProjectSummary>> SearchProjectsAsync(string query, int page = 1, int limit = 20)
    {
        var url = new StringBuilder($"{BaseApiUrl}/api/registry/projects?page={Math.Max(1, page)}&limit={Math.Clamp(limit, 1, 50)}");
        if (!string.IsNullOrWhiteSpace(query))
        {
            url.Append("&search=").Append(Uri.EscapeDataString(query));
        }

        using var response = await _httpClient.GetAsync(url.ToString());
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"VOID Registry search selhal: {body}");
        }

        var root = JsonNode.Parse(body);
        var items = root as JsonArray
            ?? root?["data"]?.AsArray()
            ?? root?["projects"]?.AsArray()
            ?? new JsonArray();

        return items
            .Select(ParseProjectSummary)
            .Where(project => project != null)
            .Cast<VoidRegistryProjectSummary>()
            .ToList();
    }

    public async Task<VoidRegistryInstallManifest?> GetInstallManifestAsync(string slug)
    {
        using var response = await _httpClient.GetAsync($"{BaseApiUrl}/api/registry/projects/{Uri.EscapeDataString(slug)}/install-manifest");
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"VOID Registry install manifest selhal: {body}");
        }

        return JsonSerializer.Deserialize<VoidRegistryInstallManifest>(body, JsonOptions);
    }

    public async Task<VoidRegistryUpdateCheckResponse?> GetUpdateCheckAsync(string slug, string currentVersion)
    {
        using var response = await _httpClient.GetAsync($"{BaseApiUrl}/api/registry/projects/{Uri.EscapeDataString(slug)}/update-check?current_version={Uri.EscapeDataString(currentVersion)}");
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"VOID Registry update check selhal: {body}");
        }

        return JsonSerializer.Deserialize<VoidRegistryUpdateCheckResponse>(body, JsonOptions);
    }

    public async Task<IReadOnlyList<VoidRegistryProjectSummary>> GetProjectsForActorAsync(string accessToken, int page = 1, int limit = 50)
    {
        using var response = await SendAuthorizedAsync(
            HttpMethod.Get,
            $"/api/registry/me/projects?page={Math.Max(1, page)}&limit={Math.Clamp(limit, 1, 100)}",
            accessToken);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"VOID Registry moje projekty selhaly: {body}");
        }

        var root = JsonNode.Parse(body);
        var items = root?[
            "data"]?.AsArray()
            ?? root?["projects"]?.AsArray()
            ?? new JsonArray();

        return items
            .Select(ParseProjectSummary)
            .Where(project => project != null)
            .Cast<VoidRegistryProjectSummary>()
            .ToList();
    }

    public async Task<VoidRegistryCollaboratorBundle> GetCollaboratorsAsync(string accessToken, string slug)
    {
        using var response = await SendAuthorizedAsync(HttpMethod.Get, $"/api/registry/projects/{Uri.EscapeDataString(slug)}/collaborators", accessToken);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"VOID Registry collaborators selhal: {body}");
        }

        return ParseCollaboratorBundle(body);
    }

    public async Task<IReadOnlyList<VoidRegistryAccountSearchEntry>> SearchAccountsAsync(string accessToken, string query, int limit = 8)
    {
        var normalizedQuery = (query ?? string.Empty).Trim();
        if (normalizedQuery.Length < 2)
        {
            return Array.Empty<VoidRegistryAccountSearchEntry>();
        }

        using var response = await SendAuthorizedAsync(
            HttpMethod.Get,
            $"/api/registry/accounts/search?q={Uri.EscapeDataString(normalizedQuery)}&limit={Math.Clamp(limit, 1, 20)}",
            accessToken);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"VOID Registry account search selhal: {body}");
        }

        return ParseAccountSearchResults(body);
    }

    public async Task<VoidRegistryCollaboratorBundle> AddCollaboratorAsync(string accessToken, string slug, int accountId, string role)
    {
        using var response = await SendAuthorizedJsonAsync(
            HttpMethod.Post,
            $"/api/registry/projects/{Uri.EscapeDataString(slug)}/collaborators",
            accessToken,
            new
            {
                accountId,
                role
            });
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"VOID Registry add collaborator selhal: {body}");
        }

        return ParseCollaboratorBundle(body);
    }

    public async Task<VoidRegistryCollaboratorBundle> UpdateCollaboratorAsync(string accessToken, string slug, int accountId, string role)
    {
        using var response = await SendAuthorizedJsonAsync(
            HttpMethod.Put,
            $"/api/registry/projects/{Uri.EscapeDataString(slug)}/collaborators/{accountId}",
            accessToken,
            new
            {
                role
            });
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"VOID Registry update collaborator selhal: {body}");
        }

        return ParseCollaboratorBundle(body);
    }

    public async Task RemoveCollaboratorAsync(string accessToken, string slug, int accountId)
    {
        using var response = await SendAuthorizedAsync(
            HttpMethod.Delete,
            $"/api/registry/projects/{Uri.EscapeDataString(slug)}/collaborators/{accountId}",
            accessToken);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"VOID Registry remove collaborator selhal: {body}");
        }
    }

    public async Task<VoidRegistryPublishResult> RegisterGitHubReleaseAsync(
        string accessToken,
        CreatorManifest manifest,
        GitHubRepositoryReference repository,
        GitHubReleaseAssetInfo assetInfo,
        string changelog)
    {
        var brandingUrls = new VoidRegistryBrandingUrls
        {
            Logo = BuildTaggedRawUrl(repository, assetInfo.TagName, manifest.Branding?.LogoPath),
            Cover = BuildTaggedRawUrl(repository, assetInfo.TagName, manifest.Branding?.CoverPath),
            Icon = BuildTaggedRawUrl(repository, assetInfo.TagName, manifest.Branding?.SquareIconPath)
        };

        var projectResult = await UpsertProjectAsync(accessToken, new VoidRegistryProjectUpsertRequest
        {
            ProjectId = manifest.RegistryProjectId,
            Slug = manifest.Slug,
            Name = manifest.PackName,
            Summary = manifest.Summary,
            MinecraftVersion = manifest.MinecraftVersion,
            ModLoader = manifest.ModLoader,
            ModLoaderVersion = manifest.ModLoaderVersion,
            RecommendedRamMb = manifest.RecommendedRamMb,
            PrimaryServer = manifest.PrimaryServer,
            LogoUrl = brandingUrls.Logo,
            CoverUrl = brandingUrls.Cover,
            SquareIconUrl = brandingUrls.Icon,
            RepositoryUrl = repository.WebUrl,
            Visibility = "public"
        });

        var versionResult = await PublishVersionAsync(accessToken, manifest.Slug, new VoidRegistryVersionPublishRequest
        {
            VersionNumber = manifest.Version,
            ReleaseChannel = string.IsNullOrWhiteSpace(manifest.ReleaseChannel) ? "stable" : manifest.ReleaseChannel,
            Changelog = changelog,
            MinecraftVersion = manifest.MinecraftVersion,
            ModLoader = manifest.ModLoader,
            ModCount = assetInfo.ModCount,
            FileName = assetInfo.AssetName,
            FileSizeBytes = assetInfo.FileSizeBytes,
            FileHashSha256 = assetInfo.FileHashSha256,
            DownloadUrl = assetInfo.DownloadUrl,
            ReleasePageUrl = assetInfo.ReleasePageUrl
        });

        return new VoidRegistryPublishResult
        {
            ProjectId = string.IsNullOrWhiteSpace(versionResult.ProjectId) ? projectResult.ProjectId : versionResult.ProjectId,
            VersionId = versionResult.VersionId,
            Slug = string.IsNullOrWhiteSpace(versionResult.Slug) ? manifest.Slug : versionResult.Slug,
            VersionNumber = string.IsNullOrWhiteSpace(versionResult.VersionNumber) ? manifest.Version : versionResult.VersionNumber
        };
    }

    public static string BuildTaggedRawUrl(GitHubRepositoryReference repository, string tagName, string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return string.Empty;
        }

        var normalizedPath = relativePath.Replace('\\', '/').TrimStart('/');
        return $"https://raw.githubusercontent.com/{repository.Owner}/{repository.Repository}/{Uri.EscapeDataString(tagName)}/{normalizedPath}";
    }

    private async Task<VoidRegistryPublishResult> UpsertProjectAsync(string accessToken, VoidRegistryProjectUpsertRequest request)
    {
        var createResponse = await SendAuthorizedJsonAsync(HttpMethod.Post, "/api/registry/projects", accessToken, request);
        if (createResponse.IsSuccessStatusCode)
        {
            return await ParsePublishResultAsync(createResponse, request.Slug);
        }

        if (createResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            throw new InvalidOperationException("VOID Registry odmítl autorizaci. VOID ID access token už není platný.");
        }

        var createError = await createResponse.Content.ReadAsStringAsync();
        if (createResponse.StatusCode != System.Net.HttpStatusCode.Conflict &&
            createResponse.StatusCode != System.Net.HttpStatusCode.BadRequest)
        {
            throw new InvalidOperationException($"VOID Registry create project selhal: {createError}");
        }

        var updateResponse = await SendAuthorizedJsonAsync(HttpMethod.Put, $"/api/registry/projects/{Uri.EscapeDataString(request.Slug)}", accessToken, request);
        if (!updateResponse.IsSuccessStatusCode)
        {
            var updateError = await updateResponse.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"VOID Registry update project selhal: {updateError}");
        }

        return await ParsePublishResultAsync(updateResponse, request.Slug);
    }

    private async Task<VoidRegistryPublishResult> PublishVersionAsync(string accessToken, string slug, VoidRegistryVersionPublishRequest request)
    {
        var response = await SendAuthorizedJsonAsync(HttpMethod.Post, $"/api/registry/projects/{Uri.EscapeDataString(slug)}/versions", accessToken, request);
        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                throw new InvalidOperationException("VOID Registry odmítl autorizaci při publish verze. VOID ID access token už není platný.");
            }

            var error = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"VOID Registry publish version selhal: {error}");
        }

        return await ParsePublishResultAsync(response, slug);
    }

    private async Task<HttpResponseMessage> SendAuthorizedJsonAsync(HttpMethod method, string path, string accessToken, object payload)
    {
        return await SendAuthorizedAsync(
            method,
            path,
            accessToken,
            new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json"));
    }

    private async Task<HttpResponseMessage> SendAuthorizedAsync(HttpMethod method, string path, string accessToken, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, $"{BaseApiUrl}{path}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = content;
        return await _httpClient.SendAsync(request);
    }

    private static async Task<VoidRegistryPublishResult> ParsePublishResultAsync(HttpResponseMessage response, string fallbackSlug)
    {
        var body = await response.Content.ReadAsStringAsync();
        var node = string.IsNullOrWhiteSpace(body) ? null : JsonNode.Parse(body);
        var root = node?["data"] ?? node?["project"] ?? node?["version"] ?? node;

        return new VoidRegistryPublishResult
        {
            ProjectId = root?["project_id"]?.ToString() ?? root?["projectId"]?.ToString() ?? string.Empty,
            VersionId = root?["version_id"]?.ToString() ?? root?["versionId"]?.ToString() ?? string.Empty,
            Slug = root?["slug"]?.ToString() ?? fallbackSlug,
            VersionNumber = root?["version_number"]?.ToString() ?? root?["version"]?.ToString() ?? string.Empty
        };
    }

    private static VoidRegistryProjectSummary? ParseProjectSummary(JsonNode? node)
    {
        if (node == null)
        {
            return null;
        }

        var branding = node["branding"];
        var latest = node["latest"] ?? node["latest_version"];
        var permissions = node["permissions"];

        return new VoidRegistryProjectSummary
        {
            ProjectId = node["project_id"]?.ToString() ?? node["projectId"]?.ToString() ?? string.Empty,
            Slug = node["slug"]?.ToString() ?? string.Empty,
            Name = node["name"]?.ToString() ?? string.Empty,
            Summary = node["summary"]?.ToString() ?? string.Empty,
            Author = node["author"]?.ToString() ?? node["owner_name"]?.ToString() ?? string.Empty,
            OwnerAvatarUrl = node["owner"]?["avatar_url"]?.ToString() ?? node["owner_avatar_url"]?.ToString() ?? string.Empty,
            LogoUrl = node["logo_url"]?.ToString() ?? branding?["logo"]?.ToString() ?? string.Empty,
            CoverUrl = node["cover_url"]?.ToString() ?? branding?["cover"]?.ToString() ?? string.Empty,
            IconUrl = node["square_icon_url"]?.ToString() ?? branding?["icon"]?.ToString() ?? string.Empty,
            MinecraftVersion = node["minecraft_version"]?.ToString() ?? string.Empty,
            ModLoader = node["mod_loader"]?.ToString() ?? string.Empty,
            ModLoaderVersion = node["mod_loader_version"]?.ToString() ?? string.Empty,
            RecommendedRamMb = node["recommended_ram_mb"]?.GetValue<int?>() ?? node["recommended_ram"]?.GetValue<int?>() ?? 0,
            PrimaryServer = node["primary_server"]?.ToString() ?? string.Empty,
            RepositoryUrl = node["repository_url"]?.ToString() ?? string.Empty,
            DownloadCount = node["download_count"]?.GetValue<long?>() ?? 0,
            LatestVersion = latest?["version"]?.ToString() ?? latest?["version_number"]?.ToString() ?? string.Empty,
            ReleaseChannel = latest?["channel"]?.ToString() ?? latest?["release_channel"]?.ToString() ?? node["release_channel"]?.ToString() ?? string.Empty,
            LatestPublishedAtUtc = latest?["published_at"]?.GetValue<DateTimeOffset?>(),
            Status = node["status"]?.ToString() ?? string.Empty,
            Visibility = node["visibility"]?.ToString() ?? string.Empty,
            ViewerRole = permissions?["role"]?.ToString() ?? node["viewer_role"]?.ToString() ?? string.Empty,
            CanView = permissions?["canView"]?.GetValue<bool?>() ?? permissions?["can_view"]?.GetValue<bool?>() ?? false,
            CanEditMetadata = permissions?["canEditMetadata"]?.GetValue<bool?>() ?? permissions?["can_edit_metadata"]?.GetValue<bool?>() ?? false,
            CanPublish = permissions?["canPublish"]?.GetValue<bool?>() ?? permissions?["can_publish"]?.GetValue<bool?>() ?? false,
            CanManageCollaborators = permissions?["canManageCollaborators"]?.GetValue<bool?>() ?? permissions?["can_manage_collaborators"]?.GetValue<bool?>() ?? false,
            CanArchive = permissions?["canArchive"]?.GetValue<bool?>() ?? permissions?["can_archive"]?.GetValue<bool?>() ?? false
        };
    }

    private static VoidRegistryCollaboratorBundle ParseCollaboratorBundle(string body)
    {
        var root = string.IsNullOrWhiteSpace(body) ? null : JsonNode.Parse(body);
        var rows = root?["data"]?.AsArray() ?? new JsonArray();

        return new VoidRegistryCollaboratorBundle
        {
            Owner = ParseCollaboratorEntry(root?["owner"], true),
            Data = rows
                .Select(node => ParseCollaboratorEntry(node, false))
                .Where(entry => entry != null)
                .Cast<VoidRegistryCollaboratorEntry>()
                .ToList(),
            Permissions = ParseCollaboratorPermissions(root?["permissions"])
        };
    }

    private static VoidRegistryCollaboratorEntry? ParseCollaboratorEntry(JsonNode? node, bool isOwner)
    {
        if (node == null)
        {
            return null;
        }

        return new VoidRegistryCollaboratorEntry
        {
            AccountId = node["account_id"]?.GetValue<int?>() ?? 0,
            Role = node["role"]?.ToString() ?? (isOwner ? "owner" : string.Empty),
            AddedAt = node["added_at"]?.ToString() ?? string.Empty,
            DisplayName = node["display_name"]?.ToString() ?? string.Empty,
            AvatarUrl = node["avatar_url"]?.ToString() ?? string.Empty,
            IsOwner = isOwner || string.Equals(node["role"]?.ToString(), "owner", StringComparison.OrdinalIgnoreCase)
        };
    }

    private static VoidRegistryCollaboratorPermissions ParseCollaboratorPermissions(JsonNode? node)
    {
        return new VoidRegistryCollaboratorPermissions
        {
            Role = node?["role"]?.ToString() ?? string.Empty,
            CanView = node?["canView"]?.GetValue<bool?>() ?? false,
            CanEditMetadata = node?["canEditMetadata"]?.GetValue<bool?>() ?? false,
            CanPublish = node?["canPublish"]?.GetValue<bool?>() ?? false,
            CanManageCollaborators = node?["canManageCollaborators"]?.GetValue<bool?>() ?? false,
            CanArchive = node?["canArchive"]?.GetValue<bool?>() ?? false
        };
    }

    private static IReadOnlyList<VoidRegistryAccountSearchEntry> ParseAccountSearchResults(string body)
    {
        var root = string.IsNullOrWhiteSpace(body) ? null : JsonNode.Parse(body);
        var rows = root as JsonArray ?? root?["data"]?.AsArray() ?? new JsonArray();

        return rows
            .Select(node => ParseAccountSearchEntry(node))
            .Where(entry => entry != null)
            .Cast<VoidRegistryAccountSearchEntry>()
            .ToList();
    }

    private static VoidRegistryAccountSearchEntry? ParseAccountSearchEntry(JsonNode? node)
    {
        if (node == null)
        {
            return null;
        }

        return new VoidRegistryAccountSearchEntry
        {
            AccountId = node["account_id"]?.GetValue<int?>() ?? 0,
            DisplayName = node["display_name"]?.ToString() ?? string.Empty,
            AvatarUrl = node["avatar_url"]?.ToString() ?? string.Empty,
            DiscordUsername = node["discord_username"]?.ToString() ?? string.Empty
        };
    }
}