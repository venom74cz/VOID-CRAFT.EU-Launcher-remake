using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using VoidCraftLauncher.Models;
using VoidCraftLauncher.Models.CreatorStudio;
using VoidCraftLauncher.Services;

namespace VoidCraftLauncher.ViewModels;

public partial class MainViewModel
{
    [ObservableProperty]
    private string? _brandingLogoPreview;

    [ObservableProperty]
    private string? _brandingCoverPreview;

    [ObservableProperty]
    private string? _brandingSquareIconPreview;

    [ObservableProperty]
    private string? _brandingWideHeroPreview;

    [ObservableProperty]
    private string? _brandingSocialPreviewPreview;

    [ObservableProperty]
    private bool _isBrandingUploading;

    partial void OnBrandingLogoPreviewChanged(string? value) => OnPropertyChanged(nameof(HasBrandingLogo));
    partial void OnBrandingCoverPreviewChanged(string? value) => OnPropertyChanged(nameof(HasBrandingCover));
    partial void OnBrandingSquareIconPreviewChanged(string? value) => OnPropertyChanged(nameof(HasBrandingSquareIcon));
    partial void OnBrandingWideHeroPreviewChanged(string? value) => OnPropertyChanged(nameof(HasBrandingWideHero));
    partial void OnBrandingSocialPreviewPreviewChanged(string? value) => OnPropertyChanged(nameof(HasBrandingSocialPreview));

    public bool HasBrandingLogo => !string.IsNullOrWhiteSpace(BrandingLogoPreview);
    public bool HasBrandingCover => !string.IsNullOrWhiteSpace(BrandingCoverPreview);
    public bool HasBrandingSquareIcon => !string.IsNullOrWhiteSpace(BrandingSquareIconPreview);
    public bool HasBrandingWideHero => !string.IsNullOrWhiteSpace(BrandingWideHeroPreview);
    public bool HasBrandingSocialPreview => !string.IsNullOrWhiteSpace(BrandingSocialPreviewPreview);

    public string BrandingLogoFallback => BrandingLogoPreview ?? CurrentModpack?.LogoUrl ?? string.Empty;

    private void RefreshBrandingPreviews()
    {
        if (!HasCreatorWorkspaceContext || string.IsNullOrWhiteSpace(CreatorWorkspaceContext.WorkspacePath))
        {
            BrandingLogoPreview = null;
            BrandingCoverPreview = null;
            BrandingSquareIconPreview = null;
            BrandingWideHeroPreview = null;
            BrandingSocialPreviewPreview = null;
            return;
        }

        BrandingLogoPreview = _creatorAssetsService.GetAssetPath(CreatorWorkspaceContext.WorkspacePath, BrandingAssetSlot.Logo);
        BrandingCoverPreview = _creatorAssetsService.GetAssetPath(CreatorWorkspaceContext.WorkspacePath, BrandingAssetSlot.Cover);
        BrandingSquareIconPreview = _creatorAssetsService.GetAssetPath(CreatorWorkspaceContext.WorkspacePath, BrandingAssetSlot.SquareIcon);
        BrandingWideHeroPreview = _creatorAssetsService.GetAssetPath(CreatorWorkspaceContext.WorkspacePath, BrandingAssetSlot.WideHero);
        BrandingSocialPreviewPreview = _creatorAssetsService.GetAssetPath(CreatorWorkspaceContext.WorkspacePath, BrandingAssetSlot.SocialPreview);
    }

    [RelayCommand]
    private async Task UploadBrandingAsset(object? parameter)
    {
        if (parameter == null || !Enum.TryParse<BrandingAssetSlot>(parameter.ToString(), out var slot))
        {
            return;
        }

        if (!HasCreatorWorkspaceContext || string.IsNullOrWhiteSpace(CreatorWorkspaceContext.WorkspacePath))
        {
            ShowToast("Creator Studio", "Nejdřív vyber pracovní workspace.", ToastSeverity.Warning);
            return;
        }

        var storageProvider = MainWindow?.StorageProvider;
        if (storageProvider == null)
        {
            return;
        }

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = $"Vyber {slot} obrázek",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Obrázky") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg" } }
            }
        });

        var file = files.FirstOrDefault();
        if (file == null)
        {
            return;
        }

        IsBrandingUploading = true;
        try
        {
            var sourcePath = file.Path.LocalPath;
            var result = await _creatorAssetsService.UploadAssetAsync(CreatorWorkspaceContext.WorkspacePath, slot, sourcePath);

            if (!result.Success)
            {
                ShowToast("Branding upload selhal", result.Error ?? "Neznámá chyba", ToastSeverity.Error, 3500);
                return;
            }

            await UpdateManifestBrandingAsync();
            RefreshBrandingPreviews();
            ShowToast("Branding asset nahrán", $"{slot} byl úspěšně přidán.", ToastSeverity.Success, 2500);
            TrackCreatorActivity($"Nahrán branding asset {slot}.");
        }
        catch (Exception ex)
        {
            LogService.Error($"Branding upload failed for {slot}", ex);
            ShowToast("Branding upload selhal", ex.Message, ToastSeverity.Error, 3500);
        }
        finally
        {
            IsBrandingUploading = false;
        }
    }

    [RelayCommand]
    private async Task RemoveBrandingAsset(object? parameter)
    {
        if (parameter == null || !Enum.TryParse<BrandingAssetSlot>(parameter.ToString(), out var slot))
        {
            return;
        }

        if (!HasCreatorWorkspaceContext || string.IsNullOrWhiteSpace(CreatorWorkspaceContext.WorkspacePath))
        {
            return;
        }

        var success = _creatorAssetsService.RemoveAsset(CreatorWorkspaceContext.WorkspacePath, slot);
        if (success)
        {
            await UpdateManifestBrandingAsync();
            RefreshBrandingPreviews();
            ShowToast("Branding asset odstraněn", $"{slot} byl smazán.", ToastSeverity.Success, 2200);
            TrackCreatorActivity($"Odstraněn branding asset {slot}.");
        }
    }

    [RelayCommand]
    private async Task ExportMediaKit()
    {
        if (!HasCreatorWorkspaceContext || string.IsNullOrWhiteSpace(CreatorWorkspaceContext.WorkspacePath))
        {
            ShowToast("Creator Studio", "Nejdřív vyber pracovní workspace.", ToastSeverity.Warning);
            return;
        }

        var storageProvider = MainWindow?.StorageProvider;
        if (storageProvider == null)
        {
            return;
        }

        var folder = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Vyber cílovou složku pro media kit",
            AllowMultiple = false
        });

        var targetFolder = folder.FirstOrDefault();
        if (targetFolder == null)
        {
            return;
        }

        try
        {
            var kitPath = await _creatorAssetsService.ExportMediaKitAsync(
                CreatorWorkspaceContext.WorkspacePath,
                targetFolder.Path.LocalPath);

            if (kitPath != null)
            {
                ShowToast("Media kit exportován", Path.GetFileName(kitPath), ToastSeverity.Success, 3000);
                TrackCreatorActivity("Exportován media kit.");
            }
            else
            {
                ShowToast("Export selhal", "Žádné branding assety k exportu.", ToastSeverity.Warning, 2500);
            }
        }
        catch (Exception ex)
        {
            LogService.Error("Media kit export failed", ex);
            ShowToast("Export selhal", ex.Message, ToastSeverity.Error, 3500);
        }
    }

    [RelayCommand]
    private async Task GenerateManifestFromExisting()
    {
        if (!HasCreatorWorkspaceContext || string.IsNullOrWhiteSpace(CreatorWorkspaceContext.WorkspacePath))
        {
            ShowToast("Creator Studio", "Nejdřív vyber pracovní instanci.", ToastSeverity.Warning);
            return;
        }

        var modpack = GetCreatorStudioSelectedModpack();
        if (modpack == null)
        {
            ShowToast("Creator Studio", "Modpack data nejsou dostupná.", ToastSeverity.Warning);
            return;
        }

        try
        {
            var loaderVersion = modpack.IsCustomProfile ? modpack.CustomModLoaderVersion : string.Empty;
            var manifest = _creatorManifestService.CreateFallbackManifest(
                modpack,
                CreatorWorkspaceContext.MinecraftVersion,
                CreatorWorkspaceContext.LoaderLabel,
                loaderVersion ?? string.Empty);

            var savedManifest = await _creatorManifestService.SaveManifestAsync(CreatorWorkspaceContext.WorkspacePath, manifest);
            ApplyCreatorMetadata(savedManifest);

            if (!string.IsNullOrWhiteSpace(modpack.LogoUrl))
            {
                await _creatorManifestService.TryImportPublicBrandingAsync(CreatorWorkspaceContext.WorkspacePath, modpack);
                RefreshBrandingPreviews();
            }

            ShowToast("Creator manifest vygenerován", "Metadata a branding byly naimportovány z existujícího packu.", ToastSeverity.Success, 3200);
            TrackCreatorActivity("Vygenerován creator manifest z existujícího packu.");
            RefreshCreatorWorkspaceContext();
        }
        catch (Exception ex)
        {
            LogService.Error("Generate manifest from existing failed", ex);
            ShowToast("Generování selhalo", ex.Message, ToastSeverity.Error, 3500);
        }
    }

    private async Task UpdateManifestBrandingAsync()
    {
        if (!HasCreatorWorkspaceContext || string.IsNullOrWhiteSpace(CreatorWorkspaceContext.WorkspacePath))
        {
            return;
        }

        var manifest = _creatorManifestService.LoadManifest(CreatorWorkspaceContext.WorkspacePath);
        if (manifest == null)
        {
            return;
        }

        manifest.Branding = new CreatorBrandingProfile
        {
            LogoPath = _creatorAssetsService.GetAssetPath(CreatorWorkspaceContext.WorkspacePath, BrandingAssetSlot.Logo),
            CoverPath = _creatorAssetsService.GetAssetPath(CreatorWorkspaceContext.WorkspacePath, BrandingAssetSlot.Cover),
            SquareIconPath = _creatorAssetsService.GetAssetPath(CreatorWorkspaceContext.WorkspacePath, BrandingAssetSlot.SquareIcon),
            WideHeroPath = _creatorAssetsService.GetAssetPath(CreatorWorkspaceContext.WorkspacePath, BrandingAssetSlot.WideHero),
            SocialPreviewPath = _creatorAssetsService.GetAssetPath(CreatorWorkspaceContext.WorkspacePath, BrandingAssetSlot.SocialPreview),
            LastUpdatedUtc = DateTimeOffset.UtcNow
        };

        await _creatorManifestService.SaveManifestAsync(CreatorWorkspaceContext.WorkspacePath, manifest);
    }
}
