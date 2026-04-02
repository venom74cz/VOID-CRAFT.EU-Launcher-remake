namespace VoidCraftLauncher.Models;

public sealed class InstanceOverviewDescriptionSection
{
    public string Title { get; set; } = "";

    public string Body { get; set; } = "";

    public bool HasBody => !string.IsNullOrWhiteSpace(Body);
}