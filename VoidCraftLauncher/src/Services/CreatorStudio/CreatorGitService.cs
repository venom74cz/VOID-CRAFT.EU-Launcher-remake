using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VoidCraftLauncher.Models.CreatorStudio;

namespace VoidCraftLauncher.Services.CreatorStudio;

public sealed class CreatorGitService
{
    private static readonly string[] EmptyArgs = Array.Empty<string>();
    private static readonly TimeSpan DefaultGitTimeout = TimeSpan.FromMinutes(2);

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

    public async Task<CreatorGitStatus> GetStatusAsync(string workspacePath, IReadOnlyCollection<string>? scopedPaths = null)
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
            var branchResult = await RunGitAsync(workspacePath, "rev-parse", "--abbrev-ref", "HEAD");
            status.BranchName = branchResult.ExitCode == 0 ? branchResult.Output.Trim() : "detached";

            var remoteResult = await RunGitAsync(workspacePath, "remote", "get-url", "origin");
            if (remoteResult.ExitCode == 0)
                status.RemoteUrl = remoteResult.Output.Trim();

            if (status.HasRemote)
            {
                var aheadBehind = await RunGitAsync(workspacePath, "rev-list", "--left-right", "--count", $"HEAD...origin/{status.BranchName}");
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

            var statusArgs = new List<string> { "status", "--porcelain=v1" };
            if (scopedPaths != null && scopedPaths.Count > 0)
            {
                statusArgs.Add("--");
                statusArgs.AddRange(scopedPaths.Where(path => !string.IsNullOrWhiteSpace(path)));
            }

            var statusOptions = new GitCommandOptions();
            statusOptions.ConfigArguments.Add("-c");
            statusOptions.ConfigArguments.Add("core.quotePath=false");

            var statusResult = await RunGitAsync(workspacePath, statusOptions, statusArgs.ToArray());
            if (statusResult.ExitCode == 0)
            {
                foreach (var line in statusResult.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (line.Length < 4) continue;
                    var idx = line[0];
                    var wt = line[1];
                    var filePath = line[3..].Trim();

                    if ((idx == 'R' || wt == 'R') && filePath.Contains(" -> ", StringComparison.Ordinal))
                    {
                        filePath = filePath[(filePath.LastIndexOf(" -> ", StringComparison.Ordinal) + 4)..].Trim();
                    }

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

            var logResult = await RunGitAsync(workspacePath, "log", "--oneline", "--format=%H|%s|%an|%aI", "-20");
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
        var result = await RunGitAsync(workspacePath, "init");
        return result.ExitCode == 0;
    }

    public async Task<bool> StageFileAsync(string workspacePath, string filePath)
    {
        var result = await RunGitAsync(workspacePath, "add", "--", filePath);
        return result.ExitCode == 0;
    }

    public async Task<bool> UnstageFileAsync(string workspacePath, string filePath)
    {
        var result = await RunGitAsync(workspacePath, "reset", "HEAD", "--", filePath);
        return result.ExitCode == 0;
    }

    public async Task<bool> StageAllAsync(string workspacePath)
    {
        var result = await RunGitAsync(workspacePath, "add", "-A");
        return result.ExitCode == 0;
    }

    public async Task<IReadOnlyList<string>> GetTrackedFilesAsync(string workspacePath)
    {
        var result = await RunGitAsync(workspacePath, "ls-files", "-z", "--full-name");
        if (result.ExitCode != 0)
        {
            return Array.Empty<string>();
        }

        return result.Output
            .Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Select(path => path.Trim().Replace('\\', '/'))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<bool> StagePathsAsync(string workspacePath, IReadOnlyCollection<string> relativePaths)
    {
        if (relativePaths == null || relativePaths.Count == 0)
        {
            return true;
        }

        var normalizedPaths = relativePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path.Replace('\\', '/').Trim().TrimStart('/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedPaths.Count == 0)
        {
            return true;
        }

        foreach (var normalizedPath in normalizedPaths)
        {
            var result = await RunGitAsync(workspacePath, "add", "-A", "--", normalizedPath);
            if (result.ExitCode != 0)
            {
                LogService.Error($"Git stage failed for '{normalizedPath}': {result.Output}");
                return false;
            }
        }

        return true;
    }

    public async Task<bool> CommitAsync(string workspacePath, string message, IReadOnlyCollection<string>? scopedPaths = null)
    {
        if (string.IsNullOrWhiteSpace(message)) return false;

        var normalizedPaths = scopedPaths?
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path.Replace('\\', '/').Trim().TrimStart('/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var result = normalizedPaths is { Count: > 0 }
            ? await CommitScopedAsync(workspacePath, message, normalizedPaths)
            : await RunGitAsync(workspacePath, "commit", "-m", message);

        if (result.ExitCode != 0)
        {
            LogService.Error($"Git commit failed: {result.Output}");
        }

        return result.ExitCode == 0;
    }

    public async Task<bool> PullAsync(string workspacePath, string? githubAccessToken = null)
    {
        var result = await RunGitWithGitHubAuthAsync(workspacePath, githubAccessToken, "pull", "--rebase");
        return result.ExitCode == 0;
    }

    public async Task<bool> PushAsync(string workspacePath, string? githubAccessToken = null)
    {
        var result = await RunGitWithGitHubAuthAsync(workspacePath, githubAccessToken, "push");
        if (result.ExitCode == 0)
        {
            return true;
        }

        var branch = await RunGitAsync(workspacePath, "rev-parse", "--abbrev-ref", "HEAD");
        var branchName = branch.ExitCode == 0 ? branch.Output.Trim() : string.Empty;
        if (string.IsNullOrWhiteSpace(branchName) || string.Equals(branchName, "HEAD", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var upstreamResult = await RunGitWithGitHubAuthAsync(workspacePath, githubAccessToken, "push", "-u", "origin", branchName);
        return upstreamResult.ExitCode == 0;
    }

    public async Task<bool> CreateOrUpdateTagAsync(string workspacePath, string tagName, string message)
    {
        if (string.IsNullOrWhiteSpace(tagName)) return false;

        await RunGitAsync(workspacePath, "tag", "-d", tagName);
        var result = string.IsNullOrWhiteSpace(message)
            ? await RunGitAsync(workspacePath, "tag", tagName)
            : await RunGitAsync(workspacePath, "tag", "-a", tagName, "-m", message);
        return result.ExitCode == 0;
    }

    public async Task<bool> PushTagAsync(string workspacePath, string tagName, string? githubAccessToken = null)
    {
        if (string.IsNullOrWhiteSpace(tagName)) return false;

        await RunGitWithGitHubAuthAsync(workspacePath, githubAccessToken, "push", "origin", ":refs/tags/" + tagName);
        var result = await RunGitWithGitHubAuthAsync(workspacePath, githubAccessToken, "push", "origin", tagName);
        return result.ExitCode == 0;
    }

    public async Task<bool> SetRemoteOriginAsync(string workspacePath, string remoteUrl)
    {
        if (string.IsNullOrWhiteSpace(workspacePath) || string.IsNullOrWhiteSpace(remoteUrl))
        {
            return false;
        }

        var existing = await RunGitAsync(workspacePath, "remote", "get-url", "origin");
        var result = existing.ExitCode == 0
            ? await RunGitAsync(workspacePath, "remote", "set-url", "origin", remoteUrl.Trim())
            : await RunGitAsync(workspacePath, "remote", "add", "origin", remoteUrl.Trim());

        return result.ExitCode == 0;
    }

    public async Task<string> GetDiffAsync(string workspacePath, string filePath)
    {
        var result = await RunGitAsync(workspacePath, "diff", "--", filePath);
        return result.ExitCode == 0 ? result.Output : string.Empty;
    }

    public async Task<bool> RevertFileAsync(string workspacePath, string filePath)
    {
        var result = await RunGitAsync(workspacePath, "checkout", "--", filePath);
        return result.ExitCode == 0;
    }

    public async Task<List<string>> GetBranchesAsync(string workspacePath)
    {
        var result = await RunGitAsync(workspacePath, "branch", "--list", "--format=%(refname:short)");
        if (result.ExitCode != 0) return new List<string>();

        return result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(b => b.Trim())
            .Where(b => !string.IsNullOrWhiteSpace(b))
            .ToList();
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
        var result = await RunGitAsync(workspacePath, "checkout", branchName);
        return result.ExitCode == 0;
    }

    public async Task<bool> CreateBranchAsync(string workspacePath, string branchName)
    {
        var result = await RunGitAsync(workspacePath, "checkout", "-b", branchName);
        return result.ExitCode == 0;
    }

    private static async Task<(int ExitCode, string Output)> RunGitWithGitHubAuthAsync(string workDir, string? githubAccessToken, params string[] args)
    {
        var remoteUrl = await GetOriginRemoteUrlAsync(workDir);
        var options = BuildGitHubCommandOptions(remoteUrl, githubAccessToken);
        return await RunGitAsync(workDir, options, args);
    }

    private static (int ExitCode, string Output) RunGitWithGitHubAuth(string workDir, string? githubAccessToken, params string[] args)
    {
        var remoteUrl = GetOriginRemoteUrl(workDir);
        var options = BuildGitHubCommandOptions(remoteUrl, githubAccessToken);
        return RunGit(workDir, options, args);
    }

    private static (int ExitCode, string Output) RunGit(string workDir, params string[] args)
    {
        return RunGit(workDir, null, args);
    }

    private static (int ExitCode, string Output) RunGit(string workDir, GitCommandOptions? options, params string[] args)
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
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            psi.Environment["GIT_TERMINAL_PROMPT"] = "0";

            if (options != null)
            {
                foreach (var entry in options.EnvironmentVariables)
                {
                    psi.Environment[entry.Key] = entry.Value;
                }

                foreach (var configArg in options.ConfigArguments)
                {
                    psi.ArgumentList.Add(configArg);
                }
            }

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

    private static async Task<(int ExitCode, string Output)> RunGitAsync(string workDir, params string[] args)
    {
        return await RunGitAsync(workDir, null, args);
    }

    private static async Task<(int ExitCode, string Output)> RunGitAsync(string workDir, GitCommandOptions? options, params string[] args)
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
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            psi.Environment["GIT_TERMINAL_PROMPT"] = "0";

            if (options != null)
            {
                foreach (var entry in options.EnvironmentVariables)
                {
                    psi.Environment[entry.Key] = entry.Value;
                }

                foreach (var configArg in options.ConfigArguments)
                {
                    psi.ArgumentList.Add(configArg);
                }
            }

            foreach (var arg in args)
            {
                psi.ArgumentList.Add(arg);
            }

            using var process = Process.Start(psi);
            if (process == null)
            {
                return (-1, string.Empty);
            }

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            try
            {
                await process.WaitForExitAsync(new CancellationTokenSource(DefaultGitTimeout).Token);
            }
            catch (OperationCanceledException)
            {
                TryKillProcess(process);
                return (-1, $"Git command timed out after {DefaultGitTimeout.TotalSeconds:0} seconds.");
            }

            var output = await outputTask;
            var error = await errorTask;
            return (process.ExitCode, string.IsNullOrWhiteSpace(output) ? error : output);
        }
        catch (Exception ex)
        {
            return (-1, ex.Message);
        }
    }

    private static async Task<(int ExitCode, string Output)> CommitScopedAsync(string workspacePath, string message, IReadOnlyCollection<string> scopedPaths)
    {
        var pathspecFilePath = Path.Combine(Path.GetTempPath(), $"voidcraft-git-pathspec-{Guid.NewGuid():N}.txt");

        try
        {
            await WritePathspecFileAsync(pathspecFilePath, scopedPaths);
            return await RunGitAsync(
                workspacePath,
                "commit",
                "-m",
                message,
                $"--pathspec-from-file={pathspecFilePath}",
                "--pathspec-file-nul");
        }
        finally
        {
            try
            {
                if (File.Exists(pathspecFilePath))
                {
                    File.Delete(pathspecFilePath);
                }
            }
            catch
            {
            }
        }
    }

    private static async Task WritePathspecFileAsync(string pathspecFilePath, IReadOnlyCollection<string> scopedPaths)
    {
        await using var stream = new FileStream(pathspecFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
        foreach (var scopedPath in scopedPaths)
        {
            var pathBytes = Encoding.UTF8.GetBytes(scopedPath);
            await stream.WriteAsync(pathBytes);
            stream.WriteByte(0);
        }
    }

    private static string GetOriginRemoteUrl(string workspacePath)
    {
        var result = RunGit(workspacePath, "remote", "get-url", "origin");
        return result.ExitCode == 0 ? result.Output.Trim() : string.Empty;
    }

    private static async Task<string> GetOriginRemoteUrlAsync(string workspacePath)
    {
        var result = await RunGitAsync(workspacePath, "remote", "get-url", "origin");
        return result.ExitCode == 0 ? result.Output.Trim() : string.Empty;
    }

    private static void TryKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }
    }

    private static GitCommandOptions? BuildGitHubCommandOptions(string remoteUrl, string? githubAccessToken)
    {
        if (string.IsNullOrWhiteSpace(remoteUrl) || string.IsNullOrWhiteSpace(githubAccessToken))
        {
            return null;
        }

        if (!Uri.TryCreate(remoteUrl.Trim(), UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var basicToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($"x-access-token:{githubAccessToken.Trim()}"));
        var options = new GitCommandOptions();
        options.ConfigArguments.Add("-c");
        options.ConfigArguments.Add("credential.helper=");
        options.ConfigArguments.Add("-c");
        options.ConfigArguments.Add("core.askPass=");
        options.ConfigArguments.Add("-c");
        options.ConfigArguments.Add($"http.https://github.com/.extraheader=AUTHORIZATION: basic {basicToken}");
        return options;
    }

    private sealed class GitCommandOptions
    {
        public Dictionary<string, string> EnvironmentVariables { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<string> ConfigArguments { get; } = new();
    }
}
