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

    /// <summary>Relative paths of included files.</summary>
    public List<string> IncludedPaths { get; set; } = new();

    /// <summary>What was included: Saves, Configs, Mods, Options, ResourcePacks, ShaderPacks.</summary>
    public List<string> Categories { get; set; } = new();
}
