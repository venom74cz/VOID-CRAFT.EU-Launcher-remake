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
        var request = new HttpRequestMessage(method, $"{BaseApiUrl}{path}")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
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

        return new VoidRegistryProjectSummary
        {
            ProjectId = node["project_id"]?.ToString() ?? node["projectId"]?.ToString() ?? string.Empty,
            Slug = node["slug"]?.ToString() ?? string.Empty,
            Name = node["name"]?.ToString() ?? string.Empty,
            Summary = node["summary"]?.ToString() ?? string.Empty,
            Author = node["author"]?.ToString() ?? node["owner_name"]?.ToString() ?? string.Empty,
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
            ReleaseChannel = latest?["channel"]?.ToString() ?? node["release_channel"]?.ToString() ?? string.Empty
        };
    }
}