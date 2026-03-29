using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SkiaSharp;
using VoidCraftLauncher.Models.CreatorStudio;

namespace VoidCraftLauncher.Services.CreatorStudio;

public sealed class CreatorAssetsService
{
    private const string BrandingFolderName = "assets/branding";

    public string GetBrandingPath(string workspacePath)
    {
        return Path.Combine(workspacePath, BrandingFolderName);
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

        var extension = Path.GetExtension(sourceFilePath);
        var targetFileName = $"{slot.ToString().ToLowerInvariant()}{extension}";
        var targetPath = Path.Combine(brandingPath, targetFileName);

        await Task.Run(() => File.Copy(sourceFilePath, targetPath, true));

        var metadata = new CreatorAssetMetadata
        {
            Slot = slot.ToString(),
            RelativePath = Path.Combine(BrandingFolderName, targetFileName).Replace('\\', '/'),
            Width = validation.Width,
            Height = validation.Height,
            FileSizeBytes = new FileInfo(targetPath).Length,
            HasTransparency = validation.HasTransparency,
            UploadedUtc = DateTimeOffset.UtcNow
        };

        return (true, null, metadata);
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
        return files.FirstOrDefault();
    }

    public async Task<string?> ExportMediaKitAsync(string workspacePath, string targetDirectory)
    {
        var brandingPath = GetBrandingPath(workspacePath);
        if (!Directory.Exists(brandingPath))
        {
            return null;
        }

        var kitPath = Path.Combine(targetDirectory, $"mediakit_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}");
        Directory.CreateDirectory(kitPath);

        await Task.Run(() =>
        {
            foreach (var file in Directory.GetFiles(brandingPath))
            {
                var fileName = Path.GetFileName(file);
                File.Copy(file, Path.Combine(kitPath, fileName), true);
            }
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

            if (requirement.AspectRatio.HasValue)
            {
                var actualRatio = (double)width / height;
                var expectedRatio = requirement.AspectRatio.Value;
                var tolerance = 0.05;
                if (Math.Abs(actualRatio - expectedRatio) > tolerance)
                {
                    return (false, $"{requirement.Label} má nesprávný poměr stran. Očekáváno ~{expectedRatio:F2}, nalezeno {actualRatio:F2}.", width, height, hasTransparency);
                }
            }

            if (requirement.RecommendedWidth.HasValue && width < requirement.RecommendedWidth.Value * 0.5)
            {
                return (false, $"{requirement.Label} je příliš malý. Minimální šířka je {requirement.RecommendedWidth.Value * 0.5}px.", width, height, hasTransparency);
            }

            return (true, null, width, height, hasTransparency);
        }
        catch (Exception ex)
        {
            return (false, $"Chyba při validaci: {ex.Message}", 0, 0, false);
        }
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
}
