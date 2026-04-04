namespace VoidCraftLauncher.Models;

public sealed class ModInstallVersionOption
{
    public string Label { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string FileId { get; set; } = string.Empty;

    public string VersionId { get; set; } = string.Empty;

    public string DownloadUrl { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string IdentityKey => !string.IsNullOrWhiteSpace(FileId)
        ? $"cf:{FileId}"
        : !string.IsNullOrWhiteSpace(VersionId)
            ? $"mr:{VersionId}"
            : Label;
}