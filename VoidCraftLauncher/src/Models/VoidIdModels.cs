using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Serialization;

namespace VoidCraftLauncher.Models;

internal static class VoidIdModelFormatting
{
    public static string FormatDateTime(DateTimeOffset? value)
    {
        if (!value.HasValue)
        {
            return "-";
        }

        return value.Value.ToLocalTime().ToString("d.M.yyyy HH:mm", CultureInfo.GetCultureInfo("cs-CZ"));
    }
}

public sealed class VoidIdProviderState
{
    [JsonPropertyName("provider")]
    public string Provider { get; set; } = string.Empty;

    [JsonPropertyName("connected")]
    public bool Connected { get; set; }

    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("user_id")]
    public string? UserIdLegacy
    {
        set => UserId = value ?? string.Empty;
    }

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("avatarUrl")]
    public string AvatarUrl { get; set; } = string.Empty;

    [JsonPropertyName("avatar_url")]
    public string? AvatarUrlLegacy
    {
        set => AvatarUrl = value ?? string.Empty;
    }

    [JsonPropertyName("canUnlink")]
    public bool CanUnlink { get; set; }

    [JsonPropertyName("can_unlink")]
    public bool CanUnlinkLegacy
    {
        set => CanUnlink = value;
    }

    [JsonPropertyName("isPrimaryLogin")]
    public bool IsPrimaryLogin { get; set; }

    [JsonPropertyName("is_primary_login")]
    public bool IsPrimaryLoginLegacy
    {
        set => IsPrimaryLogin = value;
    }

    [JsonIgnore]
    public string Summary => Connected
        ? string.IsNullOrWhiteSpace(Username)
            ? $"{Provider} je připojený"
            : $"@{Username}"
        : $"{Provider} není připojený";
}

public sealed class VoidIdSecurityState
{
    [JsonPropertyName("admin_totp_required")]
    public bool AdminTotpRequired { get; set; }

    [JsonPropertyName("adminTotpRequired")]
    public bool AdminTotpRequiredLegacy
    {
        set => AdminTotpRequired = value;
    }

    [JsonPropertyName("admin_totp_enabled")]
    public bool AdminTotpEnabled { get; set; }

    [JsonPropertyName("adminTotpEnabled")]
    public bool AdminTotpEnabledLegacy
    {
        set => AdminTotpEnabled = value;
    }

    [JsonPropertyName("admin_totp_confirmed_at")]
    public DateTimeOffset? AdminTotpConfirmedAtUtc { get; set; }

    [JsonPropertyName("adminTotpConfirmedAt")]
    public DateTimeOffset? AdminTotpConfirmedAtUtcLegacy
    {
        set => AdminTotpConfirmedAtUtc = value;
    }

    [JsonPropertyName("admin_totp_last_verified_at")]
    public DateTimeOffset? AdminTotpLastVerifiedAtUtc { get; set; }

    [JsonPropertyName("adminTotpLastVerifiedAt")]
    public DateTimeOffset? AdminTotpLastVerifiedAtUtcLegacy
    {
        set => AdminTotpLastVerifiedAtUtc = value;
    }

    [JsonIgnore]
    public string Summary => !AdminTotpRequired
        ? "Administrátorské TOTP není vyžadované"
        : AdminTotpEnabled
            ? $"Administrátorské TOTP je aktivní od {VoidIdModelFormatting.FormatDateTime(AdminTotpConfirmedAtUtc)}"
            : "Administrátorské TOTP zatím není aktivní";
}

public sealed class VoidIdAccessState
{
    [JsonPropertyName("isTeamMember")]
    public bool IsTeamMember { get; set; }

    [JsonPropertyName("is_team_member")]
    public bool IsTeamMemberLegacy
    {
        set => IsTeamMember = value;
    }

    [JsonPropertyName("canAccessAdminSurface")]
    public bool CanAccessAdminSurface { get; set; }

    [JsonPropertyName("can_access_admin_surface")]
    public bool CanAccessAdminSurfaceLegacy
    {
        set => CanAccessAdminSurface = value;
    }

    [JsonPropertyName("adminSurfaceSource")]
    public string AdminSurfaceSource { get; set; } = string.Empty;

    [JsonPropertyName("admin_surface_source")]
    public string? AdminSurfaceSourceLegacy
    {
        set => AdminSurfaceSource = value ?? string.Empty;
    }
}

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

    [JsonPropertyName("last_login_at")]
    public DateTimeOffset? LastLoginAtUtc { get; set; }

    [JsonPropertyName("providers")]
    public Dictionary<string, VoidIdProviderState> Providers { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("security")]
    public VoidIdSecurityState Security { get; set; } = new();

    [JsonPropertyName("access")]
    public VoidIdAccessState Access { get; set; } = new();

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

public sealed class VoidIdRefreshSessionInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("userAgent")]
    public string UserAgent { get; set; } = string.Empty;

    [JsonPropertyName("user_agent")]
    public string? UserAgentLegacy
    {
        set => UserAgent = value ?? string.Empty;
    }

    [JsonPropertyName("ipAddress")]
    public string IpAddress { get; set; } = string.Empty;

    [JsonPropertyName("ip_address")]
    public string? IpAddressLegacy
    {
        set => IpAddress = value ?? string.Empty;
    }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset? CreatedAtUtc { get; set; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAtUtcLegacy
    {
        set => CreatedAtUtc = value;
    }

    [JsonPropertyName("lastUsedAt")]
    public DateTimeOffset? LastUsedAtUtc { get; set; }

    [JsonPropertyName("last_used_at")]
    public DateTimeOffset? LastUsedAtUtcLegacy
    {
        set => LastUsedAtUtc = value;
    }

    [JsonPropertyName("expiresAt")]
    public DateTimeOffset? ExpiresAtUtc { get; set; }

    [JsonPropertyName("expires_at")]
    public DateTimeOffset? ExpiresAtUtcLegacy
    {
        set => ExpiresAtUtc = value;
    }

    [JsonPropertyName("isCurrent")]
    public bool IsCurrent { get; set; }

    [JsonPropertyName("is_current")]
    public bool IsCurrentLegacy
    {
        set => IsCurrent = value;
    }

    [JsonIgnore]
    public string DeviceSummary => string.IsNullOrWhiteSpace(UserAgent) ? "Nezname zarizeni" : UserAgent;

    [JsonIgnore]
    public string ActivitySummary => LastUsedAtUtc.HasValue
        ? $"Posledni aktivita {VoidIdModelFormatting.FormatDateTime(LastUsedAtUtc)}"
        : $"Vytvoreno {VoidIdModelFormatting.FormatDateTime(CreatedAtUtc)}";

    [JsonIgnore]
    public string ExpirySummary => ExpiresAtUtc.HasValue
        ? $"Vyprsi {VoidIdModelFormatting.FormatDateTime(ExpiresAtUtc)}"
        : "Bez expirace";
}