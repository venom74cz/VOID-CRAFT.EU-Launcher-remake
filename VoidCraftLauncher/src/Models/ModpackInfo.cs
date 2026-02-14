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
        private string _name = "Načítání...";

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

        [ObservableProperty]
        private string _description = "";

        [ObservableProperty]
        private bool _isDeletable = true;

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
