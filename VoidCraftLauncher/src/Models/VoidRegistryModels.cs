using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VoidCraftLauncher.Models;

public sealed class VoidRegistryBrandingUrls
{
    [JsonPropertyName("logo")]
    public string Logo { get; set; } = string.Empty;

    [JsonPropertyName("cover")]
    public string Cover { get; set; } = string.Empty;

    [JsonPropertyName("icon")]
    public string Icon { get; set; } = string.Empty;
}

public sealed class VoidRegistryProjectSummary
{
    public string ProjectId { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string Author { get; set; } = string.Empty;

    public string LogoUrl { get; set; } = string.Empty;

    public string CoverUrl { get; set; } = string.Empty;

    public string IconUrl { get; set; } = string.Empty;

    public string MinecraftVersion { get; set; } = string.Empty;

    public string ModLoader { get; set; } = string.Empty;

    public string ModLoaderVersion { get; set; } = string.Empty;

    public int RecommendedRamMb { get; set; }

    public string PrimaryServer { get; set; } = string.Empty;

    public string RepositoryUrl { get; set; } = string.Empty;

    public long DownloadCount { get; set; }

    public string LatestVersion { get; set; } = string.Empty;

    public string ReleaseChannel { get; set; } = string.Empty;
}

public sealed class VoidRegistryInstallManifest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("slug")]
    public string Slug { get; set; } = string.Empty;

    [JsonPropertyName("project_id")]
    public string ProjectId { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("version_id")]
    public string VersionId { get; set; } = string.Empty;

    [JsonPropertyName("minecraft_version")]
    public string MinecraftVersion { get; set; } = string.Empty;

    [JsonPropertyName("mod_loader")]
    public string ModLoader { get; set; } = string.Empty;

    [JsonPropertyName("mod_loader_version")]
    public string ModLoaderVersion { get; set; } = string.Empty;

    [JsonPropertyName("file_url")]
    public string FileUrl { get; set; } = string.Empty;

    [JsonPropertyName("file_hash")]
    public string FileHash { get; set; } = string.Empty;

    [JsonPropertyName("file_name")]
    public string FileName { get; set; } = string.Empty;

    [JsonPropertyName("recommended_ram")]
    public int RecommendedRam { get; set; }

    [JsonPropertyName("branding")]
    public VoidRegistryBrandingUrls Branding { get; set; } = new();
}

public sealed class VoidRegistryVersionInfo
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("channel")]
    public string Channel { get; set; } = string.Empty;

    [JsonPropertyName("changelog")]
    public string Changelog { get; set; } = string.Empty;

    [JsonPropertyName("file_url")]
    public string FileUrl { get; set; } = string.Empty;

    [JsonPropertyName("file_hash")]
    public string FileHash { get; set; } = string.Empty;

    [JsonPropertyName("release_page_url")]
    public string ReleasePageUrl { get; set; } = string.Empty;

    [JsonPropertyName("published_at")]
    public DateTimeOffset? PublishedAtUtc { get; set; }
}

public sealed class VoidRegistryUpdateCheckResponse
{
    [JsonPropertyName("update_available")]
    public bool UpdateAvailable { get; set; }

    [JsonPropertyName("latest")]
    public VoidRegistryVersionInfo? Latest { get; set; }
}

public sealed class VoidRegistryProjectUpsertRequest
{
    [JsonPropertyName("project_id")]
    public string ProjectId { get; set; } = string.Empty;

    [JsonPropertyName("slug")]
    public string Slug { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("minecraft_version")]
    public string MinecraftVersion { get; set; } = string.Empty;

    [JsonPropertyName("mod_loader")]
    public string ModLoader { get; set; } = string.Empty;

    [JsonPropertyName("mod_loader_version")]
    public string ModLoaderVersion { get; set; } = string.Empty;

    [JsonPropertyName("recommended_ram_mb")]
    public int RecommendedRamMb { get; set; }

    [JsonPropertyName("primary_server")]
    public string PrimaryServer { get; set; } = string.Empty;

    [JsonPropertyName("logo_url")]
    public string LogoUrl { get; set; } = string.Empty;

    [JsonPropertyName("cover_url")]
    public string CoverUrl { get; set; } = string.Empty;

    [JsonPropertyName("square_icon_url")]
    public string SquareIconUrl { get; set; } = string.Empty;

    [JsonPropertyName("repository_url")]
    public string RepositoryUrl { get; set; } = string.Empty;

    [JsonPropertyName("visibility")]
    public string Visibility { get; set; } = "public";
}

public sealed class VoidRegistryVersionPublishRequest
{
    [JsonPropertyName("version_number")]
    public string VersionNumber { get; set; } = string.Empty;

    [JsonPropertyName("release_channel")]
    public string ReleaseChannel { get; set; } = string.Empty;

    [JsonPropertyName("changelog")]
    public string Changelog { get; set; } = string.Empty;

    [JsonPropertyName("minecraft_version")]
    public string MinecraftVersion { get; set; } = string.Empty;

    [JsonPropertyName("mod_loader")]
    public string ModLoader { get; set; } = string.Empty;

    [JsonPropertyName("mod_count")]
    public int ModCount { get; set; }

    [JsonPropertyName("file_name")]
    public string FileName { get; set; } = string.Empty;

    [JsonPropertyName("file_size_bytes")]
    public long FileSizeBytes { get; set; }

    [JsonPropertyName("file_hash_sha256")]
    public string FileHashSha256 { get; set; } = string.Empty;

    [JsonPropertyName("download_url")]
    public string DownloadUrl { get; set; } = string.Empty;

    [JsonPropertyName("release_page_url")]
    public string ReleasePageUrl { get; set; } = string.Empty;
}

public sealed class VoidRegistryPublishResult
{
    public string ProjectId { get; set; } = string.Empty;

    public string VersionId { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string VersionNumber { get; set; } = string.Empty;
}