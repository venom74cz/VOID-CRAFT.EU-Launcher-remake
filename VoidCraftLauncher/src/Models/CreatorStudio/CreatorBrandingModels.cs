using System;
using System.Collections.Generic;

namespace VoidCraftLauncher.Models.CreatorStudio;

public sealed class CreatorBrandingProfile
{
    public string? LogoPath { get; set; }
    public string? CoverPath { get; set; }
    public string? SquareIconPath { get; set; }
    public string? WideHeroPath { get; set; }
    public string? SocialPreviewPath { get; set; }
    public string? FeaturedScreenshotPath { get; set; }
    public DateTimeOffset? LastUpdatedUtc { get; set; }
}

public sealed class CreatorBrandProfile
{
    public string AccentColor { get; set; } = "#3AA0FF";
    public string LauncherCardTitle { get; set; } = string.Empty;
    public string OneLiner { get; set; } = string.Empty;
    public string Website { get; set; } = string.Empty;
    public string Discord { get; set; } = string.Empty;
    public string GitHub { get; set; } = string.Empty;
    public string SupportLink { get; set; } = string.Empty;
}

public sealed class CreatorAssetMetadata
{
    public string Slot { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public long FileSizeBytes { get; set; }
    public bool HasTransparency { get; set; }
    public DateTimeOffset UploadedUtc { get; set; }
}

public enum CreatorScreenshotStage
{
    Unsorted,
    Official,
    ReleaseCandidate,
    Archive
}

public sealed class CreatorScreenshotMetadata
{
    public string RelativePath { get; set; } = string.Empty;
    public string Stage { get; set; } = nameof(CreatorScreenshotStage.Unsorted);
    public bool IsFavorite { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class CreatorScreenshotGalleryItem
{
    public string RelativePath { get; set; } = string.Empty;
    public string AbsolutePath { get; set; } = string.Empty;
    public string PreviewUri { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public CreatorScreenshotStage Stage { get; set; } = CreatorScreenshotStage.Unsorted;
    public bool IsFavorite { get; set; }
    public DateTimeOffset CapturedAtUtc { get; set; }

    public bool IsOfficial => Stage == CreatorScreenshotStage.Official;
    public bool IsReleaseCandidate => Stage == CreatorScreenshotStage.ReleaseCandidate;
    public bool IsArchive => Stage == CreatorScreenshotStage.Archive;
    public bool HasStage => Stage != CreatorScreenshotStage.Unsorted;

    public string StageLabel => Stage switch
    {
        CreatorScreenshotStage.Official => "Official",
        CreatorScreenshotStage.ReleaseCandidate => "Release candidate",
        CreatorScreenshotStage.Archive => "Archive",
        _ => "Bez tagu"
    };

    public string StatusLabel => IsFavorite ? $"{StageLabel} • favorit" : StageLabel;

    public string FavoriteActionLabel => IsFavorite ? "Odepnout" : "Pripnout";

    public string CapturedLabel => CapturedAtUtc == default
        ? "Bez casu"
        : CapturedAtUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
}

public enum BrandingAssetSlot
{
    Logo,
    Cover,
    SquareIcon,
    WideHero,
    SocialPreview
}

public sealed class BrandingAssetRequirement
{
    public BrandingAssetSlot Slot { get; set; }
    public string Label { get; set; } = string.Empty;
    public int? RecommendedWidth { get; set; }
    public int? RecommendedHeight { get; set; }
    public double? AspectRatio { get; set; }
    public bool RequiresTransparency { get; set; }
    public string Description { get; set; } = string.Empty;

    public static IReadOnlyList<BrandingAssetRequirement> GetStandardRequirements() => new[]
    {
        new BrandingAssetRequirement
        {
            Slot = BrandingAssetSlot.Logo,
            Label = "Logo",
            RecommendedWidth = 512,
            RecommendedHeight = 512,
            AspectRatio = 1.0,
            RequiresTransparency = false,
            Description = "Hlavní logo packu, čtvercové a dobře čitelné i bez průhledného pozadí"
        },
        new BrandingAssetRequirement
        {
            Slot = BrandingAssetSlot.Cover,
            Label = "Cover",
            RecommendedWidth = 1920,
            RecommendedHeight = 1080,
            AspectRatio = 16.0 / 9.0,
            RequiresTransparency = false,
            Description = "Hlavní cover obrázek pro detail packu"
        },
        new BrandingAssetRequirement
        {
            Slot = BrandingAssetSlot.SquareIcon,
            Label = "Square Icon",
            RecommendedWidth = 256,
            RecommendedHeight = 256,
            AspectRatio = 1.0,
            RequiresTransparency = false,
            Description = "Malá ikona pro karty a seznamy; průhledné pozadí je volitelné"
        },
        new BrandingAssetRequirement
        {
            Slot = BrandingAssetSlot.WideHero,
            Label = "Wide Hero",
            RecommendedWidth = 2560,
            RecommendedHeight = 720,
            AspectRatio = 32.0 / 9.0,
            RequiresTransparency = false,
            Description = "Široký hero banner pro landing stránky"
        },
        new BrandingAssetRequirement
        {
            Slot = BrandingAssetSlot.SocialPreview,
            Label = "Social Preview",
            RecommendedWidth = 1200,
            RecommendedHeight = 630,
            AspectRatio = 1.91,
            RequiresTransparency = false,
            Description = "Náhled pro sociální sítě (Open Graph)"
        }
    };
}
