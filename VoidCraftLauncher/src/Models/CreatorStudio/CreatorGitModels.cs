using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace VoidCraftLauncher.Models.CreatorStudio;

public enum CreatorGitFileStatus
{
    Modified,
    Added,
    Deleted,
    Renamed,
    Untracked,
    Conflict
}

public sealed class CreatorGitChange : ObservableObject
{
    public string FilePath { get; set; } = string.Empty;

    public CreatorGitFileStatus Status { get; set; } = CreatorGitFileStatus.Modified;

    private bool _isStaged;
    public bool IsStaged
    {
        get => _isStaged;
        set => SetProperty(ref _isStaged, value);
    }

    public string StatusMarker => Status switch
    {
        CreatorGitFileStatus.Modified => "M",
        CreatorGitFileStatus.Added => "A",
        CreatorGitFileStatus.Deleted => "D",
        CreatorGitFileStatus.Renamed => "R",
        CreatorGitFileStatus.Untracked => "?",
        CreatorGitFileStatus.Conflict => "!",
        _ => "?"
    };

    public string StatusLabel => Status switch
    {
        CreatorGitFileStatus.Modified => "Upraven",
        CreatorGitFileStatus.Added => "Novy",
        CreatorGitFileStatus.Deleted => "Smazan",
        CreatorGitFileStatus.Renamed => "Prejmenovan",
        CreatorGitFileStatus.Untracked => "Nesledovany",
        CreatorGitFileStatus.Conflict => "Konflikt",
        _ => "Neznamy"
    };
}

public sealed class CreatorGitCommit
{
    public string Hash { get; set; } = string.Empty;

    public string ShortHash => Hash.Length > 7 ? Hash[..7] : Hash;

    public string Message { get; set; } = string.Empty;

    public string Author { get; set; } = string.Empty;

    public DateTimeOffset TimestampUtc { get; set; }

    public string TimeAgoLabel
    {
        get
        {
            var elapsed = DateTimeOffset.UtcNow - TimestampUtc;
            if (elapsed.TotalMinutes < 60) return $"{(int)elapsed.TotalMinutes}m ago";
            if (elapsed.TotalHours < 24) return $"{(int)elapsed.TotalHours}h ago";
            return $"{(int)elapsed.TotalDays}d ago";
        }
    }
}

public sealed class CreatorGitStatus
{
    public bool IsRepository { get; set; }

    public string BranchName { get; set; } = string.Empty;

    public string RemoteUrl { get; set; } = string.Empty;

    public int AheadCount { get; set; }

    public int BehindCount { get; set; }

    public List<CreatorGitChange> Changes { get; set; } = new();

    public List<CreatorGitCommit> RecentCommits { get; set; } = new();

    public string BranchSummary => string.IsNullOrWhiteSpace(BranchName)
        ? "Bez branch"
        : AheadCount > 0 || BehindCount > 0
            ? $"{BranchName} ↑{AheadCount} ↓{BehindCount}"
            : BranchName;

    public string LastCommitLabel => RecentCommits.Count > 0
        ? $"Last commit: {RecentCommits[0].TimeAgoLabel}"
        : "Zatim zadne commity";

    public bool HasChanges => Changes.Count > 0;

    public bool HasRemote => !string.IsNullOrWhiteSpace(RemoteUrl);
}
