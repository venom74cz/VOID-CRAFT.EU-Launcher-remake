using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using VoidCraftLauncher.Models;
using VoidCraftLauncher.Services;

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

    public bool AchievementRequiresVoidIdLogin => !HasVoidIdSession;

    public bool AchievementRequiresMinecraftLink => HasVoidIdSession && CreatorVoidIdSession?.Profile?.HasMinecraftLink != true;

    public bool IsAchievementIdentityGateVisible => !HasAchievementPlayer && (AchievementRequiresVoidIdLogin || AchievementRequiresMinecraftLink);

    public bool HasAchievementGroups => FilteredAchievementGroups.Count > 0;

    public bool HasAchievementLeaderboard => AchievementLeaderboard.Count > 0;

    public string AchievementHeaderTitle => L("Achievements.Header.Title");

    public string AchievementIdentityGateTitle => AchievementRequiresVoidIdLogin
        ? "Přihlas se přes VOID ID"
        : "Chybí navázaný Minecraft účet";

    public string AchievementIdentityGateSubtitle => AchievementRequiresVoidIdLogin
        ? "Osobní achievements, sezónní progress a statistiky jsou nově vázané na tvůj VOID ID účet, ne na lokální launcher profil. Přihlas se a launcher použije propojený Minecraft účet z VOID-CRAFT.EU."
        : "Na tomhle VOID ID účtu zatím není navázaný Minecraft profil pro VOID-CRAFT.EU. Spusť synchronizaci a launcher zkusí převzít vazbu z VOIDIUM linku.";

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
        : $"#{GetTeamRankForPlayer(AchievementCurrentPlayer)}";

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

    public string AchievementEmptyTitle => AchievementRequiresVoidIdLogin
        ? "Přihlas se přes VOID ID"
        : AchievementRequiresMinecraftLink
            ? "Minecraft účet není propojený"
            : HasAchievementSnapshot
                ? "Pro tenhle účet nejsou sezónní data"
                : L("Achievements.NoData.Title");

    public string AchievementEmptySubtitle => AchievementRequiresVoidIdLogin
        ? "Launcher už nebere osobní achievements podle lokálního Minecraft profilu. Přihlas se přes VOID ID a zobrazí se tvoje vlastní sezónní statistiky." 
        : AchievementRequiresMinecraftLink
            ? "VOID ID účet zatím nemá přiřazený Minecraft profil. Použij synchronizaci a potom obnov data achievements." 
            : HasAchievementSnapshot
                ? "V aktuální sezóně zatím nejsou pro navázaný účet dostupné statistiky nebo quest progress."
                : L("Achievements.NoData.Subtitle");

    public int UnlockedAchievementCount => FilteredAchievementGroups.SelectMany(group => group.Items).Count(card => card.IsUnlocked);

    public int TotalAchievementCount => FilteredAchievementGroups.SelectMany(group => group.Items).Count();

    private void InitializeAchievementSurface()
    {
        RebuildAchievementFilterOptions();
        SyncSelectedAchievementFilter();
        // Force a live fetch on initialization to keep launcher in sync with web
        _ = LoadAchievementSnapshotAsync(true);
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
        var progressMaximum = Math.Max(1, rule.TargetValue);
        var isPercentRule = rule.TargetValue > 1 && rule.TargetValue <= 100 && !rule.Id.Equals("first-quest", StringComparison.OrdinalIgnoreCase);
        var isHoursRule = rule.Id.StartsWith("voidium-", StringComparison.OrdinalIgnoreCase);

        double currentValue;
        bool isUnlocked;
        double clampedProgress;
        string progressLabel;

        if (rule.Id.Equals("podium", StringComparison.OrdinalIgnoreCase))
        {
            // Use team-based ranking for podium: teams count as a single position
            var teamRank = GetTeamRankForPlayer(player);
            currentValue = teamRank > 0 && teamRank <= (int)rule.TargetValue ? (rule.TargetValue - (teamRank - 1)) : 0;
            isUnlocked = teamRank > 0 && teamRank <= (int)rule.TargetValue;
            clampedProgress = Math.Min(progressMaximum, currentValue);
            progressLabel = LF("Achievements.Progress.Rank", teamRank > 0 ? teamRank : player.Rank, (int)rule.TargetValue);
        }
        else
        {
            currentValue = Math.Max(0, rule.CurrentValue(player));
            isUnlocked = rule.UnlockPredicate?.Invoke(player) ?? currentValue >= rule.TargetValue;
            clampedProgress = Math.Min(progressMaximum, currentValue);

            if (rule.Id.Equals("online-now", StringComparison.OrdinalIgnoreCase))
            {
                progressLabel = LF("Achievements.Progress.Online", player.IsOnline ? L("Achievements.Progress.Online.Yes") : L("Achievements.Progress.Online.No"));
            }
            else if (rule.Id.Equals("teamplay", StringComparison.OrdinalIgnoreCase))
            {
                progressLabel = LF("Achievements.Progress.Team", string.IsNullOrWhiteSpace(player.TeamName) ? L("Achievements.Summary.NoTeam") : player.TeamName);
            }
            else if (isHoursRule)
            {
                progressLabel = LF("Achievements.Progress.Hours", FormatHourValue(Math.Min(currentValue, rule.TargetValue)), FormatHourValue(rule.TargetValue));
            }
            else if (isPercentRule)
            {
                progressLabel = LF("Achievements.Progress.Percent", Math.Round(Math.Min(player.QuestProgress, rule.TargetValue), 0), rule.TargetValue);
            }
            else
            {
                progressLabel = LF("Achievements.Progress.Quests", player.CompletedQuests, player.TotalQuests);
            }
        }

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

    [RelayCommand]
    private async Task RefreshAchievementIdentity()
    {
        if (!HasVoidIdSession)
        {
            await LoginVoidId();
            return;
        }

        await RefreshAchievementVoidIdProfileAsync(reconcileMinecraft: true);
        await LoadAchievementSnapshotAsync(true);
    }

    private async Task LoadAchievementSnapshotAsync(bool forceRefresh = false)
    {
        if (IsAchievementLoading)
            return;

        IsAchievementLoading = true;
        try
        {
            if (HasVoidIdSession)
            {
                await RefreshAchievementVoidIdProfileAsync(reconcileMinecraft: CreatorVoidIdSession?.Profile?.HasMinecraftLink != true);
            }

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

        if (!HasVoidIdSession)
            return null;

        var linkedProfile = CreatorVoidIdSession?.Profile;
        if (linkedProfile?.HasMinecraftLink != true)
            return null;

        var uuid = linkedProfile.MinecraftUuid;
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
            linkedProfile.MinecraftName,
            linkedProfile.DisplayName
        }
        .Where(name => !string.IsNullOrWhiteSpace(name))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

        return snapshot.Players.FirstOrDefault(player =>
            candidateNames.Any(name => string.Equals(name, player.Name, StringComparison.OrdinalIgnoreCase)));
    }

    private async Task RefreshAchievementVoidIdProfileAsync(bool reconcileMinecraft)
    {
        if (!await EnsureFreshVoidIdSessionAsync())
        {
            return;
        }

        var session = CreatorVoidIdSession;
        var accessToken = session?.AccessToken;
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return;
        }

        try
        {
            var profile = await _voidIdAuthService.RefreshProfileAsync(accessToken, reconcileMinecraft);
            if (profile == null)
            {
                return;
            }

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                CreatorVoidIdSession = new VoidIdSession
                {
                    AccessToken = session?.AccessToken ?? string.Empty,
                    RefreshToken = session?.RefreshToken ?? string.Empty,
                    AccessTokenExpiresAtUtc = session?.AccessTokenExpiresAtUtc,
                    Profile = profile
                };

                VoidIdLoginStatus = $"VOID ID aktivní: {profile.DisplayName}";
            });
        }
        catch (Exception ex)
        {
            LogService.Error("Achievement VOID ID profile refresh failed", ex);
        }
    }

    private void HandleAchievementIdentityChanged()
    {
        AchievementCurrentPlayer = MatchAchievementPlayer(AchievementSnapshot);
        RebuildAchievementSurface();
        NotifyAchievementSummaryStateChanged();
    }

    private void NotifyAchievementSummaryStateChanged()
    {
        OnPropertyChanged(nameof(HasAchievementSnapshot));
        OnPropertyChanged(nameof(HasAchievementPlayer));
        OnPropertyChanged(nameof(AchievementRequiresVoidIdLogin));
        OnPropertyChanged(nameof(AchievementRequiresMinecraftLink));
        OnPropertyChanged(nameof(IsAchievementIdentityGateVisible));
        OnPropertyChanged(nameof(AchievementIdentityGateTitle));
        OnPropertyChanged(nameof(AchievementIdentityGateSubtitle));
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

        // Group players from the snapshot without filtering by last-seen/online status.
        // Prefer `TeamId` for grouping when present, then `TeamName`, otherwise fall back to the player's name.
        var grouped = snapshot.Players
            .GroupBy(player => !string.IsNullOrWhiteSpace(player.TeamId)
                                ? $"teamid:{player.TeamId}"
                                : !string.IsNullOrWhiteSpace(player.TeamName)
                                    ? $"team:{player.TeamName}"
                                    : $"solo:{player.Name}", StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var members = group.OrderBy(player => player.Rank).ThenBy(player => player.Name, StringComparer.OrdinalIgnoreCase).ToList();
                var leader = members.First();
                var isTeamEntry = members.Count > 1;
                var avgQuestProgress = members.Average(m => m.QuestProgress);
                var maxQuestProgress = members.Max(m => m.QuestProgress);

                var displayLabel = !string.IsNullOrWhiteSpace(leader.TeamName)
                    ? leader.TeamName!
                    : !string.IsNullOrWhiteSpace(leader.TeamId)
                        ? leader.TeamId!
                        : leader.Name;

                var entry = new AchievementLeaderboardEntry
                {
                    Rank = 0,
                    PrimaryLabel = isTeamEntry ? displayLabel : leader.Name,
                    SecondaryLabel = isTeamEntry
                        ? string.Join(", ", members.Select(member => member.Name))
                        : (!string.IsNullOrWhiteSpace(leader.TeamName) ? leader.TeamName! : (leader.Playtime ?? string.Empty)),
                    QuestProgress = avgQuestProgress,
                    BadgeLabel = isTeamEntry
                        ? LF("Achievements.Leaderboard.TeamBadge", members.Count)
                        : string.IsNullOrWhiteSpace(leader.Playtime) ? string.Empty : leader.Playtime!,
                    CompletedLabel = $"{leader.CompletedQuests} q",
                    IsTeamEntry = isTeamEntry
                };

                return new { Entry = entry, Avg = avgQuestProgress, Max = maxQuestProgress };
            })
            .OrderByDescending(x => x.Avg)
            .ThenByDescending(x => x.Max)
            .ThenBy(x => x.Entry.PrimaryLabel, StringComparer.CurrentCultureIgnoreCase)
            .Select((x, idx) => { x.Entry.Rank = idx + 1; return x.Entry; })
            .Take(5)
            .ToList();

        return grouped;
    }

    private int GetTeamRankForPlayer(AchievementPlayerStats player)
    {
        if (AchievementSnapshot?.Players == null || AchievementSnapshot.Players.Count == 0)
            return player.Rank;

        var players = AchievementSnapshot.Players.ToList();

        var grouped = players
            .GroupBy(p => !string.IsNullOrWhiteSpace(p.TeamId) ? $"teamid:{p.TeamId}" : !string.IsNullOrWhiteSpace(p.TeamName) ? $"team:{p.TeamName}" : $"solo:{p.Name}", StringComparer.OrdinalIgnoreCase)
            .Select(g => new
            {
                Key = g.Key,
                Members = g.ToList(),
                QuestProgressAvg = g.Average(p => p.QuestProgress),
                QuestProgressMax = g.Max(p => p.QuestProgress)
            })
            .OrderByDescending(t => t.QuestProgressAvg)
            .ThenByDescending(t => t.QuestProgressMax)
            .ToList();

        // Try to find the group which contains this player by UUID or Name first (more robust)
        for (int i = 0; i < grouped.Count; i++)
        {
            var grp = grouped[i];
            if (grp.Members.Any(m => !string.IsNullOrWhiteSpace(player.Uuid) && !string.IsNullOrWhiteSpace(m.Uuid) && string.Equals(m.Uuid, player.Uuid, StringComparison.OrdinalIgnoreCase)))
                return i + 1;
            if (grp.Members.Any(m => string.Equals(m.Name?.Trim(), player.Name?.Trim(), StringComparison.OrdinalIgnoreCase)))
                return i + 1;
        }

        // Fallback: match by team key if available
        var playerKey = !string.IsNullOrWhiteSpace(player.TeamId) ? $"teamid:{player.TeamId}" : !string.IsNullOrWhiteSpace(player.TeamName) ? $"team:{player.TeamName}" : $"solo:{player.Name}";
        var idx = grouped.FindIndex(t => string.Equals(t.Key, playerKey, StringComparison.OrdinalIgnoreCase));
        return idx >= 0 ? idx + 1 : player.Rank;
    }
}