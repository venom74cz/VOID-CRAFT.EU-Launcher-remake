using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using VoidCraftLauncher.Models;

namespace VoidCraftLauncher.ViewModels;

public partial class MainViewModel
{
    private sealed record AchievementRule(
        string Id,
        string CategoryId,
        string TitleKey,
        string DescriptionKey,
        string Icon,
        Func<AchievementPlayerStats, double> CurrentValue,
        double TargetValue,
        Func<AchievementPlayerStats, bool>? UnlockPredicate = null);

    private static readonly AchievementRule[] AchievementRules =
    [
        new("first-quest", "progress", "Achievements.Rule.FirstQuest.Title", "Achievements.Rule.FirstQuest.Description", "Q1", player => player.CompletedQuests, 1),
        new("quarter", "progress", "Achievements.Rule.Quarter.Title", "Achievements.Rule.Quarter.Description", "25%", player => player.QuestProgress, 25),
        new("halfway", "progress", "Achievements.Rule.Halfway.Title", "Achievements.Rule.Halfway.Description", "50%", player => player.QuestProgress, 50),
        new("high-tier", "progress", "Achievements.Rule.HighTier.Title", "Achievements.Rule.HighTier.Description", "75%", player => player.QuestProgress, 75),
        new("completionist", "progress", "Achievements.Rule.Completionist.Title", "Achievements.Rule.Completionist.Description", "100%", player => player.QuestProgress, 100),
        new("podium", "progress", "Achievements.Rule.Podium.Title", "Achievements.Rule.Podium.Description", "TOP", player => player.Rank <= 3 ? 3 - (player.Rank - 1) : 0, 3, player => player.Rank > 0 && player.Rank <= 3),
        new("teamplay", "social", "Achievements.Rule.Teamplay.Title", "Achievements.Rule.Teamplay.Description", "TEAM", player => string.IsNullOrWhiteSpace(player.TeamName) ? 0 : 1, 1, player => !string.IsNullOrWhiteSpace(player.TeamName))
    ];

    public ObservableCollection<AchievementCategoryGroup> FilteredAchievementGroups { get; } = new();

    public ObservableCollection<AchievementLeaderboardEntry> AchievementLeaderboard { get; } = new();

    public ObservableCollection<SelectionOption> AchievementFilterOptions { get; } = new();

    [ObservableProperty]
    private SelectionOption? _selectedAchievementFilter;

    [ObservableProperty]
    private bool _isAchievementLoading;

    [ObservableProperty]
    private AchievementHubSnapshot? _achievementSnapshot;

    [ObservableProperty]
    private AchievementPlayerStats? _achievementCurrentPlayer;

    public bool HasAchievementSnapshot => AchievementSnapshot != null;

    public bool HasAchievementPlayer => AchievementCurrentPlayer != null;

    public bool HasAchievementGroups => FilteredAchievementGroups.Count > 0;

    public bool HasAchievementLeaderboard => AchievementLeaderboard.Count > 0;

    public string AchievementHeaderTitle => L("Achievements.Header.Title");

    public string AchievementFilterLabel => L("Achievements.Header.Filter");

    public string AchievementCompletionLabel => LF("Achievements.Header.Completed", UnlockedAchievementCount, TotalAchievementCount);

    public string AchievementSourceTitle => L("Achievements.Source.Title");

    public string AchievementSourceBody => L("Achievements.Source.Body");

    public string AchievementSourceMeta => AchievementSnapshot == null
        ? L("Achievements.NoData.Subtitle")
        : LF("Achievements.Source.Meta", AchievementSnapshot.SeasonName, AchievementSnapshot.FetchedAtUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm", CultureInfo.CurrentCulture));

    public string AchievementRefreshLabel => L("Achievements.Button.Refresh");

    public string AchievementSummaryPlayerTitle => L("Achievements.Summary.Player");

    public string AchievementSummaryRankTitle => L("Achievements.Summary.Rank");

    public string AchievementSummaryProgressTitle => L("Achievements.Summary.Progress");

    public string AchievementSummaryTeamTitle => L("Achievements.Summary.Team");

    public string AchievementSummaryPlayerName => AchievementCurrentPlayer?.Name ?? L("Common.Unknown");

    public string AchievementSummaryPlayerMeta => AchievementCurrentPlayer == null
        ? string.Empty
        : LF("Achievements.Summary.CompletedQuests", AchievementCurrentPlayer.CompletedQuests, AchievementCurrentPlayer.TotalQuests);

    public string AchievementSummaryRankValue => AchievementCurrentPlayer == null
        ? L("Achievements.Summary.NotRanked")
        : $"#{AchievementCurrentPlayer.Rank}";

    public string AchievementSummaryProgressValue => AchievementCurrentPlayer == null
        ? "0%"
        : $"{AchievementCurrentPlayer.QuestProgress:0.##}%";

    public string AchievementSummaryTeamValue => string.IsNullOrWhiteSpace(AchievementCurrentPlayer?.TeamName)
        ? L("Achievements.Summary.NoTeam")
        : AchievementCurrentPlayer.TeamName!;

    public string AchievementSummaryPlaytime => AchievementCurrentPlayer?.Playtime is { Length: > 0 } playtime
        ? LF("Achievements.Summary.Playtime", playtime)
        : string.Empty;

    public string AchievementLeaderboardTitle => L("Achievements.Leaderboard.Title");

    public string AchievementLeaderboardSubtitle => L("Achievements.Leaderboard.Subtitle");

    public string AchievementEmptyTitle => HasAchievementSnapshot
        ? L("Achievements.Empty.Title")
        : L("Achievements.NoData.Title");

    public string AchievementEmptySubtitle => HasAchievementSnapshot
        ? L("Achievements.Empty.Subtitle")
        : L("Achievements.NoData.Subtitle");

    public int UnlockedAchievementCount => FilteredAchievementGroups.SelectMany(group => group.Items).Count(card => card.IsUnlocked);

    public int TotalAchievementCount => FilteredAchievementGroups.SelectMany(group => group.Items).Count();

    private void InitializeAchievementSurface()
    {
        RebuildAchievementFilterOptions();
        SyncSelectedAchievementFilter();
        _ = LoadAchievementSnapshotAsync();
    }

    partial void OnSelectedAchievementFilterChanged(SelectionOption? value)
    {
        if (value == null)
            return;

        RebuildAchievementSurface();
    }

    private void RebuildAchievementFilterOptions()
    {
        var selectedFilterId = SelectedAchievementFilter?.Id ?? "all";
        AchievementFilterOptions.Clear();
        AchievementFilterOptions.Add(new SelectionOption { Id = "all", Label = L("Achievements.Filter.All") });
        AchievementFilterOptions.Add(new SelectionOption { Id = "progress", Label = L("Achievements.Filter.Progress") });
        AchievementFilterOptions.Add(new SelectionOption { Id = "mastery", Label = L("Achievements.Filter.Mastery") });
        AchievementFilterOptions.Add(new SelectionOption { Id = "social", Label = L("Achievements.Filter.Social") });
        SelectedAchievementFilter = AchievementFilterOptions.FirstOrDefault(option => option.Id == selectedFilterId)
            ?? AchievementFilterOptions.First();
    }

    private void SyncSelectedAchievementFilter()
    {
        SelectedAchievementFilter = AchievementFilterOptions.FirstOrDefault(option => option.Id == "all")
            ?? AchievementFilterOptions.FirstOrDefault();
    }

    private void RebuildAchievementSurface()
    {
        var cards = BuildAchievementCards();
        var filterId = SelectedAchievementFilter?.Id ?? "all";
        var filteredCards = filterId == "all"
            ? cards
            : cards.Where(card => string.Equals(card.CategoryId, filterId, StringComparison.OrdinalIgnoreCase)).ToList();

        var categoryLookup = new Dictionary<string, string>
        {
            ["progress"] = L("Achievements.Category.Progress"),
            ["mastery"] = L("Achievements.Category.Mastery"),
            ["social"] = L("Achievements.Category.Social")
        };

        FilteredAchievementGroups.Clear();
        foreach (var group in filteredCards.GroupBy(card => card.CategoryId))
        {
            var category = new AchievementCategoryGroup
            {
                Id = group.Key,
                Title = categoryLookup.TryGetValue(group.Key, out var title) ? title : group.Key,
                Items = new ObservableCollection<AchievementBadgeCard>(group)
            };

            FilteredAchievementGroups.Add(category);
        }

        OnPropertyChanged(nameof(HasAchievementGroups));
        OnPropertyChanged(nameof(UnlockedAchievementCount));
        OnPropertyChanged(nameof(TotalAchievementCount));
        OnPropertyChanged(nameof(AchievementCompletionLabel));
        OnPropertyChanged(nameof(AchievementSourceMeta));
        NotifyAchievementSummaryStateChanged();
    }

    private List<AchievementBadgeCard> BuildAchievementCards()
    {
        if (AchievementCurrentPlayer == null)
            return new List<AchievementBadgeCard>();

        var cards = AchievementRules.Select(rule => BuildAchievementCard(rule, AchievementCurrentPlayer)).ToList();
        cards.AddRange(BuildVoidiumRankCards(AchievementCurrentPlayer));
        return cards;
    }

    private IEnumerable<AchievementBadgeCard> BuildVoidiumRankCards(AchievementPlayerStats player)
    {
        if (AchievementSnapshot?.VoidiumRanks.Count > 0 != true)
            return Array.Empty<AchievementBadgeCard>();

        return AchievementSnapshot.VoidiumRanks
            .OrderBy(rank => rank.Hours)
            .ThenBy(rank => rank.Title, StringComparer.CurrentCultureIgnoreCase)
            .Select(rank => BuildVoidiumRankCard(rank, player))
            .ToList();
    }

    private AchievementBadgeCard BuildVoidiumRankCard(VoidiumRankDefinition rank, AchievementPlayerStats player)
    {
        var playtimeHours = player.PlaytimeHours > 0 ? player.PlaytimeHours : GetPlaytimeHours(player);
        var hoursRequired = Math.Max(1, rank.Hours);
        var hoursProgress = Math.Min(playtimeHours, hoursRequired);
        var conditionMetCount = rank.Conditions.Count(condition => GetVoidiumConditionProgress(player, condition) >= condition.Count);
        var conditionTotal = rank.Conditions.Count;
        var hoursRatio = Math.Clamp(hoursProgress / hoursRequired, 0, 1);
        var conditionRatio = conditionTotal == 0 ? 1 : (double)conditionMetCount / conditionTotal;
        var progressRatio = conditionTotal == 0 ? hoursRatio : Math.Min(hoursRatio, conditionRatio);
        var isUnlocked = player.UnlockedVoidiumRankIds.Contains(rank.Id, StringComparer.OrdinalIgnoreCase)
            || (playtimeHours >= rank.Hours && conditionMetCount >= conditionTotal);
        var description = conditionTotal == 0
            ? LF("Achievements.Rule.VoidiumRank.DescriptionHours", FormatHourValue(rank.Hours))
            : LF("Achievements.Rule.VoidiumRank.DescriptionComposite", FormatHourValue(rank.Hours), FormatVoidiumConditionSummary(rank));
        var progressLabel = conditionTotal == 0
            ? LF("Achievements.Progress.Hours", FormatHourValue(hoursProgress), FormatHourValue(rank.Hours))
            : LF("Achievements.Progress.VoidiumComposite", FormatHourValue(hoursProgress), FormatHourValue(rank.Hours), conditionMetCount, conditionTotal);

        var card = new AchievementBadgeCard
        {
            Id = rank.Id,
            CategoryId = "mastery",
            Title = rank.Title,
            Description = description,
            Icon = rank.Hours > 0 ? $"{FormatHourValue(rank.Hours)}h" : "VR",
            IsUnlocked = isUnlocked,
            ProgressValue = progressRatio * 100,
            ProgressMaximum = 100,
            ProgressLabel = progressLabel,
            StatusLabel = isUnlocked ? L("Achievements.Status.Unlocked") : L("Achievements.Status.Locked")
        };

        if (isUnlocked)
        {
            card.CardBorderBrush = "#4FAE87";
            card.CardBackgroundBrush = "#14231E";
            card.IconBackgroundBrush = "#1D332B";
            card.StatusBackgroundBrush = "#183128";
            card.StatusForegroundBrush = "#7CE0B0";
            card.AccentBrush = "#62D69A";
        }
        else if (progressRatio > 0)
        {
            card.CardBorderBrush = "#3C5378";
            card.CardBackgroundBrush = "#141D2A";
            card.IconBackgroundBrush = "#192538";
            card.StatusBackgroundBrush = "#17263B";
            card.StatusForegroundBrush = "#95C3FF";
            card.AccentBrush = "#5FA8FF";
        }

        return card;
    }

    private AchievementBadgeCard BuildAchievementCard(AchievementRule rule, AchievementPlayerStats player)
    {
        var currentValue = Math.Max(0, rule.CurrentValue(player));
        var progressMaximum = Math.Max(1, rule.TargetValue);
        var isUnlocked = rule.UnlockPredicate?.Invoke(player) ?? currentValue >= rule.TargetValue;
        var clampedProgress = Math.Min(progressMaximum, currentValue);
        var isPercentRule = rule.TargetValue > 1 && rule.TargetValue <= 100 && !rule.Id.Equals("first-quest", StringComparison.OrdinalIgnoreCase);
        var isRankRule = rule.Id.Equals("podium", StringComparison.OrdinalIgnoreCase);
        var isHoursRule = rule.Id.StartsWith("voidium-", StringComparison.OrdinalIgnoreCase);
        var progressLabel = rule.Id switch
        {
            "podium" => LF("Achievements.Progress.Rank", player.Rank, (int)rule.TargetValue),
            "online-now" => LF("Achievements.Progress.Online", player.IsOnline ? L("Achievements.Progress.Online.Yes") : L("Achievements.Progress.Online.No")),
            "teamplay" => LF("Achievements.Progress.Team", string.IsNullOrWhiteSpace(player.TeamName) ? L("Achievements.Summary.NoTeam") : player.TeamName),
            _ when isHoursRule => LF("Achievements.Progress.Hours", FormatHourValue(Math.Min(currentValue, rule.TargetValue)), FormatHourValue(rule.TargetValue)),
            _ when isPercentRule => LF("Achievements.Progress.Percent", Math.Round(Math.Min(player.QuestProgress, rule.TargetValue), 0), rule.TargetValue),
            _ => LF("Achievements.Progress.Quests", player.CompletedQuests, player.TotalQuests)
        };

        var card = new AchievementBadgeCard
        {
            Id = rule.Id,
            CategoryId = rule.CategoryId,
            Title = L(rule.TitleKey),
            Description = L(rule.DescriptionKey),
            Icon = rule.Icon,
            IsUnlocked = isUnlocked,
            ProgressValue = clampedProgress,
            ProgressMaximum = progressMaximum,
            ProgressLabel = progressLabel,
            StatusLabel = isUnlocked ? L("Achievements.Status.Unlocked") : L("Achievements.Status.Locked")
        };

        if (isUnlocked)
        {
            card.CardBorderBrush = "#4FAE87";
            card.CardBackgroundBrush = "#14231E";
            card.IconBackgroundBrush = "#1D332B";
            card.StatusBackgroundBrush = "#183128";
            card.StatusForegroundBrush = "#7CE0B0";
            card.AccentBrush = "#62D69A";
        }
        else if (clampedProgress > 0)
        {
            card.CardBorderBrush = "#3C5378";
            card.CardBackgroundBrush = "#141D2A";
            card.IconBackgroundBrush = "#192538";
            card.StatusBackgroundBrush = "#17263B";
            card.StatusForegroundBrush = "#95C3FF";
            card.AccentBrush = "#5FA8FF";
        }

        return card;
    }

    [RelayCommand]
    private async Task RefreshAchievementSnapshot()
    {
        await LoadAchievementSnapshotAsync(true);
    }

    private async Task LoadAchievementSnapshotAsync(bool forceRefresh = false)
    {
        if (IsAchievementLoading)
            return;

        IsAchievementLoading = true;
        try
        {
            var snapshot = await _achievementHubService.GetSnapshotAsync(forceRefresh);
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                AchievementSnapshot = snapshot;
                AchievementCurrentPlayer = MatchAchievementPlayer(snapshot);

                AchievementLeaderboard.Clear();
                foreach (var entry in BuildLeaderboardEntries(snapshot))
                {
                    AchievementLeaderboard.Add(entry);
                }

                OnPropertyChanged(nameof(HasAchievementSnapshot));
                OnPropertyChanged(nameof(HasAchievementPlayer));
                OnPropertyChanged(nameof(HasAchievementLeaderboard));
                RebuildAchievementSurface();
            });
        }
        finally
        {
            IsAchievementLoading = false;
        }
    }

    private AchievementPlayerStats? MatchAchievementPlayer(AchievementHubSnapshot? snapshot)
    {
        if (snapshot?.Players.Count > 0 != true)
            return null;

        var uuid = ActiveAccount?.Uuid ?? UserSession?.UUID;
        if (!string.IsNullOrWhiteSpace(uuid))
        {
            var byUuid = snapshot.Players.FirstOrDefault(player =>
                !string.IsNullOrWhiteSpace(player.Uuid) &&
                string.Equals(player.Uuid, uuid, StringComparison.OrdinalIgnoreCase));

            if (byUuid != null)
                return byUuid;
        }

        var candidateNames = new[]
        {
            ActiveAccount?.DisplayName,
            UserSession?.Username,
            OfflineUsername
        }
        .Where(name => !string.IsNullOrWhiteSpace(name))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

        return snapshot.Players.FirstOrDefault(player =>
            candidateNames.Any(name => string.Equals(name, player.Name, StringComparison.OrdinalIgnoreCase)));
    }

    private void NotifyAchievementSummaryStateChanged()
    {
        OnPropertyChanged(nameof(HasAchievementSnapshot));
        OnPropertyChanged(nameof(HasAchievementPlayer));
        OnPropertyChanged(nameof(HasAchievementLeaderboard));
        OnPropertyChanged(nameof(AchievementHeaderTitle));
        OnPropertyChanged(nameof(AchievementFilterLabel));
        OnPropertyChanged(nameof(AchievementCompletionLabel));
        OnPropertyChanged(nameof(AchievementSourceTitle));
        OnPropertyChanged(nameof(AchievementSourceBody));
        OnPropertyChanged(nameof(AchievementSourceMeta));
        OnPropertyChanged(nameof(AchievementRefreshLabel));
        OnPropertyChanged(nameof(AchievementSummaryPlayerTitle));
        OnPropertyChanged(nameof(AchievementSummaryRankTitle));
        OnPropertyChanged(nameof(AchievementSummaryProgressTitle));
        OnPropertyChanged(nameof(AchievementSummaryTeamTitle));
        OnPropertyChanged(nameof(AchievementSummaryPlayerName));
        OnPropertyChanged(nameof(AchievementSummaryPlayerMeta));
        OnPropertyChanged(nameof(AchievementSummaryRankValue));
        OnPropertyChanged(nameof(AchievementSummaryProgressValue));
        OnPropertyChanged(nameof(AchievementSummaryTeamValue));
        OnPropertyChanged(nameof(AchievementSummaryPlaytime));
        OnPropertyChanged(nameof(AchievementLeaderboardTitle));
        OnPropertyChanged(nameof(AchievementLeaderboardSubtitle));
        OnPropertyChanged(nameof(AchievementEmptyTitle));
        OnPropertyChanged(nameof(AchievementEmptySubtitle));
    }

    private static double GetPlaytimeHours(AchievementPlayerStats player)
    {
        return player.PlaytimeHours > 0 ? player.PlaytimeHours : ParsePlaytimeHours(player.Playtime);
    }

    private int GetVoidiumConditionProgress(AchievementPlayerStats player, VoidiumRankCondition condition)
    {
        if (player.VoidiumProgress.TryGetValue(condition.NormalizedType, out var exactMatch))
            return exactMatch;

        if (player.VoidiumProgress.TryGetValue(condition.Type, out var rawMatch))
            return rawMatch;

        return 0;
    }

    private string FormatVoidiumConditionSummary(VoidiumRankDefinition rank)
    {
        var parts = rank.Conditions
            .Select(FormatVoidiumConditionLabel)
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToList();

        return string.Join(", ", parts);
    }

    private string FormatVoidiumConditionLabel(VoidiumRankCondition condition)
    {
        var label = condition.NormalizedType.ToUpperInvariant() switch
        {
            "KILL" => LF("Achievements.Condition.KILL", condition.Count),
            "VISIT" => LF("Achievements.Condition.VISIT", condition.Count),
            "BREAK" => LF("Achievements.Condition.BREAK", condition.Count),
            "PLACE" => LF("Achievements.Condition.PLACE", condition.Count),
            _ => LF("Achievements.Condition.Generic", condition.Count, condition.NormalizedType)
        };

        return string.IsNullOrWhiteSpace(condition.Target)
            ? label
            : $"{label} ({condition.Target})";
    }

    private static double ParsePlaytimeHours(string? playtime)
    {
        if (string.IsNullOrWhiteSpace(playtime))
        {
            return 0;
        }

        var parts = playtime.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        double hours = 0;
        foreach (var part in parts)
        {
            if (part.EndsWith("h", StringComparison.OrdinalIgnoreCase) && double.TryParse(part[..^1], NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedHours))
            {
                hours += parsedHours;
            }
            else if (part.EndsWith("m", StringComparison.OrdinalIgnoreCase) && double.TryParse(part[..^1], NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedMinutes))
            {
                hours += parsedMinutes / 60d;
            }
        }

        return hours;
    }

    private static string FormatHourValue(double value)
    {
        return Math.Abs(value - Math.Round(value)) < 0.05
            ? Math.Round(value).ToString("0", CultureInfo.CurrentCulture)
            : value.ToString("0.#", CultureInfo.CurrentCulture);
    }

    private List<AchievementLeaderboardEntry> BuildLeaderboardEntries(AchievementHubSnapshot? snapshot)
    {
        if (snapshot?.Players.Count > 0 != true)
        {
            return new List<AchievementLeaderboardEntry>();
        }

        return snapshot.Players
            .GroupBy(player => string.IsNullOrWhiteSpace(player.TeamName) ? $"solo:{player.Name}" : $"team:{player.TeamName}", StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var members = group.OrderBy(player => player.Rank).ThenBy(player => player.Name, StringComparer.OrdinalIgnoreCase).ToList();
                var leader = members.First();
                var isTeamEntry = !string.IsNullOrWhiteSpace(leader.TeamName) && members.Count > 1;
                var playtime = members
                    .Select(member => ParsePlaytimeHours(member.Playtime))
                    .Sum();

                return new AchievementLeaderboardEntry
                {
                    Rank = leader.Rank,
                    PrimaryLabel = isTeamEntry ? leader.TeamName! : leader.Name,
                    SecondaryLabel = isTeamEntry
                        ? string.Join(", ", members.Select(member => member.Name))
                        : (string.IsNullOrWhiteSpace(leader.TeamName) ? (leader.Playtime ?? string.Empty) : leader.TeamName!),
                    QuestProgress = leader.QuestProgress,
                    BadgeLabel = isTeamEntry
                        ? LF("Achievements.Leaderboard.TeamBadge", members.Count)
                        : string.IsNullOrWhiteSpace(leader.Playtime) ? string.Empty : leader.Playtime!,
                    CompletedLabel = $"{leader.CompletedQuests} q",
                    IsTeamEntry = isTeamEntry
                };
            })
            .OrderBy(entry => entry.Rank)
            .ThenByDescending(entry => entry.QuestProgress)
            .Take(5)
            .ToList();
    }
}