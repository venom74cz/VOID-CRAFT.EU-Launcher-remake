using System;

namespace VoidCraftLauncher.Models;

/// <summary>
/// A normalized social feed item from Discord or YouTube.
/// </summary>
public class FeedItem
{
    public string Id { get; set; } = "";
    public FeedSource Source { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string? AuthorName { get; set; }
    public string? AuthorAvatarUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? ExternalUrl { get; set; }
    public DateTime PublishedAt { get; set; }

    /// <summary>Reaction counts for Discord messages.</summary>
    public int ReactionCount { get; set; }

    /// <summary>View count for YouTube videos.</summary>
    public int ViewCount { get; set; }
}

public enum FeedSource
{
    Discord,
    YouTube,
    Minecraft,
    ServerUpdate
}
