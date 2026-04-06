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
        
        public override string ToString() => Name;
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
        private bool _isCollaboratorWorkspace;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PlayButtonText))]
        [NotifyPropertyChangedFor(nameof(IsUpdateAvailable))]
        [NotifyPropertyChangedFor(nameof(InstalledVersionName))]
        [NotifyPropertyChangedFor(nameof(LatestVersionName))]
        [NotifyPropertyChangedFor(nameof(VersionTransitionText))]
        [NotifyPropertyChangedFor(nameof(PlayButtonBackground))]
        private ModpackVersion _currentVersion = new ModpackVersion { Name = "-" };

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PlayButtonText))]
        [NotifyPropertyChangedFor(nameof(IsUpdateAvailable))]
        [NotifyPropertyChangedFor(nameof(InstalledVersionName))]
        [NotifyPropertyChangedFor(nameof(LatestVersionName))]
        [NotifyPropertyChangedFor(nameof(VersionTransitionText))]
        [NotifyPropertyChangedFor(nameof(PlayButtonBackground))]
        private ObservableCollection<ModpackVersion> _versions = new();

        [ObservableProperty]
        private string _author = "";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ImageUrl))]
        private string _logoUrl = "";
        
        public string ImageUrl => LogoUrl;

        public string DisplayLabel => string.IsNullOrWhiteSpace(DisplayName) ? Name : DisplayName;

        public string LibraryStatusLabel => IsCollaboratorWorkspace ? "Creator workspace" : "Připraveno";

        [ObservableProperty]
        private string _description = "";

        [ObservableProperty]
        private bool _isDeletable = true;

        /// <summary>Custom profile = user-created, allows adding/removing individual mods</summary>
        [ObservableProperty]
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

        public string LatestVersionName => Versions?.FirstOrDefault()?.Name ?? "-";

        public string VersionTransitionText => IsUpdateAvailable
            ? $"{InstalledVersionName} → {LatestVersionName}"
            : InstalledVersionName;

        private bool HasDifferentLatestFileId()
        {
            var latest = Versions?.FirstOrDefault();
            if (latest == null || CurrentVersion == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(latest.FileId) && !string.IsNullOrWhiteSpace(CurrentVersion.FileId))
            {
                return !string.Equals(latest.FileId, CurrentVersion.FileId, System.StringComparison.OrdinalIgnoreCase);
            }

            return latest.Name != CurrentVersion.Name;
        }
        
        public bool IsUpdateAvailable => 
            Versions != null && Versions.Count > 0 && 
            CurrentVersion != null && CurrentVersion.Name != "-" && 
            HasDifferentLatestFileId();
    }
}
