using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using VoidCraftLauncher.Models;
using VoidCraftLauncher.Models.CreatorStudio;

namespace VoidCraftLauncher.Services.CreatorStudio;

public sealed class CreatorWorkspaceService
{
    private static readonly string[] StandardWorkspaceFolders =
    {
        "config",
        "defaultconfigs",
        "scripts",
        "kubejs",
        "docs",
        "notes",
        "exports",
        "qa",
        "quests"
    };

    private readonly LauncherService _launcherService;

    public CreatorWorkspaceService(LauncherService launcherService)
    {
        _launcherService = launcherService;
    }

    public IReadOnlyList<string> GetStandardWorkspaceFolders() => StandardWorkspaceFolders;

    public CreatorWorkspaceContext LoadContext(
        string? workspaceId,
        string? workspacePath,
        ModpackInfo? modpack,
        ModpackManifestInfo? manifest,
        CreatorShellState shellState,
        CreatorWorkbenchFile? selectedFile,
        IReadOnlyCollection<CreatorWorkbenchFile> workbenchFiles,
        int linkedServerCount,
        IReadOnlyList<CreatorRecentWorkspace>? recentWorkspaces,
        DateTimeOffset? lastActivityUtc,
        string? lastActivitySummary)
    {
        var normalizedPath = string.IsNullOrWhiteSpace(workspacePath) ? string.Empty : workspacePath;
        var manifestPath = string.IsNullOrWhiteSpace(normalizedPath)
            ? CreatorWorkspaceContext.CreatorManifestFileName
            : Path.Combine(normalizedPath, CreatorWorkspaceContext.CreatorManifestFileName);

        var existingFolders = new List<string>();
        var missingFolders = new List<string>();

        if (!string.IsNullOrWhiteSpace(normalizedPath) && Directory.Exists(normalizedPath))
        {
            foreach (var folder in StandardWorkspaceFolders)
            {
                var fullPath = Path.Combine(normalizedPath, folder);
                if (Directory.Exists(fullPath))
                {
                    existingFolders.Add(folder);
                }
                else
                {
                    missingFolders.Add(folder);
                }
            }
        }
        else
        {
            missingFolders.AddRange(StandardWorkspaceFolders);
        }

        var gitStatus = TryReadGitStatus(normalizedPath);
        var notesPath = string.IsNullOrWhiteSpace(normalizedPath)
            ? string.Empty
            : Path.Combine(normalizedPath, "notes");
        var exportsPath = string.IsNullOrWhiteSpace(normalizedPath)
            ? string.Empty
            : Path.Combine(normalizedPath, "exports");

        return new CreatorWorkspaceContext
        {
            WorkspaceId = workspaceId ?? string.Empty,
            WorkspaceLabel = ResolveWorkspaceLabel(modpack, workspaceId),
            WorkspacePath = normalizedPath,
            CreatorManifestPath = manifestPath,
            HasCreatorManifest = !string.IsNullOrWhiteSpace(normalizedPath) && File.Exists(manifestPath),
            MinecraftVersion = ResolveMinecraftVersion(manifest, modpack),
            LoaderLabel = BuildLoaderLabel(manifest, modpack),
            ModpackVersion = ResolvePackVersion(manifest, modpack),
            ModCount = manifest?.ModCount ?? 0,
            LinkedServersLabel = linkedServerCount == 0 ? "Zadne navazane servery" : $"{linkedServerCount} navazanych serveru",
            EditableFileCount = workbenchFiles.Count,
            NotesPath = notesPath,
            HasNotesWorkspace = !string.IsNullOrWhiteSpace(notesPath) && Directory.Exists(notesPath),
            HasGitRepository = gitStatus.HasRepository,
            GitBranchName = gitStatus.BranchName,
            HasDirtyWorkingTree = gitStatus.HasDirtyWorkingTree,
            SnapshotCount = GetSnapshotCount(workspaceId),
            ExportArtifactCount = GetExportArtifactCount(exportsPath),
            ActiveScope = BuildScope(selectedFile, workbenchFiles.Count),
            ShellState = shellState,
            LastActivityUtc = lastActivityUtc,
            LastActivitySummary = string.IsNullOrWhiteSpace(lastActivitySummary) ? "Bez zaznamenane aktivity." : lastActivitySummary,
            RecentWorkspaces = recentWorkspaces?.ToList() ?? new List<CreatorRecentWorkspace>(),
            ExistingStandardFolders = existingFolders,
            MissingStandardFolders = missingFolders
        };
    }

    private static CreatorScope BuildScope(CreatorWorkbenchFile? selectedFile, int workbenchFileCount)
    {
        if (selectedFile != null)
        {
            return new CreatorScope
            {
                Kind = CreatorScopeKind.SingleFile,
                Label = selectedFile.RelativePath,
                RelativePath = selectedFile.RelativePath,
                ItemCount = 1
            };
        }

        return new CreatorScope
        {
            Kind = CreatorScopeKind.Workspace,
            Label = "Workspace",
            ItemCount = workbenchFileCount
        };
    }

    private int GetSnapshotCount(string? workspaceId)
    {
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            return 0;
        }

        var backupRoot = Path.Combine(_launcherService.BackupsPath, SanitizeWorkspaceDirectoryName(workspaceId));
        if (!Directory.Exists(backupRoot))
        {
            return 0;
        }

        try
        {
            return Directory.EnumerateDirectories(backupRoot).Count();
        }
        catch
        {
            return 0;
        }
    }

    private static int GetExportArtifactCount(string exportsPath)
    {
        if (string.IsNullOrWhiteSpace(exportsPath) || !Directory.Exists(exportsPath))
        {
            return 0;
        }

        try
        {
            return Directory.EnumerateFiles(exportsPath).Count();
        }
        catch
        {
            return 0;
        }
    }

    private static string BuildLoaderLabel(ModpackManifestInfo? manifest, ModpackInfo? modpack)
    {
        if (!string.IsNullOrWhiteSpace(manifest?.ModLoaderId))
        {
            return manifest.ModLoaderId;
        }

        if (modpack?.IsCustomProfile == true && !string.IsNullOrWhiteSpace(modpack.CustomModLoader))
        {
            return modpack.CustomModLoaderVersion is { Length: > 0 }
                ? $"{modpack.CustomModLoader} {modpack.CustomModLoaderVersion}"
                : modpack.CustomModLoader;
        }

        return "Nezjisteno";
    }

    private static string ResolveMinecraftVersion(ModpackManifestInfo? manifest, ModpackInfo? modpack)
    {
        if (!string.IsNullOrWhiteSpace(manifest?.MinecraftVersion))
        {
            return manifest.MinecraftVersion;
        }

        if (!string.IsNullOrWhiteSpace(modpack?.CustomMcVersion))
        {
            return modpack.CustomMcVersion;
        }

        return "Nezjisteno";
    }

    private static string ResolvePackVersion(ModpackManifestInfo? manifest, ModpackInfo? modpack)
    {
        if (!string.IsNullOrWhiteSpace(modpack?.CurrentVersion?.Name))
        {
            return modpack.CurrentVersion.Name;
        }

        if (!string.IsNullOrWhiteSpace(manifest?.Version))
        {
            return manifest.Version;
        }

        return "Nezjisteno";
    }

    private static string ResolveWorkspaceLabel(ModpackInfo? modpack, string? workspaceId)
    {
        if (!string.IsNullOrWhiteSpace(modpack?.DisplayLabel))
        {
            return modpack.DisplayLabel;
        }

        return workspaceId ?? "Bez zvolene instance";
    }

    private static (bool HasRepository, string BranchName, bool? HasDirtyWorkingTree) TryReadGitStatus(string workspacePath)
    {
        if (string.IsNullOrWhiteSpace(workspacePath) || !Directory.Exists(workspacePath))
        {
            return (false, "Git neni inicializovan", null);
        }

        if (!Directory.Exists(Path.Combine(workspacePath, ".git")))
        {
            return (false, "Git neni inicializovan", null);
        }

        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"-C \"{workspacePath}\" status --porcelain=1 --branch",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(3000);

            if (process.ExitCode != 0)
            {
                return (true, "Git repository", null);
            }

            var lines = output
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .ToArray();

            var branchLine = lines.FirstOrDefault(line => line.StartsWith("## ", StringComparison.Ordinal)) ?? "## Git repository";
            var branchName = branchLine[3..].Trim();
            var isDirty = lines.Any(line => !line.StartsWith("## ", StringComparison.Ordinal));
            return (true, branchName, isDirty);
        }
        catch
        {
            return (true, "Git repository", null);
        }
    }

    private static string SanitizeWorkspaceDirectoryName(string workspaceId)
    {
        var source = string.IsNullOrWhiteSpace(workspaceId) ? "voidcraft-instance" : workspaceId.Trim();
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitizedChars = source
            .Select(character => invalidChars.Contains(character) ? '-' : character)
            .ToArray();

        var sanitized = new string(sanitizedChars)
            .Replace(' ', '-')
            .Trim('.', ' ', '-', '_');

        return string.IsNullOrWhiteSpace(sanitized) ? "voidcraft-instance" : sanitized;
    }
}