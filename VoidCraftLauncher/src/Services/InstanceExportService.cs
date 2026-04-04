using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using VoidCraftLauncher.Models;

namespace VoidCraftLauncher.Services;

/// <summary>
/// Handles exporting and importing instance snapshots as .voidpack zip archives.
/// Mods are stored as a text manifest so exported packs stay Git-friendly.
/// </summary>
public class InstanceExportService
{
    private const string ManifestEntryName = "voidpack_manifest.json";
    private const string ModlistEntryName = "voidpack_modlist.json";
    private static readonly string[] DefaultExportCategories = { "saves", "config", "mods", "options.txt", "resourcepacks", "shaderpacks" };

    private readonly CurseForgeApi _curseForgeApi;
    private readonly ModrinthApi _modrinthApi;
    private readonly HttpClient _httpClient = new();

    public InstanceExportService(CurseForgeApi curseForgeApi, ModrinthApi modrinthApi)
    {
        _curseForgeApi = curseForgeApi;
        _modrinthApi = modrinthApi;
        var launcherVersion = typeof(InstanceExportService).Assembly.GetName().Version?.ToString(3) ?? "3.1.8";
        _httpClient.DefaultRequestHeaders.Add("User-Agent", $"VoidCraftLauncher/{launcherVersion}");
    }

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

        var filesToInclude = new List<string>();
        var modEntries = new List<InstanceExportModEntry>();

        foreach (var cat in cats)
        {
            var catPath = Path.Combine(instancePath, cat);
            if (string.Equals(cat, "mods", StringComparison.OrdinalIgnoreCase))
            {
                modEntries = BuildModExportEntries(instancePath);
                if (Directory.Exists(catPath))
                {
                    var files = Directory.GetFiles(catPath, "*", SearchOption.AllDirectories)
                        .Where(path => !IsTrackedModBinary(path))
                        .Where(path => !path.Contains(Path.Combine("mods", ".mod_metadata"), StringComparison.OrdinalIgnoreCase));
                    filesToInclude.AddRange(files);
                }

                continue;
            }

            if (Directory.Exists(catPath))
            {
                filesToInclude.AddRange(Directory.GetFiles(catPath, "*", SearchOption.AllDirectories));
            }
            else if (File.Exists(catPath))
            {
                filesToInclude.Add(catPath);
            }
        }

        manifest.IncludedPaths = filesToInclude
            .Select(file => Path.GetRelativePath(instancePath, file))
            .ToList();
        manifest.ModEntryCount = modEntries.Count;
        manifest.DownloadableModCount = modEntries.Count(entry => !entry.RequiresManualFile);
        manifest.ManualModCount = modEntries.Count(entry => entry.RequiresManualFile);

        var mcManifestPath = Path.Combine(instancePath, "voidcraft_manifest.json");
        if (File.Exists(mcManifestPath))
        {
            try
            {
                var manifestJson = await File.ReadAllTextAsync(mcManifestPath);
                var manifestDocument = JsonDocument.Parse(manifestJson);
                if (manifestDocument.RootElement.TryGetProperty("minecraft_version", out var minecraftVersion))
                {
                    manifest.MinecraftVersion = minecraftVersion.GetString() ?? string.Empty;
                }

                if (manifestDocument.RootElement.TryGetProperty("mod_loader_id", out var modLoader))
                {
                    manifest.ModLoader = modLoader.GetString() ?? string.Empty;
                }
            }
            catch
            {
            }
        }

        await Task.Run(() =>
        {
            using var archive = ZipFile.Open(outputPath, ZipArchiveMode.Create);

            var manifestEntry = archive.CreateEntry(ManifestEntryName);
            using (var manifestStream = manifestEntry.Open())
            {
                JsonSerializer.Serialize(manifestStream, manifest, new JsonSerializerOptions { WriteIndented = true });
            }

            if (modEntries.Count > 0)
            {
                var modlistEntry = archive.CreateEntry(ModlistEntryName);
                using var modlistStream = modlistEntry.Open();
                JsonSerializer.Serialize(modlistStream, modEntries, new JsonSerializerOptions { WriteIndented = true });
            }

            for (int index = 0; index < filesToInclude.Count; index++)
            {
                var filePath = filesToInclude[index];
                var relativePath = Path.GetRelativePath(instancePath, filePath);
                archive.CreateEntryFromFile(filePath, relativePath);
                progress?.Invoke(filesToInclude.Count == 0 ? 1d : (double)(index + 1) / filesToInclude.Count);
            }
        });

        LogService.Log($"InstanceExport: exported {filesToInclude.Count} files and {modEntries.Count} mod references to {outputPath}");
    }

    public async Task<InstanceImportResult> ImportAsync(string voidpackPath, string instancePath,
        Action<double>? progress = null)
    {
        var result = new InstanceImportResult();

        using var archive = ZipFile.OpenRead(voidpackPath);

        var manifestEntry = archive.GetEntry(ManifestEntryName);
        if (manifestEntry != null)
        {
            using var manifestStream = manifestEntry.Open();
            result.Manifest = JsonSerializer.Deserialize<InstanceExportManifest>(manifestStream);
        }

        List<InstanceExportModEntry> modEntries = new();
        var modlistEntry = archive.GetEntry(ModlistEntryName);
        if (modlistEntry != null)
        {
            using var modlistStream = modlistEntry.Open();
            modEntries = JsonSerializer.Deserialize<List<InstanceExportModEntry>>(modlistStream) ?? new List<InstanceExportModEntry>();
        }

        var archiveEntries = archive.Entries
            .Where(entry => !string.Equals(entry.FullName, ManifestEntryName, StringComparison.OrdinalIgnoreCase))
            .Where(entry => !string.Equals(entry.FullName, ModlistEntryName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var totalSteps = archiveEntries.Count + modEntries.Count;
        var completedSteps = 0;

        foreach (var entry in archiveEntries)
        {
            var destinationPath = Path.Combine(instancePath, entry.FullName);
            var fullDestinationPath = Path.GetFullPath(destinationPath);
            var fullBasePath = Path.GetFullPath(instancePath);
            if (!fullDestinationPath.StartsWith(fullBasePath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var directory = Path.GetDirectoryName(destinationPath);
            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (!string.IsNullOrEmpty(entry.Name))
            {
                entry.ExtractToFile(destinationPath, overwrite: true);
                result.RestoredFileCount++;
            }

            completedSteps++;
            progress?.Invoke(totalSteps == 0 ? 1d : (double)completedSteps / totalSteps);
        }

        if (modEntries.Count > 0)
        {
            var modRestoreResult = await RestoreModsFromManifestAsync(modEntries, instancePath, () =>
            {
                completedSteps++;
                progress?.Invoke(totalSteps == 0 ? 1d : (double)completedSteps / totalSteps);
            });

            result.DownloadedModCount = modRestoreResult.DownloadedModCount;
            result.SkippedModCount = modRestoreResult.SkippedModCount;
            result.ManualModNames = modRestoreResult.ManualModNames;
        }

        LogService.Log($"InstanceExport: imported {voidpackPath} → {instancePath} ({result.RestoredFileCount} files, {result.DownloadedModCount} mods)");
        return result;
    }

    private async Task<InstanceImportResult> RestoreModsFromManifestAsync(
        IReadOnlyCollection<InstanceExportModEntry> modEntries,
        string instancePath,
        Action onProcessed)
    {
        var result = new InstanceImportResult();
        var modsDirectory = Path.Combine(instancePath, "mods");
        Directory.CreateDirectory(modsDirectory);
        PrepareModsDirectory(modsDirectory);

        var metadataList = new List<ModMetadata>();
        foreach (var entry in modEntries)
        {
            try
            {
                if (entry.RequiresManualFile)
                {
                    result.ManualModNames.Add(string.IsNullOrWhiteSpace(entry.Name) ? entry.FileName : entry.Name);
                    continue;
                }

                var resolvedDownload = await ResolveModDownloadAsync(entry);
                if (resolvedDownload == null)
                {
                    result.SkippedModCount++;
                    result.ManualModNames.Add(string.IsNullOrWhiteSpace(entry.Name) ? entry.FileName : entry.Name);
                    continue;
                }

                var normalizedFileName = NormalizeModFileName(string.IsNullOrWhiteSpace(entry.FileName) ? resolvedDownload.FileName : entry.FileName);
                var installedFileName = entry.IsEnabled ? normalizedFileName : normalizedFileName + ".disabled";
                var targetPath = Path.Combine(modsDirectory, installedFileName);

                await DownloadFileAsync(resolvedDownload.Url, targetPath);
                result.DownloadedModCount++;

                metadataList.Add(new ModMetadata
                {
                    FileName = normalizedFileName,
                    Name = string.IsNullOrWhiteSpace(entry.Name) ? FormatModDisplayName(normalizedFileName) : entry.Name,
                    Slug = string.Empty,
                    ProjectId = entry.ProjectId ?? string.Empty,
                    FileId = entry.FileId ?? string.Empty,
                    VersionId = entry.VersionId ?? string.Empty,
                    Source = entry.Source ?? string.Empty,
                    Summary = entry.Summary ?? string.Empty,
                    Author = entry.Author ?? string.Empty,
                    IconUrl = entry.IconUrl ?? string.Empty,
                    WebLink = entry.WebLink ?? string.Empty,
                    DownloadUrl = string.IsNullOrWhiteSpace(entry.DownloadUrl) ? resolvedDownload.Url : entry.DownloadUrl,
                    IsEnabled = entry.IsEnabled,
                    InstalledAtUtc = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                result.SkippedModCount++;
                LogService.Error($"[InstanceExport] Failed to restore mod {entry.FileName}", ex);
            }
            finally
            {
                onProcessed();
            }
        }

        PersistImportedModMetadata(instancePath, modsDirectory, metadataList);
        return result;
    }

    private void PersistImportedModMetadata(string instancePath, string modsDirectory, IReadOnlyCollection<ModMetadata> metadataList)
    {
        var metadataPath = Path.Combine(instancePath, "mods_metadata.json");
        Directory.CreateDirectory(Path.Combine(modsDirectory, ".mod_metadata"));

        File.WriteAllText(metadataPath, JsonSerializer.Serialize(metadataList, new JsonSerializerOptions { WriteIndented = true }));

        foreach (var metadata in metadataList)
        {
            var sidecarPath = Path.Combine(modsDirectory, ".mod_metadata", NormalizeModFileName(metadata.FileName) + ".json");
            File.WriteAllText(sidecarPath, JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private void PrepareModsDirectory(string modsDirectory)
    {
        foreach (var file in Directory.GetFiles(modsDirectory, "*.jar", SearchOption.TopDirectoryOnly)
                     .Concat(Directory.GetFiles(modsDirectory, "*.jar.disabled", SearchOption.TopDirectoryOnly)))
        {
            try
            {
                File.Delete(file);
            }
            catch
            {
            }
        }

        var metadataDirectory = Path.Combine(modsDirectory, ".mod_metadata");
        if (Directory.Exists(metadataDirectory))
        {
            foreach (var file in Directory.GetFiles(metadataDirectory, "*.json", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                }
            }
        }
    }

    private List<InstanceExportModEntry> BuildModExportEntries(string instancePath)
    {
        var modsDirectory = Path.Combine(instancePath, "mods");
        if (!Directory.Exists(modsDirectory))
        {
            return new List<InstanceExportModEntry>();
        }

        var metadataIndex = LoadInstalledModMetadataIndex(instancePath, Path.Combine(modsDirectory, ".mod_metadata"));
        return Directory.GetFiles(modsDirectory, "*", SearchOption.TopDirectoryOnly)
            .Where(IsTrackedModBinary)
            .Select(filePath => BuildModExportEntry(Path.GetFileName(filePath), metadataIndex))
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private InstanceExportModEntry BuildModExportEntry(string installedFileName, IReadOnlyDictionary<string, ModMetadata> metadataIndex)
    {
        var normalizedFileName = NormalizeModFileName(installedFileName);
        metadataIndex.TryGetValue(normalizedFileName, out var metadata);

        var source = metadata?.Source ?? "Manual";
        var projectId = metadata?.ProjectId ?? string.Empty;
        var fileId = metadata?.FileId ?? string.Empty;
        var versionId = metadata?.VersionId ?? string.Empty;
        var downloadUrl = metadata?.DownloadUrl ?? string.Empty;
        var canAutoDownload = !string.IsNullOrWhiteSpace(downloadUrl) ||
                              (source.Equals("CurseForge", StringComparison.OrdinalIgnoreCase) &&
                               !string.IsNullOrWhiteSpace(projectId) &&
                               !string.IsNullOrWhiteSpace(fileId)) ||
                              (source.Equals("Modrinth", StringComparison.OrdinalIgnoreCase) &&
                               (!string.IsNullOrWhiteSpace(versionId) || !string.IsNullOrWhiteSpace(projectId)));

        return new InstanceExportModEntry
        {
            FileName = normalizedFileName,
            Name = string.IsNullOrWhiteSpace(metadata?.Name) ? FormatModDisplayName(normalizedFileName) : metadata!.Name,
            Source = source,
            ProjectId = projectId,
            FileId = fileId,
            VersionId = versionId,
            DownloadUrl = downloadUrl,
            Summary = metadata?.Summary ?? string.Empty,
            Author = metadata?.Author ?? string.Empty,
            IconUrl = metadata?.IconUrl ?? string.Empty,
            WebLink = metadata?.WebLink ?? string.Empty,
            IsEnabled = !installedFileName.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase),
            RequiresManualFile = !canAutoDownload
        };
    }

    private Dictionary<string, ModMetadata> LoadInstalledModMetadataIndex(string instancePath, string metadataDirectory)
    {
        var metadata = new Dictionary<string, ModMetadata>(StringComparer.OrdinalIgnoreCase);
        var metadataPath = Path.Combine(instancePath, "mods_metadata.json");

        if (File.Exists(metadataPath))
        {
            try
            {
                var json = File.ReadAllText(metadataPath);
                var entries = JsonSerializer.Deserialize<List<ModMetadata>>(json) ?? new List<ModMetadata>();
                foreach (var entry in entries.Where(entry => !string.IsNullOrWhiteSpace(entry.FileName)))
                {
                    entry.FileName = NormalizeModFileName(entry.FileName);
                    metadata[entry.FileName] = entry;
                }
            }
            catch (Exception ex)
            {
                LogService.Error("[InstanceExport] Failed to read mods_metadata.json", ex);
            }
        }

        if (!Directory.Exists(metadataDirectory))
        {
            return metadata;
        }

        foreach (var file in Directory.GetFiles(metadataDirectory, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var fileMetadata = ReadMetadataFile(file);
                if (fileMetadata == null || string.IsNullOrWhiteSpace(fileMetadata.FileName))
                {
                    continue;
                }

                fileMetadata.FileName = NormalizeModFileName(fileMetadata.FileName);
                metadata[fileMetadata.FileName] = fileMetadata;
            }
            catch (Exception ex)
            {
                LogService.Error($"[InstanceExport] Failed to read mod metadata {file}", ex);
            }
        }

        return metadata;
    }

    private static ModMetadata? ReadMetadataFile(string path)
    {
        var json = File.ReadAllText(path);
        try
        {
            var metadata = JsonSerializer.Deserialize<ModMetadata>(json);
            if (metadata != null)
            {
                return metadata;
            }
        }
        catch
        {
        }

        var node = JsonNode.Parse(json);
        var fileName = node?["FileName"]?.ToString() ?? node?["fileName"]?.ToString();
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        return new ModMetadata
        {
            FileName = fileName,
            Name = node?["Name"]?.ToString() ?? node?["name"]?.ToString() ?? string.Empty,
            ProjectId = node?["Id"]?.ToString() ?? node?["ProjectId"]?.ToString() ?? node?["projectId"]?.ToString() ?? string.Empty,
            FileId = node?["FileId"]?.ToString() ?? node?["fileId"]?.ToString() ?? string.Empty,
            VersionId = node?["VersionId"]?.ToString() ?? node?["versionId"]?.ToString() ?? string.Empty,
            Source = node?["Source"]?.ToString() ?? node?["source"]?.ToString() ?? string.Empty,
            Summary = node?["Description"]?.ToString() ?? node?["Summary"]?.ToString() ?? node?["summary"]?.ToString() ?? string.Empty,
            Author = node?["Author"]?.ToString() ?? node?["author"]?.ToString() ?? string.Empty,
            IconUrl = node?["IconUrl"]?.ToString() ?? node?["iconUrl"]?.ToString() ?? string.Empty,
            WebLink = node?["WebLink"]?.ToString() ?? node?["webLink"]?.ToString() ?? string.Empty,
            DownloadUrl = node?["DownloadUrl"]?.ToString() ?? node?["downloadUrl"]?.ToString() ?? string.Empty,
            IsEnabled = node?["IsEnabled"]?.GetValue<bool?>() ?? node?["isEnabled"]?.GetValue<bool?>() ?? true
        };
    }

    private async Task<ResolvedModDownload?> ResolveModDownloadAsync(InstanceExportModEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.DownloadUrl))
        {
            return new ResolvedModDownload(entry.DownloadUrl, NormalizeModFileName(entry.FileName));
        }

        if (string.Equals(entry.Source, "CurseForge", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(entry.ProjectId, out var curseProjectId) &&
            int.TryParse(entry.FileId, out var curseFileId))
        {
            var directUrl = await _curseForgeApi.GetFileDownloadUrlAsync(curseProjectId, curseFileId);
            if (!string.IsNullOrWhiteSpace(directUrl))
            {
                return new ResolvedModDownload(directUrl, NormalizeModFileName(entry.FileName));
            }

            var fileJson = await _curseForgeApi.GetModFileAsync(curseProjectId, curseFileId);
            var fileNode = JsonNode.Parse(fileJson)?["data"];
            var downloadUrl = fileNode?["downloadUrl"]?.ToString();
            if (!string.IsNullOrWhiteSpace(downloadUrl))
            {
                var fileName = fileNode?["fileName"]?.ToString();
                return new ResolvedModDownload(downloadUrl, NormalizeModFileName(string.IsNullOrWhiteSpace(fileName) ? entry.FileName : fileName));
            }
        }

        if (string.Equals(entry.Source, "Modrinth", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(entry.VersionId))
            {
                var versionJson = await _modrinthApi.GetVersionAsync(entry.VersionId);
                var versionNode = JsonNode.Parse(versionJson);
                var resolved = ResolveModrinthFile(versionNode, entry.FileName);
                if (resolved != null)
                {
                    return resolved;
                }
            }

            if (!string.IsNullOrWhiteSpace(entry.ProjectId))
            {
                var versionsJson = await _modrinthApi.GetProjectVersionsAsync(entry.ProjectId);
                var versions = JsonNode.Parse(versionsJson)?.AsArray();
                var matchingVersion = versions?.FirstOrDefault(version =>
                                          string.Equals(version?["id"]?.ToString(), entry.VersionId, StringComparison.OrdinalIgnoreCase))
                                      ?? versions?.FirstOrDefault();
                var resolved = ResolveModrinthFile(matchingVersion, entry.FileName);
                if (resolved != null)
                {
                    return resolved;
                }
            }
        }

        return null;
    }

    private static ResolvedModDownload? ResolveModrinthFile(JsonNode? versionNode, string fallbackFileName)
    {
        var files = versionNode?["files"]?.AsArray();
        var primaryFile = files?.FirstOrDefault(file => file?["primary"]?.GetValue<bool>() == true)
                          ?? files?.FirstOrDefault();
        var url = primaryFile?["url"]?.ToString();
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var fileName = primaryFile?["filename"]?.ToString();
        return new ResolvedModDownload(url, NormalizeModFileName(string.IsNullOrWhiteSpace(fileName) ? fallbackFileName : fileName));
    }

    private async Task DownloadFileAsync(string url, string destinationPath)
    {
        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var bytes = await _httpClient.GetByteArrayAsync(url);
        await File.WriteAllBytesAsync(destinationPath, bytes);
    }

    private static bool IsTrackedModBinary(string path)
    {
        return path.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".jar.disabled", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeModFileName(string fileName)
    {
        return fileName.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase)
            ? fileName[..^".disabled".Length]
            : fileName;
    }

    private static string FormatModDisplayName(string fileName)
    {
        return Path.GetFileNameWithoutExtension(NormalizeModFileName(fileName))
            .Replace('_', ' ')
            .Replace('-', ' ');
    }

    private sealed record ResolvedModDownload(string Url, string FileName);
}
