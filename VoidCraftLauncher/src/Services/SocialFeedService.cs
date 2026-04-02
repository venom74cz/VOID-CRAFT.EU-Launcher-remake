using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using HtmlAgilityPack;
using VoidCraftLauncher.Models;

namespace VoidCraftLauncher.Services;

/// <summary>
/// Fetches and normalizes social feed content into a unified <see cref="FeedItem"/> list.
/// Prefers the backend-normalized content feed and falls back to legacy Discord/YouTube sources.
/// Includes in-memory cache with configurable TTL.
/// </summary>
public class SocialFeedService
{
    private static readonly Regex UrlRegex = new(@"https?://\S+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private readonly HttpClient _httpClient;
    private readonly LauncherService _launcherService;
    private readonly ObservabilityService _observability;
    private readonly TimeSpan _cacheTtl = TimeSpan.FromMinutes(5);
    private readonly string _cachePath;

    private List<FeedItem>? _cachedFeed;
    private DateTime _lastFetch = DateTime.MinValue;
    private readonly SemaphoreSlim _fetchLock = new(1, 1);

    /// <summary>
    /// Unified backend-normalized content feed endpoint.
    /// Expected JSON: { "items": [{ "id", "source", "title", "body", "authorName", ... }] }
    /// </summary>
    public string? ContentFeedEndpoint { get; set; }

    /// <summary>
    /// Discord announcements API endpoint — expects a backend proxy or public feed endpoint
    /// that returns JSON array of announcement objects.
    /// </summary>
    public string? DiscordFeedEndpoint { get; set; }

    /// <summary>
    /// YouTube channel RSS feed URL.
    /// </summary>
    public string YouTubeFeedUrl { get; set; } = "https://www.youtube.com/feeds/videos.xml?channel_id=UCxxxxxxxxxx";

    /// <summary>
    /// Official Minecraft latest news endpoint returning the normalized feed shape.
    /// </summary>
    public string? OfficialMinecraftFeedEndpoint { get; set; }

    public bool HasCachedFeed => _cachedFeed?.Count > 0;

    public SocialFeedService(HttpClient httpClient, LauncherService launcherService, ObservabilityService observability)
    {
        _httpClient = httpClient;
        _launcherService = launcherService;
        _observability = observability;
        _cachePath = Path.Combine(_launcherService.BasePath, "social_feed_cache.json");
        LoadCacheFromDisk();
    }

    public List<FeedItem> GetCachedFeedSnapshot()
    {
        return _cachedFeed?.Select(CloneFeedItem).ToList() ?? new List<FeedItem>();
    }

    /// <summary>
    /// Returns a merged, de-duplicated, sorted feed from all sources.
    /// Uses cache if still fresh.
    /// </summary>
    public async Task<List<FeedItem>> GetFeedAsync(bool forceRefresh = false)
    {
        if (!forceRefresh && _cachedFeed != null && DateTime.UtcNow - _lastFetch < _cacheTtl)
            return _cachedFeed;

        if (!await _fetchLock.WaitAsync(0))
        {
            // Another fetch is in progress, return stale cache or empty
            return _cachedFeed ?? new List<FeedItem>();
        }

        try
        {
            var tasks = new List<Task<List<FeedItem>>>();

            if (!string.IsNullOrWhiteSpace(ContentFeedEndpoint))
                tasks.Add(FetchSourceWithTimeoutAsync("SocialFeed.Unified", FetchUnifiedFeedSafeAsync, TimeSpan.FromSeconds(5), "cached-feed"));

            if (!string.IsNullOrEmpty(DiscordFeedEndpoint))
                tasks.Add(FetchSourceWithTimeoutAsync("SocialFeed.Discord", FetchDiscordFeedAsync, TimeSpan.FromSeconds(5), "cached-feed"));

            if (!string.IsNullOrEmpty(YouTubeFeedUrl))
                tasks.Add(FetchSourceWithTimeoutAsync("SocialFeed.YouTube", FetchYouTubeFeedAsync, TimeSpan.FromSeconds(5), "cached-feed"));

            if (!string.IsNullOrWhiteSpace(OfficialMinecraftFeedEndpoint))
                tasks.Add(FetchOfficialMinecraftFeedWithFallbackAsync());

            await Task.WhenAll(tasks);

            var merged = MergeFeedItems(tasks.SelectMany(task => task.Result));

            if (merged.Count == 0 && _cachedFeed != null)
            {
                _observability.RecordFallback("SocialFeed.Load", "live-feed", "cached-feed", "All live sources returned an empty feed.");
                return _cachedFeed;
            }

            return CacheFeed(merged);
        }
        catch (Exception ex)
        {
            LogService.Error("SocialFeedService.GetFeedAsync failed", ex);

            if (_cachedFeed != null)
            {
                _observability.RecordFallback("SocialFeed.Load", "live-feed", "cached-feed", ex.Message);
                return _cachedFeed;
            }

            return _cachedFeed ?? new List<FeedItem>();
        }
        finally
        {
            _fetchLock.Release();
        }
    }

    private async Task<List<FeedItem>> FetchUnifiedFeedSafeAsync()
    {
        try
        {
            return await FetchUnifiedFeedAsync();
        }
        catch (Exception ex)
        {
            LogService.Error("Unified content feed fetch failed", ex);
            _observability.RecordFallback("SocialFeed.Load", "backend-normalized-feed", "legacy-feed-sources", ex.Message);
            return new List<FeedItem>();
        }
    }

    private List<FeedItem> CacheFeed(List<FeedItem> items)
    {
        _cachedFeed = items;
        _lastFetch = DateTime.UtcNow;
        PersistCache(items, _lastFetch);
        return items;
    }

    private async Task<List<FeedItem>> FetchSourceWithTimeoutAsync(string operationName, Func<Task<List<FeedItem>>> fetch, TimeSpan timeout, string fallbackName)
    {
        try
        {
            return await fetch().WaitAsync(timeout);
        }
        catch (TimeoutException ex)
        {
            LogService.Error($"{operationName} timed out", ex);
            _observability.RecordFallback(operationName, "live-fetch", fallbackName, $"Timed out after {timeout.TotalSeconds:0.#} s");
            return new List<FeedItem>();
        }
    }

    private async Task<List<FeedItem>> FetchOfficialMinecraftFeedWithFallbackAsync()
    {
        var endpointItems = await FetchSourceWithTimeoutAsync(
            "SocialFeed.MinecraftOfficial.Endpoint",
            FetchOfficialMinecraftFeedAsync,
            TimeSpan.FromSeconds(5),
            "minecraft.net-direct");

        if (endpointItems.Count > 0)
            return endpointItems;

        _observability.RecordFallback("SocialFeed.MinecraftOfficial", "backend-endpoint", "minecraft.net-direct", "Backend endpoint returned no official Minecraft articles.");

        return await FetchSourceWithTimeoutAsync(
            "SocialFeed.MinecraftOfficial.Site",
            FetchOfficialMinecraftSiteFallbackAsync,
            TimeSpan.FromSeconds(5),
            "cached-feed");
    }

    private async Task<List<FeedItem>> FetchUnifiedFeedAsync()
    {
        if (string.IsNullOrWhiteSpace(ContentFeedEndpoint))
            return new List<FeedItem>();

        var json = await _httpClient.GetStringAsync(ContentFeedEndpoint);
        var root = JsonNode.Parse(json);
        var items = root?["items"]?.AsArray() ?? root?.AsArray();
        if (items == null)
            return new List<FeedItem>();

        return items
            .Select(ParseNormalizedFeedItem)
            .Where(item => item != null)
            .Cast<FeedItem>()
            .OrderByDescending(item => item.PublishedAt)
            .ToList();
    }

    /// <summary>
    /// Fetches Discord announcements from the configured backend endpoint.
    /// Expects JSON: [{ "id", "content", "author": { "username", "avatar_url" }, "timestamp", "reactions": int }]
    /// </summary>
    private async Task<List<FeedItem>> FetchDiscordFeedAsync()
    {
        var items = new List<FeedItem>();
        try
        {
            if (string.IsNullOrEmpty(DiscordFeedEndpoint)) return items;

            var json = await _httpClient.GetStringAsync(DiscordFeedEndpoint);
            var array = JsonNode.Parse(json)?.AsArray();
            if (array == null) return items;

            foreach (var node in array)
            {
                if (node == null) continue;
                var content = node["content"]?.ToString() ?? node["body"]?.ToString() ?? "";
                var title = node["title"]?.ToString();
                if (string.IsNullOrWhiteSpace(title))
                {
                    title = content.Length > 80 ? content[..80] + "…" : content;
                }

                var authorNode = node["author"];
                var reactionCount = 0;
                if (node["reactions"] is JsonArray reactions)
                {
                    reactionCount = reactions.Sum(reaction => SafeIntValue(reaction?["count"]));
                }
                else
                {
                    reactionCount = SafeIntValue(node["reactions"]) + SafeIntValue(node["reactionCount"]);
                }

                items.Add(new FeedItem
                {
                    Id = node["id"]?.ToString() ?? Guid.NewGuid().ToString(),
                    Source = FeedSource.Discord,
                    Title = string.IsNullOrWhiteSpace(title) ? "Discord announcement" : title,
                    Body = content,
                    AuthorName = authorNode?["username"]?.ToString()
                                 ?? authorNode?["name"]?.ToString()
                                 ?? authorNode?.ToString()
                                 ?? node["authorName"]?.ToString(),
                    AuthorAvatarUrl = authorNode?["avatar_url"]?.ToString()
                                      ?? authorNode?["avatarUrl"]?.ToString()
                                      ?? node["avatarUrl"]?.ToString()
                                      ?? node["authorAvatarUrl"]?.ToString(),
                    ThumbnailUrl = node["thumbnailUrl"]?.ToString() ?? ExtractAttachmentThumbnail(node["attachments"]),
                    ExternalUrl = node["externalUrl"]?.ToString() ?? ExtractFirstUrl(content),
                    PublishedAt = ParsePublishedAt(node, "timestamp", "publishedAt"),
                    ReactionCount = reactionCount
                });
            }
        }
        catch (Exception ex)
        {
            LogService.Error("Discord feed fetch failed", ex);
        }
        return items;
    }

    /// <summary>
    /// Fetches latest videos from a YouTube channel RSS feed (no API key required).
    /// </summary>
    private async Task<List<FeedItem>> FetchYouTubeFeedAsync()
    {
        var items = new List<FeedItem>();
        try
        {
            var xml = await _httpClient.GetStringAsync(YouTubeFeedUrl);
            var doc = XDocument.Parse(xml);
            XNamespace atom = "http://www.w3.org/2005/Atom";
            XNamespace media = "http://search.yahoo.com/mrss/";

            var entries = doc.Descendants(atom + "entry").Take(10);

            foreach (var entry in entries)
            {
                var videoId = entry.Element(atom + "id")?.Value?.Split(':').LastOrDefault() ?? "";
                var title = entry.Element(atom + "title")?.Value ?? "";
                var published = entry.Element(atom + "published")?.Value;
                var thumbnail = entry.Element(media + "group")?.Element(media + "thumbnail")?.Attribute("url")?.Value;
                var author = entry.Element(atom + "author")?.Element(atom + "name")?.Value;

                items.Add(new FeedItem
                {
                    Id = videoId,
                    Source = FeedSource.YouTube,
                    Title = title,
                    Body = "",
                    AuthorName = author ?? "VOID-CRAFT",
                    ThumbnailUrl = thumbnail,
                    ExternalUrl = $"https://www.youtube.com/watch?v={videoId}",
                    PublishedAt = DateTime.TryParse(published, out var dt) ? dt : DateTime.UtcNow
                });
            }
        }
        catch (Exception ex)
        {
            LogService.Error("YouTube feed fetch failed", ex);
        }
        return items;
    }

    private async Task<List<FeedItem>> FetchOfficialMinecraftFeedAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(OfficialMinecraftFeedEndpoint))
                return new List<FeedItem>();

            var json = await _httpClient.GetStringAsync(OfficialMinecraftFeedEndpoint);
            var root = JsonNode.Parse(json);
            var items = root?["items"]?.AsArray() ?? root?.AsArray();
            if (items == null)
                return new List<FeedItem>();

            return items
                .Select(ParseNormalizedFeedItem)
                .Where(item => item != null)
                .Cast<FeedItem>()
                .OrderByDescending(item => item.PublishedAt)
                .ToList();
        }
        catch (Exception ex)
        {
            LogService.Error("Official Minecraft feed fetch failed", ex);
            return new List<FeedItem>();
        }
    }

    private async Task<List<FeedItem>> FetchOfficialMinecraftSiteFallbackAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://www.minecraft.net/en-us/articles");
        request.Headers.TryAddWithoutValidation("User-Agent", $"VoidCraftLauncher/{typeof(SocialFeedService).Assembly.GetName().Version?.ToString(3) ?? "3.1.7"}");
        request.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        request.Headers.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");

        using var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync();
        var document = new HtmlDocument();
        document.LoadHtml(html);

        var articleAnchors = document.DocumentNode.SelectNodes("//a[contains(@href, '/en-us/article/') or contains(@href, 'https://www.minecraft.net/en-us/article/')]");
        if (articleAnchors == null)
            return new List<FeedItem>();

        var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var items = new List<FeedItem>();

        foreach (var anchor in articleAnchors)
        {
            var title = NormalizeHtmlText(anchor.InnerText);
            if (string.IsNullOrWhiteSpace(title) || LooksLikeCallToAction(title))
                continue;

            var externalUrl = NormalizeMinecraftArticleUrl(anchor.GetAttributeValue("href", string.Empty));
            if (string.IsNullOrWhiteSpace(externalUrl) || !seenUrls.Add(externalUrl))
                continue;

            var id = externalUrl.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
            items.Add(new FeedItem
            {
                Id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString() : id,
                Source = FeedSource.Minecraft,
                Title = title,
                Body = ExtractMinecraftArticleSummary(anchor, title),
                AuthorName = "Minecraft.net",
                ThumbnailUrl = FindNearestImageUrl(anchor),
                ExternalUrl = externalUrl,
                PublishedAt = DateTime.UtcNow
            });

            if (items.Count >= 1)
                break;
        }

        return items;
    }

    private FeedItem? ParseNormalizedFeedItem(JsonNode? node)
    {
        if (node == null)
            return null;

        var body = node["body"]?.ToString() ?? node["content"]?.ToString() ?? string.Empty;
        var title = node["title"]?.ToString();
        if (string.IsNullOrWhiteSpace(title))
        {
            title = body.Length > 80 ? body[..80] + "…" : body;
        }

        var bodyUrls = ExtractFirstUrl(body);
        var thumbnailUrl = node["thumbnailUrl"]?.ToString() ?? ExtractAttachmentThumbnail(node["attachments"]);
        var externalUrl = node["externalUrl"]?.ToString() ?? bodyUrls;

        return new FeedItem
        {
            Id = node["id"]?.ToString() ?? Guid.NewGuid().ToString(),
            Source = ParseFeedSource(node["source"]?.ToString()),
            Title = string.IsNullOrWhiteSpace(title) ? "VOID-CRAFT update" : title,
            Body = body,
            AuthorName = node["authorName"]?.ToString() ?? node["author"]?.ToString(),
            AuthorAvatarUrl = node["authorAvatarUrl"]?.ToString() ?? node["avatarUrl"]?.ToString(),
            ThumbnailUrl = thumbnailUrl,
            ExternalUrl = externalUrl,
            PublishedAt = ParsePublishedAt(node, "publishedAt", "timestamp"),
            ReactionCount = SafeIntValue(node["reactionCount"]),
            ViewCount = SafeIntValue(node["viewCount"])
        };
    }

    private static List<FeedItem> MergeFeedItems(IEnumerable<FeedItem> items)
    {
        return items
            .Where(item => !string.IsNullOrWhiteSpace(item.Title) || !string.IsNullOrWhiteSpace(item.Body) || !string.IsNullOrWhiteSpace(item.ThumbnailUrl))
            .GroupBy(item => $"{item.Source}:{item.Id}", StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var ordered = group.OrderByDescending(FeedRichnessScore).ThenByDescending(item => item.PublishedAt).ToList();
                var preferred = ordered.First();

                foreach (var item in ordered.Skip(1))
                {
                    preferred.Body = SelectPreferred(preferred.Body, item.Body);
                    preferred.AuthorName = SelectPreferred(preferred.AuthorName, item.AuthorName);
                    preferred.AuthorAvatarUrl = SelectPreferred(preferred.AuthorAvatarUrl, item.AuthorAvatarUrl);
                    preferred.ThumbnailUrl = SelectPreferred(preferred.ThumbnailUrl, item.ThumbnailUrl);
                    preferred.ExternalUrl = SelectPreferred(preferred.ExternalUrl, item.ExternalUrl);
                    preferred.ReactionCount = Math.Max(preferred.ReactionCount, item.ReactionCount);
                    preferred.ViewCount = Math.Max(preferred.ViewCount, item.ViewCount);
                }

                return preferred;
            })
            .OrderByDescending(item => item.PublishedAt)
            .ToList();
    }

    private static int FeedRichnessScore(FeedItem item)
    {
        var score = 0;
        if (!string.IsNullOrWhiteSpace(item.Body)) score += 4;
        if (!string.IsNullOrWhiteSpace(item.ThumbnailUrl)) score += 3;
        if (!string.IsNullOrWhiteSpace(item.ExternalUrl)) score += 2;
        if (!string.IsNullOrWhiteSpace(item.AuthorAvatarUrl)) score += 1;
        return score;
    }

    private static string? SelectPreferred(string? current, string? candidate)
    {
        return string.IsNullOrWhiteSpace(current) ? candidate : current;
    }

    private static string? ExtractAttachmentThumbnail(JsonNode? attachmentsNode)
    {
        if (attachmentsNode is not JsonArray attachments)
        {
            return null;
        }

        foreach (var attachment in attachments)
        {
            var contentType = attachment?["contentType"]?.ToString() ?? attachment?["type"]?.ToString();
            if (!string.IsNullOrWhiteSpace(contentType) && !contentType.StartsWith("image", StringComparison.OrdinalIgnoreCase) && !contentType.StartsWith("video", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var url = attachment?["url"]?.ToString();
            if (!string.IsNullOrWhiteSpace(url))
            {
                return url;
            }
        }

        return null;
    }

    private static string? ExtractFirstUrl(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var match = UrlRegex.Match(text);
        return match.Success ? match.Value.TrimEnd(')', '.', ',', ';') : null;
    }

    private static FeedSource ParseFeedSource(string? source)
    {
        return source?.Trim().ToLowerInvariant() switch
        {
            "discord" => FeedSource.Discord,
            "youtube" => FeedSource.YouTube,
            "minecraft" => FeedSource.Minecraft,
            "serverupdate" => FeedSource.ServerUpdate,
            "server_update" => FeedSource.ServerUpdate,
            _ => FeedSource.ServerUpdate
        };
    }

    private static DateTime ParsePublishedAt(JsonNode node, params string[] candidateKeys)
    {
        foreach (var key in candidateKeys)
        {
            if (DateTime.TryParse(node[key]?.ToString(), out var parsed))
                return parsed;
        }

        return DateTime.UtcNow;
    }

    private static int SafeIntValue(JsonNode? node)
    {
        if (node == null)
            return 0;

        try
        {
            return node.GetValue<int>();
        }
        catch
        {
            return int.TryParse(node.ToString(), out var parsed) ? parsed : 0;
        }
    }

    private void LoadCacheFromDisk()
    {
        try
        {
            if (!File.Exists(_cachePath))
                return;

            var json = File.ReadAllText(_cachePath);
            var snapshot = JsonSerializer.Deserialize<SocialFeedCacheSnapshot>(json);
            if (snapshot?.Items == null || snapshot.Items.Count == 0)
                return;

            _cachedFeed = snapshot.Items;
            _lastFetch = snapshot.FetchedAtUtc;
        }
        catch (Exception ex)
        {
            LogService.Error("SocialFeedService failed to load cache", ex);
        }
    }

    private void PersistCache(List<FeedItem> items, DateTime fetchedAtUtc)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_cachePath) ?? _launcherService.BasePath);
            var snapshot = new SocialFeedCacheSnapshot
            {
                FetchedAtUtc = fetchedAtUtc,
                Items = items.Select(CloneFeedItem).ToList()
            };
            File.WriteAllText(_cachePath, JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            LogService.Error("SocialFeedService failed to persist cache", ex);
        }
    }

    private static FeedItem CloneFeedItem(FeedItem item)
    {
        return new FeedItem
        {
            Id = item.Id,
            Source = item.Source,
            Title = item.Title,
            Body = item.Body,
            AuthorName = item.AuthorName,
            AuthorAvatarUrl = item.AuthorAvatarUrl,
            ThumbnailUrl = item.ThumbnailUrl,
            ExternalUrl = item.ExternalUrl,
            PublishedAt = item.PublishedAt,
            ReactionCount = item.ReactionCount,
            ViewCount = item.ViewCount
        };
    }

    private static string NormalizeHtmlText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        return Regex.Replace(HtmlEntity.DeEntitize(text), "\\s+", " ").Trim();
    }

    private static bool LooksLikeCallToAction(string title)
    {
        return title.Equals("Dive in!", StringComparison.OrdinalIgnoreCase)
            || title.Equals("Reveal the name", StringComparison.OrdinalIgnoreCase)
            || title.Equals("Check it out", StringComparison.OrdinalIgnoreCase)
            || title.Equals("Read more", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeMinecraftArticleUrl(string href)
    {
        if (string.IsNullOrWhiteSpace(href))
            return string.Empty;

        return href.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? href
            : $"https://www.minecraft.net{href}";
    }

    private static string ExtractMinecraftArticleSummary(HtmlNode anchor, string title)
    {
        foreach (var node in anchor.AncestorsAndSelf())
        {
            var paragraphs = node.SelectNodes(".//p");
            if (paragraphs == null)
                continue;

            foreach (var paragraph in paragraphs)
            {
                var text = NormalizeHtmlText(paragraph.InnerText);
                if (string.IsNullOrWhiteSpace(text) || string.Equals(text, title, StringComparison.OrdinalIgnoreCase))
                    continue;

                return text;
            }
        }

        return string.Empty;
    }

    private static string? FindNearestImageUrl(HtmlNode anchor)
    {
        foreach (var node in anchor.AncestorsAndSelf())
        {
            var imageNode = node.SelectSingleNode(".//img[@src]");
            if (imageNode == null)
                continue;

            var src = imageNode.GetAttributeValue("src", string.Empty);
            if (!string.IsNullOrWhiteSpace(src))
                return src;
        }

        return null;
    }

    private sealed class SocialFeedCacheSnapshot
    {
        public DateTime FetchedAtUtc { get; set; }

        public List<FeedItem> Items { get; set; } = new();
    }
}
