using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VoidCraftLauncher.Models;

namespace VoidCraftLauncher.Services;

public sealed class CreatorWorkbenchService
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".json", ".json5", ".cfg", ".toml", ".txt", ".ini", ".properties", ".yaml", ".yml", ".md", ".js", ".zs", ".snbt", ".csv"
    };

    private static readonly HashSet<string> PrioritizedFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "manifest_info.json", "mods_metadata.json", "options.txt", "launcher_config.json"
    };

    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "mods", "resourcepacks", "shaderpacks", "saves", "screenshots", "screenshoty", "logs", "crash-reports", ".git", "bin", "obj"
    };

    private static readonly (string FolderName, string Category)[] WorkbenchDirectories =
    {
        ("config", "Config"),
        ("defaultconfigs", "Default Config"),
        ("scripts", "Scripts"),
        ("kubejs", "KubeJS")
    };

    private const long MaxEditableFileBytes = 256 * 1024;

    public IReadOnlyList<CreatorWorkbenchFile> DiscoverFiles(string instancePath)
    {
        if (string.IsNullOrWhiteSpace(instancePath) || !Directory.Exists(instancePath))
        {
            return Array.Empty<CreatorWorkbenchFile>();
        }

        var files = new List<CreatorWorkbenchFile>();

        void AddCandidate(string fullPath, string category)
        {
            if (!File.Exists(fullPath))
            {
                return;
            }

            var info = new FileInfo(fullPath);
            if (info.Length > MaxEditableFileBytes)
            {
                return;
            }

            var extension = Path.GetExtension(fullPath);
            if (!SupportedExtensions.Contains(extension) && !PrioritizedFileNames.Contains(info.Name))
            {
                return;
            }

            if (files.Any(existing => string.Equals(existing.FullPath, fullPath, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            files.Add(new CreatorWorkbenchFile
            {
                FullPath = fullPath,
                RelativePath = Path.GetRelativePath(instancePath, fullPath),
                Category = category,
                SizeBytes = info.Length
            });
        }

        foreach (var fileName in PrioritizedFileNames)
        {
            AddCandidate(Path.Combine(instancePath, fileName), "Root");
        }

        foreach (var (folderName, category) in WorkbenchDirectories)
        {
            var workbenchPath = Path.Combine(instancePath, folderName);
            if (!Directory.Exists(workbenchPath))
            {
                continue;
            }

            foreach (var file in EnumerateFilesSafe(workbenchPath))
            {
                AddCandidate(file, category);
            }
        }

        foreach (var file in EnumerateFilesSafe(instancePath))
        {
            var relativePath = Path.GetRelativePath(instancePath, file);
            if (WorkbenchDirectories.Any(directory =>
                relativePath.StartsWith(directory.FolderName + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (relativePath.Contains(Path.DirectorySeparatorChar))
            {
                continue;
            }

            AddCandidate(file, "Root");
        }

        return files
            .OrderBy(file => file.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<string> ReadFileAsync(string fullPath)
    {
        using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream, Encoding.UTF8, true);
        return await reader.ReadToEndAsync();
    }

    public async Task SaveFileAsync(string fullPath, string content)
    {
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(fullPath, content ?? string.Empty, new UTF8Encoding(false));
    }

    private static IEnumerable<string> EnumerateFilesSafe(string rootPath)
    {
        var pending = new Stack<string>();
        pending.Push(rootPath);

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            IEnumerable<string> directories;
            IEnumerable<string> files;

            try
            {
                directories = Directory.EnumerateDirectories(current)
                    .Where(directory => !IgnoredDirectories.Contains(Path.GetFileName(directory)));
                files = Directory.EnumerateFiles(current);
            }
            catch
            {
                continue;
            }

            foreach (var file in files)
            {
                yield return file;
            }

            foreach (var directory in directories)
            {
                pending.Push(directory);
            }
        }
    }
}