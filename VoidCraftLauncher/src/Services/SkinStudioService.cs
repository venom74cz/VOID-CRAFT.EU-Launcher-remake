using HtmlAgilityPack;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VoidCraftLauncher.Models;

namespace VoidCraftLauncher.Services;

public sealed class SkinStudioService
{
    private static readonly Regex SkinPathRegex = new(@"/skin/(?<id>[a-f0-9]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex AnchorRegex = new(@"<a[^>]+href=""(?<href>/skin/[a-f0-9]+)""[^>]*>(?<label>.*?)</a>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex GenericSkinReferenceRegex = new(@"(?:https?:)?//namemc\.com/skin/(?<id>[a-f0-9]+)|/skin/(?<idLocal>[a-f0-9]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private readonly HttpClient _httpClient;
    private readonly ConcurrentDictionary<string, (DateTime CachedAt, IReadOnlyList<SkinHistoryItem> Items)> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeSpan _cacheLifetime = TimeSpan.FromMinutes(15);

    public SkinStudioService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<SkinHistoryItem>> GetSkinHistoryAsync(IEnumerable<string> profileIdentifiers, bool forceRefresh = false)
    {
        foreach (var identifier in profileIdentifiers
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!forceRefresh && _cache.TryGetValue(identifier, out var cached) && DateTime.UtcNow - cached.CachedAt < _cacheLifetime)
            {
                return cached.Items;
            }

            var items = await FetchSkinHistoryAsync(identifier);
            if (items.Count > 0)
            {
                _cache[identifier] = (DateTime.UtcNow, items);
                return items;
            }
        }

        return Array.Empty<SkinHistoryItem>();
    }

    private async Task<IReadOnlyList<SkinHistoryItem>> FetchSkinHistoryAsync(string profileIdentifier)
    {
        var candidateUrls = new[]
        {
            $"https://namemc.com/profile/{Uri.EscapeDataString(profileIdentifier)}.1",
            $"https://namemc.com/profile/{Uri.EscapeDataString(profileIdentifier)}",
            $"https://namemc.com/minecraft-skins/profile/{Uri.EscapeDataString(profileIdentifier)}.1",
            $"https://namemc.com/minecraft-skins/profile/{Uri.EscapeDataString(profileIdentifier)}"
        };

        foreach (var url in candidateUrls)
        {
            try
            {
                var items = await TryParseSkinHistoryAsync(url);
                if (items.Count > 0)
                {
                    return items;
                }
            }
            catch (HttpRequestException)
            {
                // Profile not found or blocked — try next URL
            }
            catch (TaskCanceledException)
            {
                // Request timeout — try next URL
            }
        }

        return Array.Empty<SkinHistoryItem>();
    }

    private async Task<IReadOnlyList<SkinHistoryItem>> TryParseSkinHistoryAsync(string url)
    {
        var html = await GetPageHtmlAsync(url);
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var items = new List<SkinHistoryItem>();

        var document = new HtmlDocument();
        document.LoadHtml(html);

        var anchors = document.DocumentNode.SelectNodes("//a[contains(@href, '/skin/')]");
        if (anchors != null)
        {
            foreach (var anchor in anchors)
            {
                var canvas = anchor.SelectSingleNode(".//canvas[@data-id]");
                var href = anchor.GetAttributeValue("href", string.Empty);
                if (string.IsNullOrWhiteSpace(href) && canvas != null)
                {
                    var canvasSkinId = canvas.GetAttributeValue("data-id", string.Empty);
                    if (!string.IsNullOrWhiteSpace(canvasSkinId))
                    {
                        href = $"/skin/{canvasSkinId}";
                    }
                }

                var label = canvas?.GetAttributeValue("title", string.Empty);
                if (string.IsNullOrWhiteSpace(label))
                {
                    label = HtmlEntity.DeEntitize(anchor.InnerText ?? string.Empty);
                }

                if (!TryAddItem(seenIds, items, href, label))
                {
                    continue;
                }
            }
        }

        if (items.Count == 0)
        {
            foreach (Match match in AnchorRegex.Matches(html))
            {
                if (!match.Success)
                {
                    continue;
                }

                var href = match.Groups["href"].Value;
                var label = HtmlEntity.DeEntitize(Regex.Replace(match.Groups["label"].Value, "<.*?>", string.Empty));
                TryAddItem(seenIds, items, href, label);
            }
        }

        if (items.Count == 0)
        {
            foreach (Match match in GenericSkinReferenceRegex.Matches(html))
            {
                if (!match.Success)
                {
                    continue;
                }

                var skinId = match.Groups["id"].Success
                    ? match.Groups["id"].Value
                    : match.Groups["idLocal"].Value;

                if (string.IsNullOrWhiteSpace(skinId))
                {
                    continue;
                }

                TryAddItem(seenIds, items, $"/skin/{skinId}", $"NameMC snapshot #{items.Count + 1}");
            }
        }

        return items;
    }

    private async Task<string> GetPageHtmlAsync(string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
        request.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");

        using var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    private static bool TryAddItem(HashSet<string> seenIds, List<SkinHistoryItem> items, string href, string? label)
    {
        var match = SkinPathRegex.Match(href ?? string.Empty);
        if (!match.Success)
        {
            return false;
        }

        var skinId = match.Groups["id"].Value;
        if (!seenIds.Add(skinId))
        {
            return false;
        }

        var seenAtLabel = string.IsNullOrWhiteSpace(label)
            ? "NameMC snapshot"
            : Regex.Replace(label, "\\s+", " ").Trim();

        items.Add(new SkinHistoryItem
        {
            SkinId = skinId,
            PreviewUrl = $"https://s.namemc.com/2d/skin/face.png?id={skinId}&scale=4",
            TextureUrl = $"https://s.namemc.com/i/{skinId}.png",
            PageUrl = $"https://namemc.com/skin/{skinId}",
            SeenAtLabel = seenAtLabel,
            IsCurrent = items.Count == 0
        });

        return true;
    }
}