using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using VoidCraftLauncher.Models;
using VoidCraftLauncher.Models.CreatorStudio;
using VoidCraftLauncher.Services;

namespace VoidCraftLauncher.ViewModels;

public partial class MainViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCreatorScreenshots))]
    private ObservableCollection<CreatorScreenshotGalleryItem> _creatorScreenshotGallery = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFeaturedCreatorScreenshot))]
    private string? _featuredCreatorScreenshotPreview;

    public bool HasCreatorScreenshots => CreatorScreenshotGallery.Count > 0;

    public bool HasFeaturedCreatorScreenshot => !string.IsNullOrWhiteSpace(FeaturedCreatorScreenshotPreview);

    public string CreatorScreenshotGallerySummary
    {
        get
        {
            if (!HasCreatorWorkspaceContext)
            {
                return "Screenshot galerie ceka na vybrany workspace.";
            }

            if (!HasCreatorScreenshots)
            {
                return "Ve workspace zatim nejsou zadne screenshoty pro kuraci.";
            }

            var officialCount = CreatorScreenshotGallery.Count(item => item.IsOfficial);
            var releaseCandidateCount = CreatorScreenshotGallery.Count(item => item.IsReleaseCandidate);
            var archiveCount = CreatorScreenshotGallery.Count(item => item.IsArchive);
            var favoriteCount = CreatorScreenshotGallery.Count(item => item.IsFavorite);

            return $"{CreatorScreenshotGallery.Count} screenshotu • {officialCount} official • {releaseCandidateCount} release candidate • {archiveCount} archive • {favoriteCount} favorit";
        }
    }

    public string CreatorScreenshotFolderPath => HasCreatorWorkspaceContext && !string.IsNullOrWhiteSpace(CreatorWorkspaceContext.WorkspacePath)
        ? _creatorAssetsService.GetScreenshotGalleryPath(CreatorWorkspaceContext.WorkspacePath)
        : string.Empty;

    public string FeaturedCreatorScreenshotCaption => GetFeaturedCreatorScreenshotItem() is { } screenshot
        ? $"{screenshot.FileName} • {screenshot.CapturedLabel}"
        : "Pripni favorit nebo oznac screenshot jako official a preview ho pouzije automaticky.";

    public string FeaturedCreatorScreenshotStatus
    {
        get
        {
            var screenshot = GetFeaturedCreatorScreenshotItem();
            if (screenshot == null)
            {
                return "Zadny promo screenshot zatim neni vybrany.";
            }

            if (screenshot.IsFavorite)
            {
                return $"Favorit • {screenshot.StageLabel}";
            }

            return screenshot.Stage switch
            {
                CreatorScreenshotStage.Official => "Official screenshot",
                CreatorScreenshotStage.ReleaseCandidate => "Release candidate",
                CreatorScreenshotStage.Archive => "Archivni fallback",
                _ => "Automaticky fallback z galerie"
            };
        }
    }

    private void RefreshCreatorScreenshotGallery()
    {
        if (!HasCreatorWorkspaceContext || string.IsNullOrWhiteSpace(CreatorWorkspaceContext.WorkspacePath))
        {
            CreatorScreenshotGallery = new ObservableCollection<CreatorScreenshotGalleryItem>();
            FeaturedCreatorScreenshotPreview = null;
            NotifyCreatorScreenshotStateChanged();
            return;
        }

        var manifest = _creatorManifestService.LoadManifest(CreatorWorkspaceContext.WorkspacePath) ?? CurrentModpackCreatorManifest;
        var gallery = _creatorAssetsService
            .GetScreenshotGallery(CreatorWorkspaceContext.WorkspacePath, manifest?.Screenshots)
            .ToList();

        CreatorScreenshotGallery = new ObservableCollection<CreatorScreenshotGalleryItem>(gallery);
        FeaturedCreatorScreenshotPreview = SelectFeaturedCreatorScreenshot(gallery)?.PreviewUri;
        NotifyCreatorScreenshotStateChanged();
    }

    private void NotifyCreatorScreenshotStateChanged()
    {
        OnPropertyChanged(nameof(HasCreatorScreenshots));
        OnPropertyChanged(nameof(CreatorScreenshotGallerySummary));
        OnPropertyChanged(nameof(CreatorScreenshotFolderPath));
        OnPropertyChanged(nameof(HasFeaturedCreatorScreenshot));
        OnPropertyChanged(nameof(FeaturedCreatorScreenshotCaption));
        OnPropertyChanged(nameof(FeaturedCreatorScreenshotStatus));
    }

    private CreatorScreenshotGalleryItem? GetFeaturedCreatorScreenshotItem()
    {
        return SelectFeaturedCreatorScreenshot(CreatorScreenshotGallery);
    }

    private static CreatorScreenshotGalleryItem? SelectFeaturedCreatorScreenshot(System.Collections.Generic.IReadOnlyList<CreatorScreenshotGalleryItem> screenshots)
    {
        return screenshots.FirstOrDefault(item => item.IsFavorite)
            ?? screenshots.FirstOrDefault(item => item.IsOfficial)
            ?? screenshots.FirstOrDefault(item => item.IsReleaseCandidate)
            ?? screenshots.FirstOrDefault();
    }

    [RelayCommand]
    private void OpenCreatorScreenshotFolder()
    {
        if (!HasCreatorWorkspaceContext || string.IsNullOrWhiteSpace(CreatorWorkspaceContext.WorkspacePath))
        {
            ShowToast("Creator Studio", "Nejdřív vyber pracovní workspace.", ToastSeverity.Warning);
            return;
        }

        var screenshotFolder = _creatorAssetsService.GetScreenshotGalleryPath(CreatorWorkspaceContext.WorkspacePath, ensureExists: true);
        OpenFolder(screenshotFolder);
        TrackCreatorActivity("Otevrena screenshot slozka workspace.");
    }

    [RelayCommand]
    private void RefreshCreatorScreenshots()
    {
        RefreshCreatorScreenshotGallery();
    }

    [RelayCommand]
    private async Task ToggleCreatorScreenshotFavorite(CreatorScreenshotGalleryItem? screenshot)
    {
        if (screenshot == null)
        {
            return;
        }

        await PersistCreatorScreenshotMetadataAsync(
            screenshot,
            (entries, entry) =>
            {
                var shouldFavorite = !entry.IsFavorite;
                foreach (var candidate in entries)
                {
                    candidate.IsFavorite = false;
                }

                entry.IsFavorite = shouldFavorite;
                entry.UpdatedAtUtc = DateTimeOffset.UtcNow;
            },
            screenshot.IsFavorite ? "Favorit odepnut" : "Favorit pripnut",
            screenshot.IsFavorite
                ? $"{screenshot.FileName} uz neni hlavni promo screenshot."
                : $"{screenshot.FileName} je ted hlavni promo screenshot.",
            screenshot.IsFavorite
                ? $"Screenshot {screenshot.FileName} byl odepnut z favorita."
                : $"Screenshot {screenshot.FileName} byl pripnut jako favorit.");
    }

    [RelayCommand]
    private Task MarkCreatorScreenshotOfficial(CreatorScreenshotGalleryItem? screenshot)
    {
        return SetCreatorScreenshotStageAsync(screenshot, CreatorScreenshotStage.Official, "official");
    }

    [RelayCommand]
    private Task MarkCreatorScreenshotReleaseCandidate(CreatorScreenshotGalleryItem? screenshot)
    {
        return SetCreatorScreenshotStageAsync(screenshot, CreatorScreenshotStage.ReleaseCandidate, "release candidate");
    }

    [RelayCommand]
    private Task MarkCreatorScreenshotArchive(CreatorScreenshotGalleryItem? screenshot)
    {
        return SetCreatorScreenshotStageAsync(screenshot, CreatorScreenshotStage.Archive, "archive");
    }

    [RelayCommand]
    private Task UseCreatorScreenshotAsCover(CreatorScreenshotGalleryItem? screenshot)
    {
        return ApplyCreatorScreenshotToBrandingSlotAsync(screenshot, BrandingAssetSlot.Cover, "Cover pripraven", "Screenshot byl pouzit jako cover asset.");
    }

    [RelayCommand]
    private Task UseCreatorScreenshotAsSocialPreview(CreatorScreenshotGalleryItem? screenshot)
    {
        return ApplyCreatorScreenshotToBrandingSlotAsync(screenshot, BrandingAssetSlot.SocialPreview, "Social preview pripraven", "Screenshot byl pouzit jako social preview asset.");
    }

    [RelayCommand]
    private async Task ClearCreatorScreenshotStage(CreatorScreenshotGalleryItem? screenshot)
    {
        if (screenshot == null)
        {
            return;
        }

        await PersistCreatorScreenshotMetadataAsync(
            screenshot,
            (_, entry) =>
            {
                entry.Stage = CreatorScreenshotStage.Unsorted.ToString();
                entry.UpdatedAtUtc = DateTimeOffset.UtcNow;
            },
            "Tag vycisten",
            $"{screenshot.FileName} uz nema stage tag.",
            $"Screenshot {screenshot.FileName} byl vracen do defaultniho stavu.");
    }

    private Task SetCreatorScreenshotStageAsync(CreatorScreenshotGalleryItem? screenshot, CreatorScreenshotStage stage, string label)
    {
        if (screenshot == null)
        {
            return Task.CompletedTask;
        }

        return PersistCreatorScreenshotMetadataAsync(
            screenshot,
            (_, entry) =>
            {
                entry.Stage = stage.ToString();
                entry.UpdatedAtUtc = DateTimeOffset.UtcNow;
            },
            "Screenshot oznacen",
            $"{screenshot.FileName} je ted veden jako {label}.",
            $"Screenshot {screenshot.FileName} byl oznacen jako {label}.");
    }

    private async Task PersistCreatorScreenshotMetadataAsync(
        CreatorScreenshotGalleryItem screenshot,
        Action<System.Collections.Generic.List<CreatorScreenshotMetadata>, CreatorScreenshotMetadata> mutate,
        string toastTitle,
        string toastMessage,
        string activitySummary)
    {
        if (!IsCreatorWorkspaceEditable)
        {
            ShowToast("Creator Studio", CreatorWorkspaceEditabilityMessage, ToastSeverity.Warning, 3200);
            return;
        }

        if (!HasCreatorWorkspaceContext || string.IsNullOrWhiteSpace(CreatorWorkspaceContext.WorkspacePath))
        {
            ShowToast("Creator Studio", "Nejdřív vyber pracovní workspace.", ToastSeverity.Warning);
            return;
        }

        try
        {
            var manifest = _creatorManifestService.LoadManifest(CreatorWorkspaceContext.WorkspacePath)
                ?? CreateCreatorFallbackManifest(GetCreatorStudioSelectedModpack(), null);

            var screenshotMetadata = _creatorAssetsService
                .NormalizeScreenshotMetadata(CreatorWorkspaceContext.WorkspacePath, manifest.Screenshots)
                .ToList();

            var targetEntry = screenshotMetadata.FirstOrDefault(entry =>
                string.Equals(entry.RelativePath, screenshot.RelativePath, StringComparison.OrdinalIgnoreCase));

            if (targetEntry == null)
            {
                ShowToast("Creator Studio", "Screenshot se nepodarilo dohledat v aktualni galerii.", ToastSeverity.Warning);
                return;
            }

            mutate(screenshotMetadata, targetEntry);
            manifest.Screenshots = _creatorAssetsService.CompactScreenshotMetadata(screenshotMetadata).ToList();
            manifest.Branding = _creatorAssetsService.BuildBrandingProfile(CreatorWorkspaceContext.WorkspacePath, manifest.Screenshots);
            manifest.Assets = _creatorAssetsService.GetAssetMetadata(CreatorWorkspaceContext.WorkspacePath).ToList();

            await _creatorManifestService.SaveManifestAsync(CreatorWorkspaceContext.WorkspacePath, manifest);
            RefreshCurrentModpackCreatorManifest();
            RefreshCreatorWorkspaceContext();
            ShowToast(toastTitle, toastMessage, ToastSeverity.Success, 2400);
            TrackCreatorActivity(activitySummary);
        }
        catch (Exception ex)
        {
            LogService.Error("Creator screenshot metadata update failed", ex);
            ShowToast("Creator Studio", ex.Message, ToastSeverity.Error, 3500);
        }
    }

    private async Task ApplyCreatorScreenshotToBrandingSlotAsync(
        CreatorScreenshotGalleryItem? screenshot,
        BrandingAssetSlot slot,
        string toastTitle,
        string activitySummary)
    {
        if (screenshot == null)
        {
            return;
        }

        if (!IsCreatorWorkspaceEditable)
        {
            ShowToast("Creator Studio", CreatorWorkspaceEditabilityMessage, ToastSeverity.Warning, 3200);
            return;
        }

        if (!HasCreatorWorkspaceContext || string.IsNullOrWhiteSpace(CreatorWorkspaceContext.WorkspacePath))
        {
            ShowToast("Creator Studio", "Nejdřív vyber pracovní workspace.", ToastSeverity.Warning);
            return;
        }

        try
        {
            var result = await _creatorAssetsService.UploadAssetAsync(CreatorWorkspaceContext.WorkspacePath, slot, screenshot.AbsolutePath);
            if (!result.Success)
            {
                ShowToast("Branding asset nevznikl", result.Error ?? "Screenshot se nepodařilo převést na branding asset.", ToastSeverity.Warning, 3200);
                return;
            }

            await UpdateManifestBrandingAsync();
            RefreshBrandingPreviews();
            RefreshCurrentModpackCreatorManifest();
            RefreshCreatorWorkspaceContext();
            ShowToast(toastTitle, $"{screenshot.FileName} byl použit pro slot {slot}.", ToastSeverity.Success, 2600);
            TrackCreatorActivity($"{activitySummary} Slot {slot} z {screenshot.FileName}.");
        }
        catch (Exception ex)
        {
            LogService.Error($"Creator screenshot apply to {slot} failed", ex);
            ShowToast("Branding asset nevznikl", ex.Message, ToastSeverity.Error, 3500);
        }
    }
}