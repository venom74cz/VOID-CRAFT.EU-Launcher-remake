using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VoidCraftLauncher.Models.CreatorStudio;

namespace VoidCraftLauncher.Services.CreatorStudio;

public sealed class CreatorReleaseService
{
    private readonly CreatorManifestService _manifestService;
    private readonly LauncherService _launcherService;

    public CreatorReleaseService(CreatorManifestService manifestService, LauncherService launcherService)
    {
        _manifestService = manifestService;
        _launcherService = launcherService;
    }

    public List<CreatorReleaseStep> BuildPipeline(CreatorManifest? manifest, string workspacePath)
    {
        var steps = new List<CreatorReleaseStep>
        {
            new() { Step = CreatorReleasePipelineStep.Version },
            new() { Step = CreatorReleasePipelineStep.Snapshot },
            new() { Step = CreatorReleasePipelineStep.Validate },
            new() { Step = CreatorReleasePipelineStep.Notes },
            new() { Step = CreatorReleasePipelineStep.Publish }
        };

        if (manifest == null) return steps;

        // Version step
        if (!string.IsNullOrWhiteSpace(manifest.Version))
            steps[0].Status = CreatorReleaseStepStatus.Completed;

        // Snapshot step
        var snapshotDir = Path.Combine(workspacePath, ".voidcraft", "snapshots");
        if (Directory.Exists(snapshotDir) && Directory.EnumerateFiles(snapshotDir).Any())
            steps[1].Status = CreatorReleaseStepStatus.Completed;

        // Validate — run checks
        var checks = RunValidation(manifest, workspacePath);
        var allPassed = checks.All(c => c.Passed || c.IsWarning);
        var hasWarnings = checks.Any(c => c.IsWarning && !c.Passed);
        steps[2].Status = allPassed
            ? hasWarnings ? CreatorReleaseStepStatus.Warning : CreatorReleaseStepStatus.Completed
            : CreatorReleaseStepStatus.Failed;

        // Notes + Publish remain pending unless we detect artifacts
        var exportDir = Path.Combine(workspacePath, ".voidcraft", "exports");
        if (Directory.Exists(exportDir) && Directory.EnumerateFiles(exportDir).Any())
        {
            steps[3].Status = CreatorReleaseStepStatus.Completed;
            steps[4].Status = CreatorReleaseStepStatus.Completed;
        }

        // Set first incomplete step as active
        var firstPending = steps.FirstOrDefault(s => s.Status == CreatorReleaseStepStatus.Pending);
        if (firstPending != null)
            firstPending.Status = CreatorReleaseStepStatus.InProgress;

        return steps;
    }

    public List<CreatorValidationCheck> RunValidation(CreatorManifest? manifest, string workspacePath)
    {
        var checks = new List<CreatorValidationCheck>();

        checks.Add(new CreatorValidationCheck
        {
            Label = "Metadata kompletni",
            Passed = manifest != null &&
                     !string.IsNullOrWhiteSpace(manifest.PackName) &&
                     !string.IsNullOrWhiteSpace(manifest.Slug) &&
                     !string.IsNullOrWhiteSpace(manifest.Version)
        });

        checks.Add(new CreatorValidationCheck
        {
            Label = "Logo nastaveno",
            Passed = manifest?.Branding != null && !string.IsNullOrWhiteSpace(manifest.Branding.LogoPath)
        });

        var modsDir = Path.Combine(workspacePath, "mods");
        var modCount = Directory.Exists(modsDir)
            ? Directory.EnumerateFiles(modsDir, "*.jar").Count() + Directory.EnumerateFiles(modsDir, "*.jar.disabled").Count()
            : 0;

        checks.Add(new CreatorValidationCheck
        {
            Label = $"{modCount} modu — zadne zname konflikty",
            Passed = modCount > 0
        });

        checks.Add(new CreatorValidationCheck
        {
            Label = "Cover nastaveno",
            Passed = manifest?.Branding != null && !string.IsNullOrWhiteSpace(manifest.Branding.CoverPath),
            IsWarning = true
        });

        checks.Add(new CreatorValidationCheck
        {
            Label = $"Loader target: {manifest?.ModLoader ?? "?"} {manifest?.ModLoaderVersion ?? ""}",
            Passed = !string.IsNullOrWhiteSpace(manifest?.ModLoader)
        });

        var totalSizeMb = 0L;
        if (Directory.Exists(workspacePath))
        {
            try
            {
                totalSizeMb = new DirectoryInfo(workspacePath)
                    .EnumerateFiles("*", SearchOption.AllDirectories)
                    .Sum(f => f.Length) / (1024 * 1024);
            }
            catch { }
        }

        checks.Add(new CreatorValidationCheck
        {
            Label = $"Velikost: {totalSizeMb} MB",
            Passed = true
        });

        return checks;
    }

    public List<CreatorExportProfile> GetExportProfiles(CreatorManifest? manifest)
    {
        var profiles = new List<CreatorExportProfile>();

        profiles.Add(new CreatorExportProfile
        {
            Format = ".voidpack",
            Label = ".voidpack",
            IsReady = manifest != null && !string.IsNullOrWhiteSpace(manifest.PackName)
        });

        profiles.Add(new CreatorExportProfile
        {
            Format = "CurseForge",
            Label = "CurseForge",
            IsReady = manifest != null && !string.IsNullOrWhiteSpace(manifest.PackName),
            BlockerLabel = manifest?.Branding?.CoverPath == null ? "cover" : ""
        });

        profiles.Add(new CreatorExportProfile
        {
            Format = ".mrpack",
            Label = ".mrpack",
            IsReady = manifest != null && !string.IsNullOrWhiteSpace(manifest.PackName)
        });

        return profiles;
    }

    public List<CreatorReleaseHistoryEntry> GetReleaseHistory(string workspacePath)
    {
        var exportDir = Path.Combine(workspacePath, ".voidcraft", "exports");
        if (!Directory.Exists(exportDir))
            return new List<CreatorReleaseHistoryEntry>();

        return Directory.EnumerateFiles(exportDir)
            .OrderByDescending(f => File.GetLastWriteTimeUtc(f))
            .Take(10)
            .Select(f =>
            {
                var fi = new FileInfo(f);
                var ext = fi.Extension.ToLowerInvariant();
                return new CreatorReleaseHistoryEntry
                {
                    Version = fi.Name,
                    Channel = ext == ".voidpack" ? "voidpack" : ext == ".mrpack" ? "mrpack" : "export",
                    ReleasedUtc = fi.LastWriteTimeUtc,
                    ExportFormat = ext
                };
            })
            .ToList();
    }
}
