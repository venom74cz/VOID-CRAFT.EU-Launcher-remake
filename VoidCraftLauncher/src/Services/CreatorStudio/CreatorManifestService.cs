using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using VoidCraftLauncher.Models;
using VoidCraftLauncher.Models.CreatorStudio;

namespace VoidCraftLauncher.Services.CreatorStudio;

public sealed class CreatorManifestService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly CreatorWorkspaceService _creatorWorkspaceService;
    private readonly CreatorAssetsService _creatorAssetsService;

    public CreatorManifestService(CreatorWorkspaceService creatorWorkspaceService, CreatorAssetsService creatorAssetsService)
    {
        _creatorWorkspaceService = creatorWorkspaceService;
        _creatorAssetsService = creatorAssetsService;
    }

    public string GetManifestPath(string workspacePath)
    {
        return Path.Combine(workspacePath, CreatorWorkspaceContext.CreatorManifestFileName);
    }

    public CreatorManifest CreateManifest(
        string packName,
        string slug,
        string summary,
        IEnumerable<string> authors,
        string version,
        string minecraftVersion,
        string modLoader,
        string modLoaderVersion,
        int recommendedRamMb,
        string primaryServer,
        string releaseChannel,
        DateTimeOffset? createdAtUtc = null)
    {
        var createdAt = createdAtUtc ?? DateTimeOffset.UtcNow;
        return new CreatorManifest
        {
            PackName = packName.Trim(),
            Slug = slug.Trim(),
            Summary = summary.Trim(),
            Authors = NormalizeAuthors(authors),
            Version = string.IsNullOrWhiteSpace(version) ? "0.1.0" : version.Trim(),
            MinecraftVersion = minecraftVersion.Trim(),
            ModLoader = modLoader.Trim(),
            ModLoaderVersion = modLoaderVersion.Trim(),
            RecommendedRamMb = Math.Max(2048, recommendedRamMb),
            PrimaryServer = primaryServer.Trim(),
            ReleaseChannel = string.IsNullOrWhiteSpace(releaseChannel) ? "alpha" : releaseChannel.Trim(),
            CreatedAtUtc = createdAt,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    public CreatorManifest CreateDefaultManifest(string workspaceLabel, string minecraftVersion, string modLoader, string modLoaderVersion)
    {
        var slug = BuildSlug(workspaceLabel);
        return CreateManifest(
            workspaceLabel,
            slug,
            string.Empty,
            Array.Empty<string>(),
            "0.1.0",
            minecraftVersion,
            modLoader,
            modLoaderVersion,
            12288,
            string.Empty,
            "alpha");
    }

    public CreatorManifest CreateFallbackManifest(ModpackInfo? modpack, string minecraftVersion, string modLoader, string modLoaderVersion)
    {
        if (modpack == null)
        {
            return CreateDefaultManifest("Unnamed Pack", minecraftVersion, modLoader, modLoaderVersion);
        }

        var packName = !string.IsNullOrWhiteSpace(modpack.Name) ? modpack.Name : "Unnamed Pack";
        var slug = BuildSlug(packName);
        var authors = !string.IsNullOrWhiteSpace(modpack.Author) ? new[] { modpack.Author } : Array.Empty<string>();
        var summary = !string.IsNullOrWhiteSpace(modpack.Description) ? modpack.Description : string.Empty;
        var version = modpack.CurrentVersion?.Name ?? "0.1.0";

        return CreateManifest(
            packName,
            slug,
            summary,
            authors,
            version,
            minecraftVersion,
            modLoader,
            modLoaderVersion,
            12288,
            string.Empty,
            "alpha");
    }

    public CreatorManifest? LoadManifest(string workspacePath)
    {
        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            return null;
        }

        var manifestPath = GetManifestPath(workspacePath);
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(manifestPath);
            return JsonSerializer.Deserialize<CreatorManifest>(json, SerializerOptions);
        }
        catch
        {
            return null;
        }
    }

    public async Task<CreatorManifest> SaveManifestAsync(string workspacePath, CreatorManifest manifest)
    {
        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            throw new InvalidOperationException("Creator workspace path is missing.");
        }

        Directory.CreateDirectory(workspacePath);
        EnsureWorkspaceStructure(workspacePath);

        var existingManifest = LoadManifest(workspacePath);
        manifest.CreatedAtUtc = existingManifest?.CreatedAtUtc ?? manifest.CreatedAtUtc;
        manifest.Branding = existingManifest?.Branding ?? manifest.Branding;
        manifest.Assets = existingManifest?.Assets ?? manifest.Assets;
        manifest.UpdatedAtUtc = DateTimeOffset.UtcNow;

        var manifestPath = GetManifestPath(workspacePath);
        var json = JsonSerializer.Serialize(manifest, SerializerOptions);
        await File.WriteAllTextAsync(manifestPath, json);
        return manifest;
    }

    public IReadOnlyList<string> EnsureWorkspaceStructure(string workspacePath)
    {
        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            return Array.Empty<string>();
        }

        Directory.CreateDirectory(workspacePath);
        var createdFolders = new List<string>();

        foreach (var folder in _creatorWorkspaceService.GetStandardWorkspaceFolders())
        {
            var fullPath = Path.Combine(workspacePath, folder);
            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
                createdFolders.Add(folder);
            }
        }

        return createdFolders;
    }

    public static string BuildSlug(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return "voidcraft-pack";
        }

        var sanitized = new string(source
            .Trim()
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray());

        while (sanitized.Contains("--", StringComparison.Ordinal))
        {
            sanitized = sanitized.Replace("--", "-", StringComparison.Ordinal);
        }

        sanitized = sanitized.Trim('-');
        return string.IsNullOrWhiteSpace(sanitized) ? "voidcraft-pack" : sanitized;
    }

    public async Task<bool> TryImportPublicBrandingAsync(string workspacePath, ModpackInfo modpack)
    {
        if (string.IsNullOrWhiteSpace(modpack.LogoUrl))
        {
            return false;
        }

        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);

            var logoBytes = await httpClient.GetByteArrayAsync(modpack.LogoUrl);
            var tempLogoPath = Path.Combine(Path.GetTempPath(), $"logo_{Guid.NewGuid()}.png");
            await File.WriteAllBytesAsync(tempLogoPath, logoBytes);

            var logoResult = await _creatorAssetsService.UploadAssetAsync(workspacePath, BrandingAssetSlot.Logo, tempLogoPath);
            if (logoResult.Success && logoResult.Metadata != null)
            {
                await _creatorAssetsService.UploadAssetAsync(workspacePath, BrandingAssetSlot.SquareIcon, tempLogoPath);
            }

            File.Delete(tempLogoPath);
            return logoResult.Success;
        }
        catch
        {
            return false;
        }
    }

    public static List<string> NormalizeAuthors(IEnumerable<string> authors)
    {
        return authors
            .Select(author => author.Trim())
            .Where(author => !string.IsNullOrWhiteSpace(author))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}