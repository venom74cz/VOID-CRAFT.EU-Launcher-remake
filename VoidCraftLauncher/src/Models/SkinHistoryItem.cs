namespace VoidCraftLauncher.Models;

public sealed class SkinHistoryItem
{
    public string SkinId { get; set; } = string.Empty;

    public string PreviewUrl { get; set; } = string.Empty;

    public string TextureUrl { get; set; } = string.Empty;

    public string PageUrl { get; set; } = string.Empty;

    public string SeenAtLabel { get; set; } = string.Empty;

    public bool IsCurrent { get; set; }
}