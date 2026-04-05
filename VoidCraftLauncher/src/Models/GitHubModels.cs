using System;

namespace VoidCraftLauncher.Models;

public sealed class GitHubUserProfile
{
    public string Login { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string AvatarUrl { get; set; } = string.Empty;

    public string HtmlUrl { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Login : Name;
}

public sealed class GitHubSession
{
    public string AccessToken { get; set; } = string.Empty;

    public string Scope { get; set; } = string.Empty;

    public GitHubUserProfile? Profile { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public bool IsAuthenticated =>
        !string.IsNullOrWhiteSpace(AccessToken) &&
        !string.IsNullOrWhiteSpace(Profile?.Login);
}

public sealed class GitHubOAuthLoginAttempt
{
    public string LoginId { get; set; } = string.Empty;

    public string PollToken { get; set; } = string.Empty;

    public string AuthorizeUrl { get; set; } = string.Empty;

    public int PollIntervalMs { get; set; } = 2000;

    public DateTimeOffset ExpiresAtUtc { get; set; } = DateTimeOffset.UtcNow.AddMinutes(10);
}

public sealed class GitHubOAuthPollResponse
{
    public string Status { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string Error { get; set; } = string.Empty;

    public string AccessToken { get; set; } = string.Empty;

    public string Scope { get; set; } = string.Empty;

    public GitHubUserProfile? User { get; set; }

    public int PollIntervalMs { get; set; } = 2000;

    public DateTimeOffset? ExpiresAtUtc { get; set; }
}

public sealed class GitHubRepositoryCreationRequest
{
    public string Owner { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Homepage { get; set; } = string.Empty;

    public bool IsPrivate { get; set; }

    public bool AutoInitializeWithReadme { get; set; }
}

public sealed class GitHubRepositoryInfo
{
    public string Owner { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string HtmlUrl { get; set; } = string.Empty;

    public string CloneUrl { get; set; } = string.Empty;

    public string SshUrl { get; set; } = string.Empty;

    public string DefaultBranch { get; set; } = "main";

    public bool IsPrivate { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string FullName => string.IsNullOrWhiteSpace(Owner) ? Name : $"{Owner}/{Name}";

    public string VisibilityLabel => IsPrivate ? "private" : "public";

    public string Summary => string.IsNullOrWhiteSpace(Description)
        ? $"{FullName} • {VisibilityLabel}"
        : $"{FullName} • {VisibilityLabel} • {Description}";
}