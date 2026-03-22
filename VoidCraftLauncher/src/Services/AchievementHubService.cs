using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using VoidCraftLauncher.Models;

namespace VoidCraftLauncher.Services;

public sealed class AchievementHubService
{
    private readonly HttpClient _httpClient;
    private readonly LauncherService _launcherService;
    private readonly ObservabilityService _observability;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly TimeSpan _cacheTtl = TimeSpan.FromMinutes(10);
    private readonly string _cachePath;

    private AchievementHubSnapshot? _cachedSnapshot;
    private DateTime _lastFetchUtc = DateTime.MinValue;

    public string CurrentSeasonEndpoint { get; set; } = "https://api.void-craft.eu/api/seasons/current";

    public string SeasonStatsEndpointTemplate { get; set; } = "https://api.void-craft.eu/api/seasons/{0}/stats";

    public string VoidiumRanksEndpointTemplate { get; set; } = "https://api.void-craft.eu/api/voidium/ranks?season={0}";

    public AchievementHubService(HttpClient httpClient, LauncherService launcherService, ObservabilityService observability)
    {
        _httpClient = httpClient;
        _launcherService = launcherService;
        _observability = observability;
        _cachePath = Path.Combine(_launcherService.BasePath, "achievement_hub_cache.json");
    }

    public async Task<AchievementHubSnapshot?> GetSnapshotAsync(bool forceRefresh = false)
    {
        if (!forceRefresh && _cachedSnapshot != null && DateTime.UtcNow - _lastFetchUtc < _cacheTtl)
            return _cachedSnapshot;

        await _refreshLock.WaitAsync();
        try
        {
            if (!forceRefresh && _cachedSnapshot != null && DateTime.UtcNow - _lastFetchUtc < _cacheTtl)
                return _cachedSnapshot;

            try
            {
                var snapshot = await FetchSnapshotAsync();
                CacheSnapshot(snapshot);
                PersistSnapshot(snapshot);
                return snapshot;
            }
            catch (Exception ex)
            {
                LogService.Error("AchievementHubService live fetch failed", ex);
                var cachedSnapshot = LoadSnapshotFromDisk();
                if (cachedSnapshot != null)
                {
                    _observability.RecordFallback("AchievementHub.Load", "live-season-stats", "cached-achievement-snapshot", ex.Message);
                    CacheSnapshot(cachedSnapshot, cachedSnapshot.FetchedAtUtc);
                    return cachedSnapshot;
                }

                return _cachedSnapshot;
            }
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task<AchievementHubSnapshot> FetchSnapshotAsync()
    {
        var seasonJson = await _httpClient.GetStringAsync(CurrentSeasonEndpoint);
        var seasonRoot = JsonNode.Parse(seasonJson)?.AsObject();
        var seasonId = seasonRoot?["id"]?.GetValue<int?>();
        var seasonName = seasonRoot?["name"]?.GetValue<string>();

        if (seasonId == null || seasonId <= 0 || string.IsNullOrWhiteSpace(seasonName))
            throw new InvalidOperationException("Current season endpoint did not return a valid active season.");

        var statsJson = await _httpClient.GetStringAsync(string.Format(SeasonStatsEndpointTemplate, seasonId.Value));
        var statsRoot = JsonNode.Parse(statsJson)?.AsObject();
        var playersArray = statsRoot?["players"]?.AsArray();

        var snapshot = new AchievementHubSnapshot
        {
            SeasonId = seasonId.Value,
            SeasonName = seasonName,
            TotalQuests = statsRoot?["totalQuests"]?.GetValue<int?>() ?? 0,
            FetchedAtUtc = DateTime.UtcNow
        };

        if (playersArray != null)
        {
            snapshot.Players = playersArray
                .Select((node, index) => new AchievementPlayerStats
                {
                    Rank = index + 1,
                    Name = node?["name"]?.GetValue<string>() ?? string.Empty,
                    Uuid = node?["uuid"]?.GetValue<string>(),
                    Playtime = node?["playtime"]?.GetValue<string>(),
                    QuestProgress = node?["questProgress"]?.GetValue<double?>() ?? 0,
                    CompletedQuests = node?["completedQuests"]?.GetValue<int?>() ?? 0,
                    TotalQuests = node?["totalQuests"]?.GetValue<int?>() ?? snapshot.TotalQuests,
                    CompletedDate = node?["completedDate"]?.GetValue<string>(),
                    TeamName = node?["teamName"]?.GetValue<string>(),
                    IsOnline = node?["isOnline"]?.GetValue<bool?>() ?? false
                })
                .Where(player => !string.IsNullOrWhiteSpace(player.Name))
                .ToList();
        }

        await TryAttachVoidiumRankDataAsync(snapshot, seasonId.Value);

        return snapshot;
    }

    private async Task TryAttachVoidiumRankDataAsync(AchievementHubSnapshot snapshot, int seasonId)
    {
        try
        {
            var ranksJson = await _httpClient.GetStringAsync(string.Format(VoidiumRanksEndpointTemplate, seasonId));
            var ranksRoot = JsonNode.Parse(ranksJson)?.AsObject();
            if (ranksRoot == null)
                return;

            if (ranksRoot["ranks"] is JsonArray rankArray)
            {
                snapshot.VoidiumRanks = rankArray
                    .Select(ParseVoidiumRankDefinition)
                    .Where(rank => rank != null)
                    .Cast<VoidiumRankDefinition>()
                    .ToList();
            }

            if (ranksRoot["players"] is not JsonArray playerArray)
                return;

            var rankPlayers = playerArray
                .Select(ParseVoidiumPlayerData)
                .Where(player => player != null)
                .Cast<AchievementPlayerStats>()
                .ToList();

            foreach (var player in snapshot.Players)
            {
                var rankPlayer = FindMatchingRankPlayer(rankPlayers, player);
                if (rankPlayer == null)
                    continue;

                player.PlaytimeHours = rankPlayer.PlaytimeHours;
                player.VoidiumProgress = rankPlayer.VoidiumProgress;
                player.UnlockedVoidiumRankIds = rankPlayer.UnlockedVoidiumRankIds;
                player.HighestUnlockedVoidiumRankId = rankPlayer.HighestUnlockedVoidiumRankId;
                player.HighestUnlockedVoidiumRankTitle = rankPlayer.HighestUnlockedVoidiumRankTitle;
                player.NextVoidiumRankId = rankPlayer.NextVoidiumRankId;
                player.NextVoidiumRankTitle = rankPlayer.NextVoidiumRankTitle;
            }
        }
        catch (Exception ex)
        {
            LogService.Error("AchievementHubService rank fetch failed", ex);
        }
    }

    private static AchievementPlayerStats? FindMatchingRankPlayer(IEnumerable<AchievementPlayerStats> rankPlayers, AchievementPlayerStats player)
    {
        if (!string.IsNullOrWhiteSpace(player.Uuid))
        {
            var byUuid = rankPlayers.FirstOrDefault(candidate =>
                !string.IsNullOrWhiteSpace(candidate.Uuid) &&
                string.Equals(candidate.Uuid, player.Uuid, StringComparison.OrdinalIgnoreCase));

            if (byUuid != null)
                return byUuid;
        }

        return rankPlayers.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, player.Name, StringComparison.OrdinalIgnoreCase));
    }

    private static VoidiumRankDefinition? ParseVoidiumRankDefinition(JsonNode? node)
    {
        if (node is not JsonObject rankObject)
            return null;

        var rank = new VoidiumRankDefinition
        {
            Id = rankObject["id"]?.GetValue<string>() ?? string.Empty,
            Type = rankObject["type"]?.GetValue<string>() ?? string.Empty,
            Value = rankObject["value"]?.GetValue<string>() ?? string.Empty,
            Title = rankObject["title"]?.GetValue<string>() ?? string.Empty,
            Hours = rankObject["hours"]?.GetValue<double?>() ?? 0
        };

        if (rankObject["conditions"] is JsonArray conditionArray)
        {
            rank.Conditions = conditionArray
                .Select(ParseVoidiumRankCondition)
                .Where(condition => condition != null)
                .Cast<VoidiumRankCondition>()
                .ToList();
        }

        return rank;
    }

    private static VoidiumRankCondition? ParseVoidiumRankCondition(JsonNode? node)
    {
        if (node is not JsonObject conditionObject)
            return null;

        return new VoidiumRankCondition
        {
            Type = conditionObject["type"]?.GetValue<string>() ?? string.Empty,
            NormalizedType = conditionObject["normalizedType"]?.GetValue<string>() ?? string.Empty,
            Target = conditionObject["target"]?.GetValue<string?>(),
            Count = conditionObject["count"]?.GetValue<int?>() ?? 0
        };
    }

    private static AchievementPlayerStats? ParseVoidiumPlayerData(JsonNode? node)
    {
        if (node is not JsonObject playerObject)
            return null;

        var player = new AchievementPlayerStats
        {
            Name = playerObject["name"]?.GetValue<string>() ?? string.Empty,
            Uuid = playerObject["uuid"]?.GetValue<string>(),
            PlaytimeHours = playerObject["playtimeHours"]?.GetValue<double?>() ?? 0,
            HighestUnlockedVoidiumRankId = playerObject["highestUnlockedRankId"]?.GetValue<string?>(),
            HighestUnlockedVoidiumRankTitle = playerObject["highestUnlockedRankTitle"]?.GetValue<string?>(),
            NextVoidiumRankId = playerObject["nextRankId"]?.GetValue<string?>(),
            NextVoidiumRankTitle = playerObject["nextRankTitle"]?.GetValue<string?>()
        };

        if (playerObject["progress"] is JsonObject progressObject)
        {
            foreach (var entry in progressObject)
            {
                if (entry.Value == null)
                    continue;

                player.VoidiumProgress[entry.Key] = entry.Value.GetValue<int>();
            }
        }

        if (playerObject["unlockedRankIds"] is JsonArray unlockedArray)
        {
            player.UnlockedVoidiumRankIds = unlockedArray
                .Select(item => item?.GetValue<string>())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Cast<string>()
                .ToList();
        }

        return player;
    }

    private void CacheSnapshot(AchievementHubSnapshot snapshot, DateTime? fetchedAtUtc = null)
    {
        _cachedSnapshot = snapshot;
        _lastFetchUtc = fetchedAtUtc ?? DateTime.UtcNow;
    }

    private void PersistSnapshot(AchievementHubSnapshot snapshot)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_cachePath) ?? _launcherService.BasePath);
            File.WriteAllText(_cachePath, JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            LogService.Error("AchievementHubService failed to persist cache", ex);
        }
    }

    private AchievementHubSnapshot? LoadSnapshotFromDisk()
    {
        try
        {
            if (!File.Exists(_cachePath))
                return null;

            var json = File.ReadAllText(_cachePath);
            return JsonSerializer.Deserialize<AchievementHubSnapshot>(json);
        }
        catch (Exception ex)
        {
            LogService.Error("AchievementHubService failed to load cache", ex);
            return null;
        }
    }
}