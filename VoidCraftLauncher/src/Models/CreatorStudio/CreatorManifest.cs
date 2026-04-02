using System;
using System.Collections.Generic;

namespace VoidCraftLauncher.Models.CreatorStudio;

public sealed class CreatorManifest
{
    public string PackName { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public List<string> Authors { get; set; } = new();

    public string Version { get; set; } = "0.1.0";

    public string MinecraftVersion { get; set; } = string.Empty;

    public string ModLoader { get; set; } = string.Empty;

    public string ModLoaderVersion { get; set; } = string.Empty;

    public int RecommendedRamMb { get; set; } = 12288;

    public string PrimaryServer { get; set; } = string.Empty;

    public string ReleaseChannel { get; set; } = "alpha";

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public CreatorBrandingProfile? Branding { get; set; }

    public CreatorBrandProfile? BrandProfile { get; set; }

    public List<CreatorAssetMetadata> Assets { get; set; } = new();

    public List<CreatorScreenshotMetadata> Screenshots { get; set; } = new();
}