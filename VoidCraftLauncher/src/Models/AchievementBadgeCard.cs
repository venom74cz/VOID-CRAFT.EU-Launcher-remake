using System.Collections.ObjectModel;

namespace VoidCraftLauncher.Models;

public sealed class AchievementBadgeCard
{
    public string Id { get; set; } = string.Empty;

    public string CategoryId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Icon { get; set; } = string.Empty;

    public bool IsUnlocked { get; set; }

    public double ProgressValue { get; set; }

    public double ProgressMaximum { get; set; } = 100;

    public string ProgressLabel { get; set; } = string.Empty;

    public string StatusLabel { get; set; } = string.Empty;

    public string CardBorderBrush { get; set; } = "#2A3347";

    public string CardBackgroundBrush { get; set; } = "#151B26";

    public string IconBackgroundBrush { get; set; } = "#1B2330";

    public string StatusBackgroundBrush { get; set; } = "#182234";

    public string StatusForegroundBrush { get; set; } = "#9DA9BD";

    public string AccentBrush { get; set; } = "#5FA8FF";
}

public sealed class AchievementCategoryGroup
{
    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public ObservableCollection<AchievementBadgeCard> Items { get; set; } = new();
}