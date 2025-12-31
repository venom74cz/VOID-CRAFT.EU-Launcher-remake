namespace VoidCraftLauncher.Models;

public class ModpackItem
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Author { get; set; } = "";
    public string IconUrl { get; set; } = "";
    public string Id { get; set; } = ""; // Project ID
    public string Source { get; set; } = ""; // "CurseForge" or "Modrinth"
    public string WebLink { get; set; } = "";
    public long DownloadCount { get; set; }
}
