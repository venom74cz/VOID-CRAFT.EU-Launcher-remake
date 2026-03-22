using System.Collections.ObjectModel;

namespace VoidCraftLauncher.Models;

public sealed class FutureRoadmapSection
{
    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public string AccentBrush { get; set; } = "#7C6FFF";

    public string SurfaceBrush { get; set; } = "#141420";

    public string BorderBrush { get; set; } = "#2D2D42";

    public string BadgeLabel { get; set; } = "GUIDE";

    public bool HasBody => !string.IsNullOrWhiteSpace(Body);

    public bool HasEntries { get; set; }

    public bool HasEntryCount => Entries.Count > 0;

    public int EntryCount => Entries.Count;

    public string EntryCountLabel => EntryCount == 1 ? "1 smer" : $"{EntryCount} smeru";

    public ObservableCollection<FutureRoadmapEntry> Entries { get; set; } = new();
}

public sealed class FutureRoadmapSignal
{
    public string Label { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string AccentBrush { get; set; } = "#7C6FFF";

    public string SurfaceBrush { get; set; } = "#141420";

    public string BorderBrush { get; set; } = "#3A3A55";
}

public sealed class FutureRoadmapEntry
{
    public string IndexLabel { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Implementation { get; set; } = string.Empty;

    public string WhyItMatters { get; set; } = string.Empty;

    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

    public bool HasIndexLabel => !string.IsNullOrWhiteSpace(IndexLabel);

    public bool HasImplementation => !string.IsNullOrWhiteSpace(Implementation);

    public bool HasWhyItMatters => !string.IsNullOrWhiteSpace(WhyItMatters);

    public string AccentBrush { get; set; } = "#7C6FFF";

    public string SurfaceBrush { get; set; } = "#141420";

    public string BorderBrush { get; set; } = "#2D2D42";
}