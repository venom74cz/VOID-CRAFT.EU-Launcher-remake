using System;

namespace VoidCraftLauncher.Models.CreatorStudio;

public sealed class CreatorActivityEntry
{
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;

    public string Summary { get; set; } = string.Empty;

    public string TabContext { get; set; } = string.Empty;

    public string TimeLabel => TimestampUtc.LocalDateTime.ToString("HH:mm");

    public string DateLabel => TimestampUtc.LocalDateTime.ToString("dd.MM.");

    public string FullLabel => $"{TimeLabel}  {Summary}";
}

public sealed class CreatorQuickLink
{
    public string Label { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public string Kind { get; set; } = "web";

    public bool HasUrl => !string.IsNullOrWhiteSpace(Url);
}
