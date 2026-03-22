namespace VoidCraftLauncher.Models;

public sealed class CreatorWorkbenchFile
{
    public string FullPath { get; set; } = string.Empty;

    public string RelativePath { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public string SizeLabel => SizeBytes switch
    {
        < 1024 => $"{SizeBytes} B",
        < 1024 * 1024 => $"{SizeBytes / 1024d:0.#} KB",
        _ => $"{SizeBytes / 1024d / 1024d:0.#} MB"
    };
}