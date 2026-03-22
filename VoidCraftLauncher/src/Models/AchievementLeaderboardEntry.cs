namespace VoidCraftLauncher.Models;

public sealed class AchievementLeaderboardEntry
{
    public int Rank { get; set; }

    public string PrimaryLabel { get; set; } = string.Empty;

    public string SecondaryLabel { get; set; } = string.Empty;

    public double QuestProgress { get; set; }

    public string BadgeLabel { get; set; } = string.Empty;

    public string CompletedLabel { get; set; } = string.Empty;

    public bool IsTeamEntry { get; set; }
}