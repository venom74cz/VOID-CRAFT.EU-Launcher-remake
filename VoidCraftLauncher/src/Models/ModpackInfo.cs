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
        private ModpackVersion _currentVersion = new ModpackVersion { Name = "-" };

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PlayButtonText))]
        [NotifyPropertyChangedFor(nameof(IsUpdateAvailable))]
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
                // If Versions are loaded and Latest != Current -> Update
                if (Versions != null && Versions.Count > 0 && 
                    CurrentVersion != null && CurrentVersion.Name != "-" && 
                    Versions.FirstOrDefault()?.Name != CurrentVersion.Name)
                {
                    return "AKTUALIZOVAT";
                }
                return "HRÁT";
            }
        }
        
        public bool IsUpdateAvailable => 
            Versions != null && Versions.Count > 0 && 
            CurrentVersion != null && CurrentVersion.Name != "-" && 
            Versions.FirstOrDefault()?.Name != CurrentVersion.Name;
    }
}
