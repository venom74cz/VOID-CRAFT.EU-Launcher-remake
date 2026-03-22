using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using VoidCraftLauncher.Models;

namespace VoidCraftLauncher.Services;

/// <summary>
/// Handles exporting and importing instance snapshots as .voidpack zip archives.
/// Each archive contains a manifest + selected directories (saves, configs, mods, etc.).
/// </summary>
public class InstanceExportService
{
    private static readonly string[] DefaultExportCategories = { "saves", "config", "mods", "options.txt", "resourcepacks", "shaderpacks" };

    /// <summary>
    /// Exports an instance directory to a .voidpack zip file.
    /// </summary>
    /// <param name="instancePath">Root path of the Minecraft instance.</param>
    /// <param name="instanceName">Display name of the instance.</param>
    /// <param name="outputPath">Full path for the output .voidpack file.</param>
    /// <param name="categories">Which directories/files to include (null = all defaults).</param>
    /// <param name="progress">Progress callback (0..1).</param>
    public async Task ExportAsync(string instancePath, string instanceName, string outputPath,
        IEnumerable<string>? categories = null, Action<double>? progress = null)
    {
        var cats = (categories ?? DefaultExportCategories).ToList();

        var manifest = new InstanceExportManifest
        {
            LauncherVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0",
            InstanceName = instanceName,
            ExportedAt = DateTime.UtcNow,
            Categories = cats
        };

        // Collect files to include
        var filesToInclude = new List<string>();
        foreach (var cat in cats)
        {
            var catPath = Path.Combine(instancePath, cat);
            if (Directory.Exists(catPath))
            {
                var files = Directory.GetFiles(catPath, "*", SearchOption.AllDirectories);
                filesToInclude.AddRange(files);
            }
            else if (File.Exists(catPath))
            {
                filesToInclude.Add(catPath);
            }
        }

        manifest.IncludedPaths = filesToInclude
            .Select(f => Path.GetRelativePath(instancePath, f))
            .ToList();

        // Try to read instance manifest for MC version info
        var mcManifestPath = Path.Combine(instancePath, "voidcraft_manifest.json");
        if (File.Exists(mcManifestPath))
        {
            try
            {
                var mJson = await File.ReadAllTextAsync(mcManifestPath);
                var mDoc = JsonDocument.Parse(mJson);
                if (mDoc.RootElement.TryGetProperty("minecraft_version", out var mcv))
                    manifest.MinecraftVersion = mcv.GetString() ?? "";
                if (mDoc.RootElement.TryGetProperty("mod_loader_id", out var ml))
                    manifest.ModLoader = ml.GetString() ?? "";
            }
            catch { /* Ignore parsing errors */ }
        }

        // Create zip
        await Task.Run(() =>
        {
            using var archive = ZipFile.Open(outputPath, ZipArchiveMode.Create);

            // Write manifest
            var manifestEntry = archive.CreateEntry("voidpack_manifest.json");
            using (var ms = manifestEntry.Open())
            {
                JsonSerializer.Serialize(ms, manifest, new JsonSerializerOptions { WriteIndented = true });
            }

            // Write files
            for (int i = 0; i < filesToInclude.Count; i++)
            {
                var filePath = filesToInclude[i];
                var relativePath = Path.GetRelativePath(instancePath, filePath);
                archive.CreateEntryFromFile(filePath, relativePath);
                progress?.Invoke((double)(i + 1) / filesToInclude.Count);
            }
        });

        LogService.Log($"InstanceExport: exported {filesToInclude.Count} files to {outputPath}");
    }

    /// <summary>
    /// Imports a .voidpack archive into the given instance directory.
    /// </summary>
    /// <param name="voidpackPath">Path to the .voidpack zip.</param>
    /// <param name="instancePath">Destination instance directory.</param>
    /// <param name="progress">Progress callback (0..1).</param>
    /// <returns>The manifest from the imported pack, or null on failure.</returns>
    public async Task<InstanceExportManifest?> ImportAsync(string voidpackPath, string instancePath,
        Action<double>? progress = null)
    {
        InstanceExportManifest? manifest = null;

        await Task.Run(() =>
        {
            using var archive = ZipFile.OpenRead(voidpackPath);

            // Read manifest first
            var manifestEntry = archive.GetEntry("voidpack_manifest.json");
            if (manifestEntry != null)
            {
                using var ms = manifestEntry.Open();
                manifest = JsonSerializer.Deserialize<InstanceExportManifest>(ms);
            }

            var entries = archive.Entries.Where(e => e.FullName != "voidpack_manifest.json").ToList();

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var destPath = Path.Combine(instancePath, entry.FullName);

                // Prevent path traversal
                var fullDest = Path.GetFullPath(destPath);
                var fullBase = Path.GetFullPath(instancePath);
                if (!fullDest.StartsWith(fullBase, StringComparison.OrdinalIgnoreCase))
                    continue;

                var dir = Path.GetDirectoryName(destPath);
                if (dir != null && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                if (!string.IsNullOrEmpty(entry.Name))
                {
                    entry.ExtractToFile(destPath, overwrite: true);
                }

                progress?.Invoke((double)(i + 1) / entries.Count);
            }
        });

        LogService.Log($"InstanceExport: imported {voidpackPath} → {instancePath}");
        return manifest;
    }
}
