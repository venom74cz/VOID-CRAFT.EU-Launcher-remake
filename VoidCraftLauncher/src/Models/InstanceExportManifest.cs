using System;
using System.Collections.Generic;

namespace VoidCraftLauncher.Models;

/// <summary>
/// Instance export/import manifest for backup snapshots.
/// </summary>
public class InstanceExportManifest
{
    public string LauncherVersion { get; set; } = "";
    public string InstanceName { get; set; } = "";
    public string MinecraftVersion { get; set; } = "";
    public string ModLoader { get; set; } = "";
    public string ModLoaderVersion { get; set; } = "";
    public DateTime ExportedAt { get; set; }
    public int ModEntryCount { get; set; }
    public int DownloadableModCount { get; set; }
    public int ManualModCount { get; set; }

    /// <summary>Relative paths of included files.</summary>
    public List<string> IncludedPaths { get; set; } = new();

    /// <summary>What was included: Saves, Configs, Mods, Options, ResourcePacks, ShaderPacks.</summary>
    public List<string> Categories { get; set; } = new();
}

public class InstanceExportModEntry
{
    public string FileName { get; set; } = "";
    public string Name { get; set; } = "";
    public string Source { get; set; } = "";
    public string ProjectId { get; set; } = "";
    public string FileId { get; set; } = "";
    public string VersionId { get; set; } = "";
    public string DownloadUrl { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Author { get; set; } = "";
    public string IconUrl { get; set; } = "";
    public string WebLink { get; set; } = "";
    public bool IsEnabled { get; set; } = true;
    public bool RequiresManualFile { get; set; }
}

public class InstanceImportResult
{
    public InstanceExportManifest? Manifest { get; set; }
    public int RestoredFileCount { get; set; }
    public int DownloadedModCount { get; set; }
    public int SkippedModCount { get; set; }
    public List<string> ManualModNames { get; set; } = new();
}
