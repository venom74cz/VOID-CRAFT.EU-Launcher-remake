using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VoidCraftLauncher.Models.CreatorStudio;

namespace VoidCraftLauncher.Services.CreatorStudio;

public sealed class CreatorGitService
{
    private static readonly string[] EmptyArgs = Array.Empty<string>();

    public bool IsGitAvailable()
    {
        try
        {
            var result = RunGit(".", "--version");
            return result.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task<CreatorGitStatus> GetStatusAsync(string workspacePath)
    {
        var status = new CreatorGitStatus();

        if (string.IsNullOrWhiteSpace(workspacePath) || !Directory.Exists(workspacePath))
            return status;

        var gitDir = Path.Combine(workspacePath, ".git");
        if (!Directory.Exists(gitDir) && !File.Exists(gitDir))
            return status;

        status.IsRepository = true;

        try
        {
            var branchResult = RunGit(workspacePath, "rev-parse", "--abbrev-ref", "HEAD");
            status.BranchName = branchResult.ExitCode == 0 ? branchResult.Output.Trim() : "detached";

            var remoteResult = RunGit(workspacePath, "remote", "get-url", "origin");
            if (remoteResult.ExitCode == 0)
                status.RemoteUrl = remoteResult.Output.Trim();

            if (status.HasRemote)
            {
                var aheadBehind = RunGit(workspacePath, "rev-list", "--left-right", "--count", $"HEAD...origin/{status.BranchName}");
                if (aheadBehind.ExitCode == 0)
                {
                    var parts = aheadBehind.Output.Trim().Split('\t', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        int.TryParse(parts[0], out var ahead);
                        int.TryParse(parts[1], out var behind);
                        status.AheadCount = ahead;
                        status.BehindCount = behind;
                    }
                }
            }

            var statusResult = RunGit(workspacePath, "status", "--porcelain=v1");
            if (statusResult.ExitCode == 0)
            {
                foreach (var line in statusResult.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (line.Length < 4) continue;
                    var idx = line[0];
                    var wt = line[1];
                    var filePath = line[3..].Trim();

                    var change = new CreatorGitChange { FilePath = filePath };

                    if (idx == '?' && wt == '?')
                    {
                        change.Status = CreatorGitFileStatus.Untracked;
                    }
                    else if (idx == 'U' || wt == 'U')
                    {
                        change.Status = CreatorGitFileStatus.Conflict;
                    }
                    else
                    {
                        change.Status = (idx != ' ' ? idx : wt) switch
                        {
                            'M' => CreatorGitFileStatus.Modified,
                            'A' => CreatorGitFileStatus.Added,
                            'D' => CreatorGitFileStatus.Deleted,
                            'R' => CreatorGitFileStatus.Renamed,
                            _ => CreatorGitFileStatus.Modified
                        };
                        change.IsStaged = idx != ' ' && idx != '?';
                    }

                    status.Changes.Add(change);
                }
            }

            var logResult = RunGit(workspacePath, "log", "--oneline", "--format=%H|%s|%an|%aI", "-20");
            if (logResult.ExitCode == 0)
            {
                foreach (var line in logResult.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = line.Split('|', 4);
                    if (parts.Length < 4) continue;

                    status.RecentCommits.Add(new CreatorGitCommit
                    {
                        Hash = parts[0],
                        Message = parts[1],
                        Author = parts[2],
                        TimestampUtc = DateTimeOffset.TryParse(parts[3], out var ts) ? ts : DateTimeOffset.UtcNow
                    });
                }
            }
        }
        catch (Exception ex)
        {
            LogService.Error("Git status failed", ex);
        }

        return status;
    }

    public async Task<bool> InitRepositoryAsync(string workspacePath)
    {
        var result = RunGit(workspacePath, "init");
        return result.ExitCode == 0;
    }

    public async Task<bool> StageFileAsync(string workspacePath, string filePath)
    {
        var result = RunGit(workspacePath, "add", "--", filePath);
        return result.ExitCode == 0;
    }

    public async Task<bool> UnstageFileAsync(string workspacePath, string filePath)
    {
        var result = RunGit(workspacePath, "reset", "HEAD", "--", filePath);
        return result.ExitCode == 0;
    }

    public async Task<bool> StageAllAsync(string workspacePath)
    {
        var result = RunGit(workspacePath, "add", "-A");
        return result.ExitCode == 0;
    }

    public async Task<bool> CommitAsync(string workspacePath, string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return false;
        var result = RunGit(workspacePath, "commit", "-m", message);
        return result.ExitCode == 0;
    }

    public async Task<bool> PullAsync(string workspacePath)
    {
        var result = RunGit(workspacePath, "pull", "--rebase");
        return result.ExitCode == 0;
    }

    public async Task<bool> PushAsync(string workspacePath)
    {
        var result = RunGit(workspacePath, "push");
        return result.ExitCode == 0;
    }

    public async Task<string> GetDiffAsync(string workspacePath, string filePath)
    {
        var result = RunGit(workspacePath, "diff", "--", filePath);
        return result.ExitCode == 0 ? result.Output : string.Empty;
    }

    public async Task<bool> RevertFileAsync(string workspacePath, string filePath)
    {
        var result = RunGit(workspacePath, "checkout", "--", filePath);
        return result.ExitCode == 0;
    }

    public List<string> GetBranches(string workspacePath)
    {
        var result = RunGit(workspacePath, "branch", "--list", "--format=%(refname:short)");
        if (result.ExitCode != 0) return new List<string>();
        return result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(b => b.Trim())
            .Where(b => !string.IsNullOrWhiteSpace(b))
            .ToList();
    }

    public async Task<bool> CheckoutBranchAsync(string workspacePath, string branchName)
    {
        var result = RunGit(workspacePath, "checkout", branchName);
        return result.ExitCode == 0;
    }

    public async Task<bool> CreateBranchAsync(string workspacePath, string branchName)
    {
        var result = RunGit(workspacePath, "checkout", "-b", branchName);
        return result.ExitCode == 0;
    }

    private static (int ExitCode, string Output) RunGit(string workDir, params string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = workDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            foreach (var arg in args)
                psi.ArgumentList.Add(arg);

            using var process = Process.Start(psi);
            if (process == null) return (-1, string.Empty);

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit(10_000);

            return (process.ExitCode, string.IsNullOrWhiteSpace(output) ? error : output);
        }
        catch (Exception ex)
        {
            return (-1, ex.Message);
        }
    }
}
