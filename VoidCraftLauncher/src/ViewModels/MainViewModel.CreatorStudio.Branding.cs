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

    partial void OnBrandingLogoPreviewChanged(string? value)
    {
        OnPropertyChanged(nameof(HasBrandingLogo));
        OnPropertyChanged(nameof(HasBrandingLogoAsset));
        OnPropertyChanged(nameof(BrandingLogoFallback));
    }
    partial void OnBrandingCoverPreviewChanged(string? value) => OnPropertyChanged(nameof(HasBrandingCover));
    partial void OnBrandingSquareIconPreviewChanged(string? value) => OnPropertyChanged(nameof(HasBrandingSquareIcon));
    partial void OnBrandingWideHeroPreviewChanged(string? value) => OnPropertyChanged(nameof(HasBrandingWideHero));
    partial void OnBrandingSocialPreviewPreviewChanged(string? value) => OnPropertyChanged(nameof(HasBrandingSocialPreview));

    public bool HasBrandingLogo => !string.IsNullOrWhiteSpace(BrandingLogoFallback);
    public bool HasBrandingLogoAsset => !string.IsNullOrWhiteSpace(BrandingLogoPreview);
    public bool HasBrandingCover => !string.IsNullOrWhiteSpace(BrandingCoverPreview);
    public bool HasBrandingSquareIcon => !string.IsNullOrWhiteSpace(BrandingSquareIconPreview);
    public bool HasBrandingWideHero => !string.IsNullOrWhiteSpace(BrandingWideHeroPreview);
    public bool HasBrandingSocialPreview => !string.IsNullOrWhiteSpace(BrandingSocialPreviewPreview);

    public string BrandingLogoFallback => BrandingLogoPreview ?? GetCreatorStudioSelectedModpack()?.LogoUrl ?? CurrentModpack?.LogoUrl ?? string.Empty;

    private void RefreshBrandingPreviews()
    {
        if (!HasCreatorWorkspaceContext || string.IsNullOrWhiteSpace(CreatorWorkspaceContext.WorkspacePath))
        {
            BrandingLogoPreview = null;
            BrandingCoverPreview = null;
            BrandingSquareIconPreview = null;
            BrandingWideHeroPreview = null;
            BrandingSocialPreviewPreview = null;
            NotifyBrandingStateChanged();
            return;
        }

        BrandingLogoPreview = _creatorAssetsService.GetAssetPath(CreatorWorkspaceContext.WorkspacePath, BrandingAssetSlot.Logo);
        BrandingCoverPreview = _creatorAssetsService.GetAssetPath(CreatorWorkspaceContext.WorkspacePath, BrandingAssetSlot.Cover);
        BrandingSquareIconPreview = _creatorAssetsService.GetAssetPath(CreatorWorkspaceContext.WorkspacePath, BrandingAssetSlot.SquareIcon);
        BrandingWideHeroPreview = _creatorAssetsService.GetAssetPath(CreatorWorkspaceContext.WorkspacePath, BrandingAssetSlot.WideHero);
        BrandingSocialPreviewPreview = _creatorAssetsService.GetAssetPath(CreatorWorkspaceContext.WorkspacePath, BrandingAssetSlot.SocialPreview);
        NotifyBrandingStateChanged();
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
                new FilePickerFileType("Obrázky") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.webp" } }
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
            RefreshCurrentModpackCreatorManifest();
            RefreshCreatorWorkspaceContext();
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
            RefreshCurrentModpackCreatorManifest();
            RefreshCreatorWorkspaceContext();
            ShowToast("Branding asset odstraněn", $"{slot} byl smazán.", ToastSeverity.Success, 2200);
            TrackCreatorActivity($"Odstraněn branding asset {slot}.");
        }
    }

    [RelayCommand]
    private void OpenCreatorBrandingFolder()
    {
        if (!HasCreatorWorkspaceContext || string.IsNullOrWhiteSpace(CreatorWorkspaceContext.WorkspacePath))
        {
            ShowToast("Creator Studio", "Nejdřív vyber pracovní workspace.", ToastSeverity.Warning);
            return;
        }

        var brandingPath = _creatorAssetsService.GetBrandingPath(CreatorWorkspaceContext.WorkspacePath);
        Directory.CreateDirectory(brandingPath);
        OpenFolder(brandingPath);
        TrackCreatorActivity("Otevřena branding složka workspace.");
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
            await TryRefreshModpackSourceMetadataAsync(modpack);

            var loaderVersion = modpack.IsCustomProfile ? modpack.CustomModLoaderVersion : string.Empty;
            var existingManifest = _creatorManifestService.LoadManifest(CreatorWorkspaceContext.WorkspacePath);
            var manifest = CreateCreatorFallbackManifest(modpack, existingManifest, loaderVersion ?? string.Empty);

            var savedManifest = await _creatorManifestService.SaveManifestAsync(CreatorWorkspaceContext.WorkspacePath, manifest);
            ApplyCreatorMetadata(savedManifest);

            if (!string.IsNullOrWhiteSpace(modpack.LogoUrl))
            {
                await _creatorManifestService.TryImportPublicBrandingAsync(CreatorWorkspaceContext.WorkspacePath, modpack);
                await UpdateManifestBrandingAsync();
                RefreshBrandingPreviews();
            }

            RefreshCurrentModpackCreatorManifest();
            SyncInstalledModpackFromCreatorManifest(CreatorWorkspaceContext.WorkspaceId, savedManifest);
            ShowToast("Creator manifest připraven", "Metadata a branding byly načteny ze zdrojového packu.", ToastSeverity.Success, 3200);
            TrackCreatorActivity("Načtena metadata a branding ze zdrojového packu.");
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
            manifest = ValidateCreatorMetadata(out _)
                ? BuildCreatorManifestFromEditor(null)
                : CreateCreatorFallbackManifest(GetCreatorStudioSelectedModpack(), null);
        }

        manifest.BrandProfile ??= BuildCurrentBrandProfile();
        manifest.Branding = _creatorAssetsService.BuildBrandingProfile(CreatorWorkspaceContext.WorkspacePath);
        manifest.Assets = _creatorAssetsService.GetAssetMetadata(CreatorWorkspaceContext.WorkspacePath).ToList();

        var savedManifest = await _creatorManifestService.SaveManifestAsync(CreatorWorkspaceContext.WorkspacePath, manifest);
        RefreshCurrentModpackCreatorManifest();
        SyncInstalledModpackFromCreatorManifest(CreatorWorkspaceContext.WorkspaceId, savedManifest);
    }

    private void NotifyBrandingStateChanged()
    {
        OnPropertyChanged(nameof(HasBrandingLogo));
        OnPropertyChanged(nameof(HasBrandingLogoAsset));
        OnPropertyChanged(nameof(HasBrandingCover));
        OnPropertyChanged(nameof(HasBrandingSquareIcon));
        OnPropertyChanged(nameof(HasBrandingWideHero));
        OnPropertyChanged(nameof(HasBrandingSocialPreview));
        OnPropertyChanged(nameof(BrandingLogoFallback));
        OnPropertyChanged(nameof(BrandStorageStatus));
        OnPropertyChanged(nameof(LauncherPreviewAssetStatus));
    }

    private async Task<bool> EnsureCreatorWorkspacePublicBrandingAsync(string workspaceId, ModpackInfo modpack)
    {
        if (string.IsNullOrWhiteSpace(workspaceId) || string.IsNullOrWhiteSpace(modpack.LogoUrl))
        {
            return false;
        }

        var workspacePath = _launcherService.GetModpackPath(workspaceId);
        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(_creatorAssetsService.GetAssetPath(workspacePath, BrandingAssetSlot.Logo)))
        {
            return false;
        }

        var imported = await _creatorManifestService.TryImportPublicBrandingAsync(workspacePath, modpack);
        if (!imported)
        {
            return false;
        }

        var manifest = _creatorManifestService.LoadManifest(workspacePath)
            ?? CreateCreatorFallbackManifest(modpack, null, modpack.CustomModLoaderVersion);
        manifest.BrandProfile ??= BuildCurrentBrandProfile();
        manifest.Branding = _creatorAssetsService.BuildBrandingProfile(workspacePath);
        manifest.Assets = _creatorAssetsService.GetAssetMetadata(workspacePath).ToList();

        var savedManifest = await _creatorManifestService.SaveManifestAsync(workspacePath, manifest);
        SyncInstalledModpackFromCreatorManifest(workspaceId, savedManifest);
        return true;
    }

    private async Task<bool> TryRefreshModpackSourceMetadataAsync(ModpackInfo modpack)
    {
        try
        {
            var metadataChanged = false;

            if (modpack.ProjectId > 0)
            {
                var modpackNode = System.Text.Json.Nodes.JsonNode.Parse(await _curseForgeApi.GetModpackInfoAsync(modpack.ProjectId))?["data"];
                var authorLabel = string.Join(", ", modpackNode?["authors"]?.AsArray()?
                    .Select(node => node?["name"]?.ToString())
                    .Where(value => !string.IsNullOrWhiteSpace(value)) ?? Enumerable.Empty<string>());

                var fetchedDisplayName = modpackNode?["name"]?.ToString();
                if (!string.IsNullOrWhiteSpace(fetchedDisplayName) && !string.Equals(modpack.DisplayName, fetchedDisplayName, StringComparison.Ordinal))
                {
                    modpack.DisplayName = fetchedDisplayName;
                    metadataChanged = true;
                }

                var fetchedSummary = modpackNode?["summary"]?.ToString();
                if (string.IsNullOrWhiteSpace(modpack.Description) && !string.IsNullOrWhiteSpace(fetchedSummary))
                {
                    modpack.Description = fetchedSummary;
                    metadataChanged = true;
                }

                if (string.IsNullOrWhiteSpace(modpack.Author) && !string.IsNullOrWhiteSpace(authorLabel))
                {
                    modpack.Author = authorLabel;
                    metadataChanged = true;
                }

                var logoUrl = modpackNode?["logo"]?["url"]?.ToString() ?? modpackNode?["logo"]?["thumbnailUrl"]?.ToString();
                if (string.IsNullOrWhiteSpace(modpack.LogoUrl) && !string.IsNullOrWhiteSpace(logoUrl))
                {
                    modpack.LogoUrl = logoUrl;
                    metadataChanged = true;
                }

                var webLink = modpackNode?["links"]?["websiteUrl"]?.ToString();
                if (string.IsNullOrWhiteSpace(modpack.WebLink) && !string.IsNullOrWhiteSpace(webLink))
                {
                    modpack.WebLink = webLink;
                    metadataChanged = true;
                }
            }
            else if (!string.IsNullOrWhiteSpace(modpack.ModrinthId))
            {
                var projectNode = System.Text.Json.Nodes.JsonNode.Parse(await _modrinthApi.GetProjectAsync(modpack.ModrinthId));
                var fetchedDisplayName = projectNode?["title"]?.ToString();
                if (!string.IsNullOrWhiteSpace(fetchedDisplayName) && !string.Equals(modpack.DisplayName, fetchedDisplayName, StringComparison.Ordinal))
                {
                    modpack.DisplayName = fetchedDisplayName;
                    metadataChanged = true;
                }

                var fetchedSummary = projectNode?["description"]?.ToString();
                if (string.IsNullOrWhiteSpace(modpack.Description) && !string.IsNullOrWhiteSpace(fetchedSummary))
                {
                    modpack.Description = fetchedSummary;
                    metadataChanged = true;
                }

                var fetchedAuthor = projectNode?["author"]?.ToString();
                if (string.IsNullOrWhiteSpace(modpack.Author) && !string.IsNullOrWhiteSpace(fetchedAuthor))
                {
                    modpack.Author = fetchedAuthor;
                    metadataChanged = true;
                }

                var logoUrl = projectNode?["icon_url"]?.ToString();
                if (string.IsNullOrWhiteSpace(modpack.LogoUrl) && !string.IsNullOrWhiteSpace(logoUrl))
                {
                    modpack.LogoUrl = logoUrl;
                    metadataChanged = true;
                }

                var slug = projectNode?["slug"]?.ToString();
                if (string.IsNullOrWhiteSpace(modpack.WebLink) && !string.IsNullOrWhiteSpace(slug))
                {
                    modpack.WebLink = $"https://modrinth.com/modpack/{slug}";
                    metadataChanged = true;
                }
            }

            if (metadataChanged)
            {
                SaveModpacks();
            }

            return metadataChanged;
        }
        catch (Exception ex)
        {
            LogService.Error("Creator source metadata refresh failed", ex);
            return false;
        }
    }
}
