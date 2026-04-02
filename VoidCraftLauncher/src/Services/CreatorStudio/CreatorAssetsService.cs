using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using SkiaSharp;
using VoidCraftLauncher.Models.CreatorStudio;

namespace VoidCraftLauncher.Services.CreatorStudio;

public sealed class CreatorAssetsService
{
    private const string BrandingFolderName = "assets/branding";
    private const string PreferredScreenshotsFolderName = "screenshots";
    private static readonly string[] ScreenshotFolderCandidates = { PreferredScreenshotsFolderName, "screenshoty" };
    private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp"
    };

    public string GetBrandingPath(string workspacePath)
    {
        return Path.Combine(workspacePath, BrandingFolderName);
    }

    public string GetScreenshotGalleryPath(string workspacePath, bool ensureExists = false)
    {
        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            return string.Empty;
        }

        foreach (var folderName in ScreenshotFolderCandidates)
        {
            var candidatePath = Path.Combine(workspacePath, folderName);
            if (Directory.Exists(candidatePath))
            {
                return candidatePath;
            }
        }

        var defaultPath = Path.Combine(workspacePath, PreferredScreenshotsFolderName);
        if (ensureExists)
        {
            Directory.CreateDirectory(defaultPath);
        }

        return defaultPath;
    }

    public bool IsSupportedImagePath(string filePath)
    {
        return AllowedImageExtensions.Contains(Path.GetExtension(filePath));
    }

    public async Task<(bool Success, string? Error, CreatorAssetMetadata? Metadata)> UploadAssetAsync(
        string workspacePath,
        BrandingAssetSlot slot,
        string sourceFilePath)
    {
        if (!File.Exists(sourceFilePath))
        {
            return (false, "Zdrojový soubor neexistuje.", null);
        }

        var requirement = BrandingAssetRequirement.GetStandardRequirements()
            .FirstOrDefault(r => r.Slot == slot);
        if (requirement == null)
        {
            return (false, "Neznámý asset slot.", null);
        }

        var validation = ValidateImage(sourceFilePath, requirement);
        if (!validation.IsValid)
        {
            return (false, validation.Error, null);
        }

        var brandingPath = GetBrandingPath(workspacePath);
        Directory.CreateDirectory(brandingPath);

        var targetFileName = $"{slot.ToString().ToLowerInvariant()}.png";
        var targetPath = Path.Combine(brandingPath, targetFileName);

        foreach (var existingFile in Directory.GetFiles(brandingPath, $"{slot.ToString().ToLowerInvariant()}.*"))
        {
            if (string.Equals(existingFile, targetPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            File.Delete(existingFile);
        }

        await Task.Run(() => TransformAndSaveAsset(sourceFilePath, targetPath, requirement));

        var savedInfo = ReadImageInfo(targetPath);
        if (!savedInfo.IsValid)
        {
            return (false, "Transformovaný asset se nepodařilo načíst.", null);
        }

        var metadata = BuildAssetMetadata(slot, targetPath, savedInfo.Width, savedInfo.Height, savedInfo.HasTransparency);

        return (true, null, metadata);
    }

    public string? GetAssetRelativePath(string workspacePath, BrandingAssetSlot slot)
    {
        var assetPath = GetAssetPath(workspacePath, slot);
        if (string.IsNullOrWhiteSpace(assetPath) || string.IsNullOrWhiteSpace(workspacePath))
        {
            return null;
        }

        return Path.GetRelativePath(workspacePath, assetPath).Replace('\\', '/');
    }

    public string? ResolveWorkspaceRelativePath(string workspacePath, string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(workspacePath) || string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        var absolutePath = Path.Combine(workspacePath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(absolutePath) ? absolutePath : null;
    }

    public CreatorBrandingProfile BuildBrandingProfile(string workspacePath, IReadOnlyList<CreatorScreenshotMetadata>? screenshotMetadata = null)
    {
        return new CreatorBrandingProfile
        {
            LogoPath = GetAssetRelativePath(workspacePath, BrandingAssetSlot.Logo),
            CoverPath = GetAssetRelativePath(workspacePath, BrandingAssetSlot.Cover),
            SquareIconPath = GetAssetRelativePath(workspacePath, BrandingAssetSlot.SquareIcon),
            WideHeroPath = GetAssetRelativePath(workspacePath, BrandingAssetSlot.WideHero),
            SocialPreviewPath = GetAssetRelativePath(workspacePath, BrandingAssetSlot.SocialPreview),
            FeaturedScreenshotPath = GetFeaturedScreenshotRelativePath(workspacePath, screenshotMetadata),
            LastUpdatedUtc = DateTimeOffset.UtcNow
        };
    }

    public IReadOnlyList<CreatorAssetMetadata> GetAssetMetadata(string workspacePath)
    {
        var metadata = new List<CreatorAssetMetadata>();
        foreach (var requirement in BrandingAssetRequirement.GetStandardRequirements())
        {
            var assetPath = GetAssetPath(workspacePath, requirement.Slot);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                continue;
            }

            var imageInfo = ReadImageInfo(assetPath);
            if (!imageInfo.IsValid)
            {
                continue;
            }

            metadata.Add(BuildAssetMetadata(requirement.Slot, assetPath, imageInfo.Width, imageInfo.Height, imageInfo.HasTransparency));
        }

        return metadata;
    }

    public bool RemoveAsset(string workspacePath, BrandingAssetSlot slot)
    {
        var brandingPath = GetBrandingPath(workspacePath);
        if (!Directory.Exists(brandingPath))
        {
            return false;
        }

        var pattern = $"{slot.ToString().ToLowerInvariant()}.*";
        var files = Directory.GetFiles(brandingPath, pattern);
        foreach (var file in files)
        {
            try
            {
                File.Delete(file);
            }
            catch
            {
                return false;
            }
        }

        return true;
    }

    public string? GetAssetPath(string workspacePath, BrandingAssetSlot slot)
    {
        var brandingPath = GetBrandingPath(workspacePath);
        if (!Directory.Exists(brandingPath))
        {
            return null;
        }

        var pattern = $"{slot.ToString().ToLowerInvariant()}.*";
        var files = Directory.GetFiles(brandingPath, pattern);
        return files
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    public IReadOnlyList<CreatorScreenshotMetadata> NormalizeScreenshotMetadata(
        string workspacePath,
        IReadOnlyList<CreatorScreenshotMetadata>? persistedMetadata)
    {
        var screenshotsPath = GetScreenshotGalleryPath(workspacePath);
        if (!Directory.Exists(screenshotsPath))
        {
            return Array.Empty<CreatorScreenshotMetadata>();
        }

        var metadataByPath = (persistedMetadata ?? Array.Empty<CreatorScreenshotMetadata>())
            .Where(item => !string.IsNullOrWhiteSpace(item.RelativePath))
            .GroupBy(item => item.RelativePath.Replace('\\', '/'), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => CloneScreenshotMetadata(group.First()), StringComparer.OrdinalIgnoreCase);

        var screenshotFiles = Directory
            .GetFiles(screenshotsPath)
            .Where(IsSupportedImagePath)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToList();

        var normalized = new List<CreatorScreenshotMetadata>(screenshotFiles.Count);
        var favoriteAssigned = false;

        foreach (var screenshotFile in screenshotFiles)
        {
            var relativePath = Path.GetRelativePath(workspacePath, screenshotFile).Replace('\\', '/');
            metadataByPath.TryGetValue(relativePath, out var savedMetadata);

            var isFavorite = savedMetadata?.IsFavorite == true && !favoriteAssigned;
            if (isFavorite)
            {
                favoriteAssigned = true;
            }

            normalized.Add(new CreatorScreenshotMetadata
            {
                RelativePath = relativePath,
                Stage = NormalizeScreenshotStage(savedMetadata?.Stage),
                IsFavorite = isFavorite,
                UpdatedAtUtc = savedMetadata?.UpdatedAtUtc ?? new DateTimeOffset(File.GetLastWriteTimeUtc(screenshotFile), TimeSpan.Zero)
            });
        }

        return normalized;
    }

    public IReadOnlyList<CreatorScreenshotMetadata> CompactScreenshotMetadata(IReadOnlyList<CreatorScreenshotMetadata>? metadata)
    {
        return (metadata ?? Array.Empty<CreatorScreenshotMetadata>())
            .Where(item => item.IsFavorite || ParseScreenshotStage(item.Stage) != CreatorScreenshotStage.Unsorted)
            .Select(CloneScreenshotMetadata)
            .ToList();
    }

    public IReadOnlyList<CreatorScreenshotGalleryItem> GetScreenshotGallery(
        string workspacePath,
        IReadOnlyList<CreatorScreenshotMetadata>? persistedMetadata)
    {
        return NormalizeScreenshotMetadata(workspacePath, persistedMetadata)
            .Select(metadata => BuildScreenshotGalleryItem(workspacePath, metadata))
            .Where(item => item != null)
            .Cast<CreatorScreenshotGalleryItem>()
            .ToList();
    }

    public string? GetFeaturedScreenshotPath(string workspacePath, IReadOnlyList<CreatorScreenshotMetadata>? persistedMetadata)
    {
        return SelectFeaturedScreenshot(GetScreenshotGallery(workspacePath, persistedMetadata))?.AbsolutePath;
    }

    public string? GetFeaturedScreenshotRelativePath(string workspacePath, IReadOnlyList<CreatorScreenshotMetadata>? persistedMetadata)
    {
        var absolutePath = GetFeaturedScreenshotPath(workspacePath, persistedMetadata);
        if (string.IsNullOrWhiteSpace(absolutePath))
        {
            return null;
        }

        return Path.GetRelativePath(workspacePath, absolutePath).Replace('\\', '/');
    }

    public async Task<string?> ExportMediaKitAsync(string workspacePath, CreatorManifest? manifest, string targetDirectory)
    {
        var brandingPath = GetBrandingPath(workspacePath);
        var brandingFiles = Directory.Exists(brandingPath)
            ? Directory.GetFiles(brandingPath).ToList()
            : new List<string>();

        var screenshotGallery = GetScreenshotGallery(workspacePath, manifest?.Screenshots);
        var featuredScreenshot = SelectFeaturedScreenshot(screenshotGallery);
        var curatedScreenshots = screenshotGallery
            .Where(item => item.IsFavorite || item.Stage != CreatorScreenshotStage.Unsorted)
            .ToList();

        if (brandingFiles.Count == 0 && featuredScreenshot == null && curatedScreenshots.Count == 0)
        {
            return null;
        }

        var kitPath = Path.Combine(targetDirectory, $"mediakit_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}");
        Directory.CreateDirectory(kitPath);
        var mediaManifestPath = Path.Combine(kitPath, "media_manifest.json");

        await Task.Run(() =>
        {
            foreach (var file in brandingFiles)
            {
                var fileName = Path.GetFileName(file);
                File.Copy(file, Path.Combine(kitPath, fileName), true);
            }

            if (featuredScreenshot != null)
            {
                var featuredExtension = Path.GetExtension(featuredScreenshot.AbsolutePath);
                File.Copy(
                    featuredScreenshot.AbsolutePath,
                    Path.Combine(kitPath, $"featured-screenshot{featuredExtension}"),
                    true);
            }

            if (curatedScreenshots.Count > 0)
            {
                var screenshotsExportRoot = Path.Combine(kitPath, "screenshots");
                Directory.CreateDirectory(screenshotsExportRoot);

                foreach (var screenshot in curatedScreenshots)
                {
                    var bucket = GetScreenshotExportBucket(screenshot);
                    var bucketPath = Path.Combine(screenshotsExportRoot, bucket);
                    Directory.CreateDirectory(bucketPath);

                    var targetName = screenshot.IsFavorite
                        ? $"featured_{screenshot.FileName}"
                        : screenshot.FileName;

                    File.Copy(screenshot.AbsolutePath, Path.Combine(bucketPath, targetName), true);
                }
            }

            var mediaManifest = new
            {
                PackName = manifest?.PackName ?? Path.GetFileName(workspacePath),
                ExportedAtUtc = DateTimeOffset.UtcNow,
                FeaturedScreenshot = featuredScreenshot == null
                    ? null
                    : Path.GetFileName($"featured-screenshot{Path.GetExtension(featuredScreenshot.AbsolutePath)}"),
                Screenshots = curatedScreenshots.Select(item => new
                {
                    item.FileName,
                    item.RelativePath,
                    Stage = item.Stage.ToString(),
                    item.IsFavorite
                })
            };

            File.WriteAllText(
                mediaManifestPath,
                JsonSerializer.Serialize(mediaManifest, new JsonSerializerOptions { WriteIndented = true }));
        });

        return kitPath;
    }

    private (bool IsValid, string? Error, int Width, int Height, bool HasTransparency) ValidateImage(
        string filePath,
        BrandingAssetRequirement requirement)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            using var bitmap = SKBitmap.Decode(stream);
            if (bitmap == null)
            {
                return (false, "Soubor není platný obrázek.", 0, 0, false);
            }

            var width = bitmap.Width;
            var height = bitmap.Height;
            var hasTransparency = HasAlphaChannel(bitmap);

            if (requirement.RequiresTransparency && !hasTransparency)
            {
                return (false, $"{requirement.Label} vyžaduje průhledné pozadí (alpha kanál).", width, height, hasTransparency);
            }

            return (true, null, width, height, hasTransparency);
        }
        catch (Exception ex)
        {
            return (false, $"Chyba při validaci: {ex.Message}", 0, 0, false);
        }
    }

    private void TransformAndSaveAsset(string sourceFilePath, string targetPath, BrandingAssetRequirement requirement)
    {
        using var sourceStream = File.OpenRead(sourceFilePath);
        using var sourceBitmap = SKBitmap.Decode(sourceStream);
        if (sourceBitmap == null)
        {
            throw new InvalidOperationException("Soubor není platný obrázek.");
        }

        var targetWidth = requirement.RecommendedWidth ?? sourceBitmap.Width;
        var targetHeight = requirement.RecommendedHeight ?? sourceBitmap.Height;
        var imageInfo = new SKImageInfo(targetWidth, targetHeight, SKColorType.Rgba8888, SKAlphaType.Premul);

        using var surface = SKSurface.Create(imageInfo);
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        var fullSourceRect = new SKRect(0, 0, sourceBitmap.Width, sourceBitmap.Height);
        var fullTargetRect = new SKRect(0, 0, targetWidth, targetHeight);
        var useContainMode = requirement.Slot is BrandingAssetSlot.Logo or BrandingAssetSlot.SquareIcon;

        SKRect sourceRect;
        SKRect destinationRect;

        if (useContainMode)
        {
            sourceRect = fullSourceRect;
            destinationRect = CalculateContainRect(sourceBitmap.Width, sourceBitmap.Height, targetWidth, targetHeight);
        }
        else if (requirement.AspectRatio.HasValue)
        {
            sourceRect = CalculateCropRect(sourceBitmap.Width, sourceBitmap.Height, (float)requirement.AspectRatio.Value);
            destinationRect = fullTargetRect;
        }
        else
        {
            sourceRect = fullSourceRect;
            destinationRect = fullTargetRect;
        }

        using var paint = new SKPaint
        {
            FilterQuality = SKFilterQuality.High,
            IsAntialias = true,
            IsDither = true
        };

        canvas.DrawBitmap(sourceBitmap, sourceRect, destinationRect, paint);
        canvas.Flush();

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var outputStream = File.Open(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
        data.SaveTo(outputStream);
    }

    private static SKRect CalculateContainRect(int sourceWidth, int sourceHeight, int targetWidth, int targetHeight)
    {
        var scale = Math.Min((float)targetWidth / sourceWidth, (float)targetHeight / sourceHeight);
        var drawWidth = sourceWidth * scale;
        var drawHeight = sourceHeight * scale;
        var left = (targetWidth - drawWidth) * 0.5f;
        var top = (targetHeight - drawHeight) * 0.5f;
        return new SKRect(left, top, left + drawWidth, top + drawHeight);
    }

    private static SKRect CalculateCropRect(int sourceWidth, int sourceHeight, float targetAspectRatio)
    {
        var sourceAspectRatio = (float)sourceWidth / sourceHeight;
        if (Math.Abs(sourceAspectRatio - targetAspectRatio) < 0.001f)
        {
            return new SKRect(0, 0, sourceWidth, sourceHeight);
        }

        if (sourceAspectRatio > targetAspectRatio)
        {
            var cropWidth = sourceHeight * targetAspectRatio;
            var left = (sourceWidth - cropWidth) * 0.5f;
            return new SKRect(left, 0, left + cropWidth, sourceHeight);
        }

        var cropHeight = sourceWidth / targetAspectRatio;
        var top = (sourceHeight - cropHeight) * 0.5f;
        return new SKRect(0, top, sourceWidth, top + cropHeight);
    }

    private bool HasAlphaChannel(SKBitmap bitmap)
    {
        if (bitmap.AlphaType == SKAlphaType.Opaque)
        {
            return false;
        }

        for (var y = 0; y < bitmap.Height; y += Math.Max(1, bitmap.Height / 10))
        {
            for (var x = 0; x < bitmap.Width; x += Math.Max(1, bitmap.Width / 10))
            {
                var pixel = bitmap.GetPixel(x, y);
                if (pixel.Alpha < 255)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private CreatorAssetMetadata BuildAssetMetadata(BrandingAssetSlot slot, string absolutePath, int width, int height, bool hasTransparency)
    {
        return new CreatorAssetMetadata
        {
            Slot = slot.ToString(),
            RelativePath = Path.Combine(BrandingFolderName, Path.GetFileName(absolutePath)).Replace('\\', '/'),
            Width = width,
            Height = height,
            FileSizeBytes = new FileInfo(absolutePath).Length,
            HasTransparency = hasTransparency,
            UploadedUtc = new DateTimeOffset(File.GetLastWriteTimeUtc(absolutePath), TimeSpan.Zero)
        };
    }

    private CreatorScreenshotGalleryItem? BuildScreenshotGalleryItem(string workspacePath, CreatorScreenshotMetadata metadata)
    {
        var relativePath = metadata.RelativePath.Replace('/', Path.DirectorySeparatorChar);
        var absolutePath = Path.Combine(workspacePath, relativePath);
        if (!File.Exists(absolutePath))
        {
            return null;
        }

        return new CreatorScreenshotGalleryItem
        {
            RelativePath = metadata.RelativePath.Replace('\\', '/'),
            AbsolutePath = absolutePath,
            PreviewUri = new Uri(absolutePath).AbsoluteUri,
            FileName = Path.GetFileName(absolutePath),
            Stage = ParseScreenshotStage(metadata.Stage),
            IsFavorite = metadata.IsFavorite,
            CapturedAtUtc = new DateTimeOffset(File.GetLastWriteTimeUtc(absolutePath), TimeSpan.Zero)
        };
    }

    private static CreatorScreenshotMetadata CloneScreenshotMetadata(CreatorScreenshotMetadata metadata)
    {
        return new CreatorScreenshotMetadata
        {
            RelativePath = metadata.RelativePath.Replace('\\', '/'),
            Stage = NormalizeScreenshotStage(metadata.Stage),
            IsFavorite = metadata.IsFavorite,
            UpdatedAtUtc = metadata.UpdatedAtUtc
        };
    }

    private static CreatorScreenshotStage ParseScreenshotStage(string? stage)
    {
        return Enum.TryParse<CreatorScreenshotStage>(stage, true, out var parsed)
            ? parsed
            : CreatorScreenshotStage.Unsorted;
    }

    private static string NormalizeScreenshotStage(string? stage)
    {
        return ParseScreenshotStage(stage).ToString();
    }

    private static CreatorScreenshotGalleryItem? SelectFeaturedScreenshot(IReadOnlyList<CreatorScreenshotGalleryItem> screenshots)
    {
        return screenshots.FirstOrDefault(item => item.IsFavorite)
            ?? screenshots.FirstOrDefault(item => item.IsOfficial)
            ?? screenshots.FirstOrDefault(item => item.IsReleaseCandidate)
            ?? screenshots.FirstOrDefault();
    }

    private static string GetScreenshotExportBucket(CreatorScreenshotGalleryItem screenshot)
    {
        return screenshot.Stage switch
        {
            CreatorScreenshotStage.Official => "official",
            CreatorScreenshotStage.ReleaseCandidate => "release-candidate",
            CreatorScreenshotStage.Archive => "archive",
            _ => screenshot.IsFavorite ? "featured" : "curated"
        };
    }

    private (bool IsValid, int Width, int Height, bool HasTransparency) ReadImageInfo(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            using var bitmap = SKBitmap.Decode(stream);
            if (bitmap == null)
            {
                return (false, 0, 0, false);
            }

            return (true, bitmap.Width, bitmap.Height, HasAlphaChannel(bitmap));
        }
        catch
        {
            return (false, 0, 0, false);
        }
    }
}
