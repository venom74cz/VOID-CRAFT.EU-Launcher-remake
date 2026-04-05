using System;
using System.Text.Json.Serialization;

namespace VoidCraftLauncher.Models;

public sealed class VoidIdProfile
{
    [JsonPropertyName("void_id")]
    public string VoidId { get; set; } = string.Empty;

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("avatar_url")]
    public string AvatarUrl { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; set; } = "player";

    [JsonPropertyName("discord_id")]
    public string DiscordId { get; set; } = string.Empty;

    [JsonPropertyName("minecraft_uuid")]
    public string MinecraftUuid { get; set; } = string.Empty;

    [JsonPropertyName("minecraft_name")]
    public string MinecraftName { get; set; } = string.Empty;

    [JsonIgnore]
    public bool HasMinecraftLink => !string.IsNullOrWhiteSpace(MinecraftUuid) || !string.IsNullOrWhiteSpace(MinecraftName);
}

public sealed class VoidIdSession
{
    public string AccessToken { get; set; } = string.Empty;

    public string RefreshToken { get; set; } = string.Empty;

    public DateTimeOffset? AccessTokenExpiresAtUtc { get; set; }

    public VoidIdProfile? Profile { get; set; }

    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(AccessToken) && Profile != null;
}

public sealed class VoidIdOAuthLoginAttempt
{
    public string LoginId { get; set; } = string.Empty;

    public string PollToken { get; set; } = string.Empty;

    public string AuthorizeUrl { get; set; } = string.Empty;

    public int PollIntervalMs { get; set; } = 2000;

    public DateTimeOffset ExpiresAtUtc { get; set; } = DateTimeOffset.UtcNow.AddMinutes(10);
}

public sealed class VoidIdOAuthPollResponse
{
    public string Status { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string Error { get; set; } = string.Empty;

    public string AccessToken { get; set; } = string.Empty;

    public string RefreshToken { get; set; } = string.Empty;

    public VoidIdProfile? User { get; set; }

    public int PollIntervalMs { get; set; } = 2000;

    public DateTimeOffset? ExpiresAtUtc { get; set; }
}