using System;
using System.Collections.Generic;

namespace VoidCraftLauncher.Models;

public sealed class AchievementHubSnapshot
{
    public int SeasonId { get; set; }

    public string SeasonName { get; set; } = string.Empty;

    public int TotalQuests { get; set; }

    public DateTime FetchedAtUtc { get; set; }

    public List<AchievementPlayerStats> Players { get; set; } = new();

    public List<VoidiumRankDefinition> VoidiumRanks { get; set; } = new();
}

public sealed class AchievementPlayerStats
{
    public string Name { get; set; } = string.Empty;

    public string? Uuid { get; set; }

    public string? Playtime { get; set; }

    public double QuestProgress { get; set; }

    public int CompletedQuests { get; set; }

    public int TotalQuests { get; set; }

    public string? CompletedDate { get; set; }

    public string? TeamName { get; set; }
    public string? TeamId { get; set; }
    public DateTime? LastSeen { get; set; }

    public bool IsOnline { get; set; }

    public int Rank { get; set; }

    public double PlaytimeHours { get; set; }

    public Dictionary<string, int> VoidiumProgress { get; set; } = new();

    public List<string> UnlockedVoidiumRankIds { get; set; } = new();

    public string? HighestUnlockedVoidiumRankId { get; set; }

    public string? HighestUnlockedVoidiumRankTitle { get; set; }

    public string? NextVoidiumRankId { get; set; }

    public string? NextVoidiumRankTitle { get; set; }
}

public sealed class VoidiumRankDefinition
{
    public string Id { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public double Hours { get; set; }

    public List<VoidiumRankCondition> Conditions { get; set; } = new();
}

public sealed class VoidiumRankCondition
{
    public string Type { get; set; } = string.Empty;

    public string NormalizedType { get; set; } = string.Empty;

    public string? Target { get; set; }

    public int Count { get; set; }
}