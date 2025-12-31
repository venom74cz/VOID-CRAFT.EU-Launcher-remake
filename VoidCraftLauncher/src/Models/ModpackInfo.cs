namespace VoidCraftLauncher.Models
{
    public class ModpackInfo
    {
        public int ProjectId { get; set; } // CurseForge Project ID
        public string Name { get; set; } = "Načítání...";
        
        // Změna: Ukládáme objekt verze, ne jen string
        public ModpackVersion CurrentVersion { get; set; } = new ModpackVersion { Name = "-" };
        public System.Collections.ObjectModel.ObservableCollection<ModpackVersion> Versions { get; set; } = new();

        public string Author { get; set; } = "";
        public string LogoUrl { get; set; } = "";
        public string ImageUrl => LogoUrl; // Alias for UI binding
        public string Description { get; set; } = "";
        
        public bool IsDeletable { get; set; } = true;
    }

    public class ModpackVersion
    {
        public string Name { get; set; } = "";
        public string FileId { get; set; } = "";
        public string ReleaseDate { get; set; } = "";
        
        public override string ToString() => Name;
    }
}
