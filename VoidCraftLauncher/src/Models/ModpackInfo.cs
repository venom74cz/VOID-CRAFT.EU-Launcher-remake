using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Linq;

namespace VoidCraftLauncher.Models
{
    public class ModpackVersion
    {
        public string Name { get; set; } = "";
        public string FileId { get; set; } = "";
        public string ReleaseDate { get; set; } = "";

        /// <summary>Sentinel FileId marking "always track latest"</summary>
        public const string LatestFileId = "__latest__";

        public bool IsLatestSentinel => FileId == LatestFileId;

        public static ModpackVersion CreateLatest() => new() { Name = "⭐ Latest", FileId = LatestFileId };
        
        public override string ToString() => Name;

        public override bool Equals(object? obj)
        {
            if (obj is not ModpackVersion other) return false;
            return string.Equals(FileId, other.FileId, System.StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode() => FileId?.GetHashCode() ?? 0;
    }

    public partial class ModpackInfo : ObservableObject
    {
        [ObservableProperty]
        private int _projectId;

        [ObservableProperty]
        private string _source = "CurseForge"; // "CurseForge" or "Modrinth" or "Custom"

        [ObservableProperty]
        private string _modrinthId = ""; // For Modrinth project IDs (string)

        [ObservableProperty]
        private string _voidRegistryProjectId = "";

        [ObservableProperty]
        private string _voidRegistrySlug = "";

        [ObservableProperty]
        private string _webLink = ""; // URL to the modpack page

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayLabel))]
        private string _name = "Načítání...";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayLabel))]
        private string _displayName = "";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(LibraryStatusLabel))]
        [NotifyPropertyChangedFor(nameof(IsDevBadgeVisible))]
        private bool _isCollaboratorWorkspace;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PlayButtonText))]
        [NotifyPropertyChangedFor(nameof(IsUpdateAvailable))]
        [NotifyPropertyChangedFor(nameof(InstalledVersionName))]
        [NotifyPropertyChangedFor(nameof(TargetVersionName))]
        [NotifyPropertyChangedFor(nameof(VersionTransitionText))]
        [NotifyPropertyChangedFor(nameof(PlayButtonBackground))]
        private ModpackVersion _currentVersion = new ModpackVersion { Name = "-" };

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PlayButtonText))]
        [NotifyPropertyChangedFor(nameof(IsUpdateAvailable))]
        [NotifyPropertyChangedFor(nameof(InstalledVersionName))]
        [NotifyPropertyChangedFor(nameof(TargetVersionName))]
        [NotifyPropertyChangedFor(nameof(VersionTransitionText))]
        [NotifyPropertyChangedFor(nameof(PlayButtonBackground))]
        private ObservableCollection<ModpackVersion> _versions = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PlayButtonText))]
        [NotifyPropertyChangedFor(nameof(IsUpdateAvailable))]
        [NotifyPropertyChangedFor(nameof(TargetVersionName))]
        [NotifyPropertyChangedFor(nameof(VersionTransitionText))]
        [NotifyPropertyChangedFor(nameof(PlayButtonBackground))]
        private ModpackVersion _targetVersion;

        [ObservableProperty]
        private string _author = "";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ImageUrl))]
        [NotifyPropertyChangedFor(nameof(DisplayLogoUrl))]
        private string _logoUrl = "";
        
        public string ImageUrl => LogoUrl;

        public string DisplayLabel => string.IsNullOrWhiteSpace(DisplayName) ? Name : DisplayName;

        public string LibraryStatusLabel => IsCollaboratorWorkspace ? "Creator workspace" : "Připraveno";

        /// <summary>True when the card should display the .dev ribbon badge (top-right corner).</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsDevBadgeVisible => IsCustomProfile || IsCollaboratorWorkspace;

        /// <summary>Logo pro kartu — custom profily bez nastaveného loga zobrazí výchozí obrázek.</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public string DisplayLogoUrl => string.IsNullOrWhiteSpace(LogoUrl) && IsCustomProfile
            ? "avares://VoidCraftLauncher/Assets/custom_profile_default.png"
            : LogoUrl;

        [ObservableProperty]
        private string _description = "";

        [ObservableProperty]
        private bool _isDeletable = true;

        /// <summary>Custom profile = user-created, allows adding/removing individual mods</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsDevBadgeVisible))]
        [NotifyPropertyChangedFor(nameof(DisplayLogoUrl))]
        private bool _isCustomProfile = false;

        /// <summary>MC version for custom profiles (e.g. "1.21.1")</summary>
        [ObservableProperty]
        private string _customMcVersion = "";

        /// <summary>Mod loader for custom profiles (e.g. "forge", "fabric", "neoforge")</summary>
        [ObservableProperty]
        private string _customModLoader = "";

        /// <summary>Specific mod loader version (e.g. "0.16.5")</summary>
        [ObservableProperty]
        private string _customModLoaderVersion = "";

        // Dynamic Button Text Logic
        public string PlayButtonText 
        {
            get
            {
                if (IsUpdateAvailable)
                {
                    return "AKTUALIZOVAT";
                }
                return "HRÁT";
            }
        }

        public string PlayButtonBackground => IsUpdateAvailable ? "#3A3A3A" : "#007ACC";

        public string InstalledVersionName => CurrentVersion?.Name ?? "-";

        /// <summary>True when user selected ⭐ Latest (or has no explicit selection)</summary>
        public bool IsTrackingLatest =>
            TargetVersion == null || TargetVersion.IsLatestSentinel;

        /// <summary>The actual version we would install — resolves Latest to first real version</summary>
        public ModpackVersion? ResolvedTargetVersion =>
            IsTrackingLatest
                ? Versions?.FirstOrDefault(v => !v.IsLatestSentinel)
                : TargetVersion;

        public string TargetVersionName => ResolvedTargetVersion?.Name ?? "-";

        public string VersionTransitionText => IsUpdateAvailable
            ? $"{InstalledVersionName} → {TargetVersionName}"
            : InstalledVersionName;

        private bool HasDifferentTargetFileId()
        {
            var target = ResolvedTargetVersion;
            if (target == null || CurrentVersion == null || CurrentVersion.Name == "-")
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(target.FileId) && !string.IsNullOrWhiteSpace(CurrentVersion.FileId))
            {
                return !string.Equals(target.FileId, CurrentVersion.FileId, System.StringComparison.OrdinalIgnoreCase);
            }

            return target.Name != CurrentVersion.Name;
        }
        
        /// <summary>
        /// Update is available ONLY when tracking latest and installed differs from newest.
        /// Pinned version never triggers update prompt.
        /// </summary>
        public bool IsUpdateAvailable => 
            IsTrackingLatest &&
            Versions != null && Versions.Count > 1 && 
            CurrentVersion != null && CurrentVersion.Name != "-" && 
            HasDifferentTargetFileId();
    }
}
