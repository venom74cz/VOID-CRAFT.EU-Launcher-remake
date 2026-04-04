using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using VoidCraftLauncher.Models;
using VoidCraftLauncher.Models.CurseForge;

namespace VoidCraftLauncher.Services
{
    // Result of modpack installation containing manifest info
    public class ModpackManifestInfo
    {
        public string PackName { get; set; } = "";
        public string Author { get; set; } = "";
        public string MinecraftVersion { get; set; } = "";
        public string ModLoaderId { get; set; } = ""; // e.g. "neoforge-21.1.90"
        public string ModLoaderType { get; set; } = ""; // "neoforge", "forge", "fabric"
        public int ModCount { get; set; }
        public int FileId { get; set; } // CurseForge FileID of installed version
        public string Version { get; set; } = ""; // Generic version string for Modrinth
    }

    public class ModpackInstaller
    {
        private readonly CurseForgeApi _api;
        private readonly HttpClient _httpClient;

        public event Action<string> StatusChanged;
        public event Action<double> ProgressChanged; // 0.0 to 1.0

        public ModpackInstaller(CurseForgeApi api)
        {
            _api = api;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/octet-stream, */*");
        }

        public async Task<ModpackManifestInfo> InstallOrUpdateAsync(string modpackZipPath, string installPath, int? targetFileId = null, string? targetVersion = null)
        {
            StatusChanged?.Invoke("Otevírám balíček...");
            
            ModpackManifestInfo manifestInfo = new ModpackManifestInfo();
            var completionStatus = "Instalace dokončena!";
            if (targetFileId.HasValue) manifestInfo.FileId = targetFileId.Value;
            if (!string.IsNullOrWhiteSpace(targetVersion)) manifestInfo.Version = targetVersion;
            
            using (var archive = ZipFile.OpenRead(modpackZipPath))
            {
                // 1. Detect format
                var manifestEntry = archive.GetEntry("manifest.json");
                var modrinthEntry = archive.GetEntry("modrinth.index.json");

                if (modrinthEntry != null)
                {
                    var mrInfo = await InstallModrinthAsync(archive, installPath, StatusChanged, ProgressChanged, targetVersion);
                    return mrInfo;
                }

                if (manifestEntry == null) throw new Exception("Neplatný modpack: chybí manifest.json nebo modrinth.index.json");

                CurseForgeManifest manifest;
                using (var stream = manifestEntry.Open())
                {
                    manifest = await JsonSerializer.DeserializeAsync<CurseForgeManifest>(stream);
                }

                if (manifest == null) throw new Exception("Nepodařilo se načíst manifest.");
                var manifestFiles = manifest.Files ?? new List<ManifestFile>();
                
                // Extract manifest info for launcher
                manifestInfo.PackName = manifest.Name ?? string.Empty;
                manifestInfo.Author = manifest.Author ?? string.Empty;
                manifestInfo.MinecraftVersion = manifest.Minecraft?.Version ?? "1.21.1";
                manifestInfo.ModCount = manifestFiles.Count;
                if (string.IsNullOrWhiteSpace(manifestInfo.Version) && !string.IsNullOrWhiteSpace(manifest.Version))
                {
                    manifestInfo.Version = manifest.Version;
                }
                
                var primaryLoader = manifest.Minecraft?.ModLoaders?.FirstOrDefault(m => m.Primary) 
                                   ?? manifest.Minecraft?.ModLoaders?.FirstOrDefault();
                if (primaryLoader != null)
                {
                    manifestInfo.ModLoaderId = primaryLoader.Id; // e.g. "neoforge-21.1.90"
                    // Extract loader type
                    if (primaryLoader.Id.StartsWith("neoforge", StringComparison.OrdinalIgnoreCase)) manifestInfo.ModLoaderType = "neoforge";
                    else if (primaryLoader.Id.StartsWith("forge", StringComparison.OrdinalIgnoreCase)) manifestInfo.ModLoaderType = "forge";
                    else if (primaryLoader.Id.StartsWith("fabric", StringComparison.OrdinalIgnoreCase)) manifestInfo.ModLoaderType = "fabric";
                    else if (primaryLoader.Id.StartsWith("quilt", StringComparison.OrdinalIgnoreCase)) manifestInfo.ModLoaderType = "quilt";
                }

                // DEBUG LOGGING
                var debugLogPath = Path.Combine(installPath, "install_debug.txt");
                using var debugLog = new StreamWriter(debugLogPath, false) { AutoFlush = true };
                debugLog.WriteLine($"Install Start: {DateTime.Now}");
                debugLog.WriteLine($"Target FileId: {targetFileId}");
                debugLog.WriteLine($"Manifest Overrides: '{manifest.Overrides}'");
                debugLog.WriteLine($"Manifest Files Count: {manifestFiles.Count}");

                StatusChanged?.Invoke($"Načítám informace o modech ({manifestFiles.Count})...");

                // 2. Resolve URLs for mods - chunked first, then per-file fallback.
                var fileIds = manifestFiles.Select(f => f.FileID).Distinct().ToList();
                var curseFilesById = new Dictionary<int, CurseFile>();

                for (int i = 0; i < fileIds.Count; i += 40)
                {
                    var chunk = fileIds.Skip(i).Take(40);
                    var filesJson = await _api.GetFilesAsync(chunk);
                    var curseFilesData = JsonSerializer.Deserialize<CurseFileDatas>(filesJson);
                    if (curseFilesData?.Data == null)
                    {
                        continue;
                    }

                    foreach (var curseFile in curseFilesData.Data)
                    {
                        curseFilesById[curseFile.Id] = curseFile;
                    }
                }

                foreach (var manifestFile in manifestFiles.Where(file => !curseFilesById.ContainsKey(file.FileID)))
                {
                    try
                    {
                        var fileJson = await _api.GetModFileAsync(manifestFile.ProjectID, manifestFile.FileID);
                        var singleFileData = JsonSerializer.Deserialize<CurseSingleFileData>(fileJson);
                        if (singleFileData?.Data != null)
                        {
                            curseFilesById[singleFileData.Data.Id] = singleFileData.Data;
                        }
                    }
                    catch (Exception ex)
                    {
                        debugLog.WriteLine($"ResolveFileFallback FAILED: Project={manifestFile.ProjectID}, File={manifestFile.FileID} | {ex.Message}");
                    }
                }

                var resolvedManifestFiles = manifestFiles
                    .Where(file => curseFilesById.TryGetValue(file.FileID, out _))
                    .Select(file => new ResolvedCurseManifestFile(file, curseFilesById[file.FileID]))
                    .GroupBy(file => file.File.Id)
                    .Select(group => group.First())
                    .ToList();

                var unresolvedManifestFiles = manifestFiles
                    .Where(file => !curseFilesById.ContainsKey(file.FileID))
                    .ToList();

                var unresolvedRequiredFiles = unresolvedManifestFiles
                    .Where(file => file.Required)
                    .ToList();

                if (unresolvedRequiredFiles.Count > 0)
                {
                    debugLog.WriteLine("Required file metadata missing: " + string.Join(", ", unresolvedRequiredFiles.Select(file => $"{file.ProjectID}:{file.FileID}")));
                    throw new Exception($"Nepodařilo se dohledat metadata pro {unresolvedRequiredFiles.Count} povinných souborů modpacku.");
                }

                if (unresolvedManifestFiles.Count > 0)
                {
                    debugLog.WriteLine("Optional file metadata missing: " + string.Join(", ", unresolvedManifestFiles.Select(file => $"{file.ProjectID}:{file.FileID}")));
                }

                // 2b. Resolve categories - chunked.
                var resolvedCurseFiles = resolvedManifestFiles.Select(file => file.File).ToList();
                var modIds = resolvedCurseFiles.Select(f => f.ModId).Distinct().ToList();
                var modClassMap = new Dictionary<int, int>(); // ModId -> ClassId
                var allMods = new List<CurseMod>();
                
                try
                {
                    StatusChanged?.Invoke("Ověřuji typy souborů...");
                    for (int i = 0; i < modIds.Count; i += 40)
                    {
                        var chunk = modIds.Skip(i).Take(40);
                        var modsJson = await _api.GetModsAsync(chunk);
                        var modsData = JsonSerializer.Deserialize<CurseModsData>(modsJson);
                        if (modsData?.Data != null)
                        {
                            allMods.AddRange(modsData.Data);
                            foreach (var m in modsData.Data)
                            {
                                modClassMap[m.Id] = m.ClassId;
                            }
                        }
                    }

                    // METADATA PERSISTENCE
                    try 
                    {
                        var metadataList = new List<ModMetadata>();
                        var metadataPath = Path.Combine(installPath, "mods_metadata.json");
                        
                        // Try load existing
                        if (File.Exists(metadataPath))
                        {
                            try 
                            {
                                var existingJson = File.ReadAllText(metadataPath);
                                var existing = JsonSerializer.Deserialize<List<ModMetadata>>(existingJson);
                                if (existing != null) metadataList.AddRange(existing);
                            } 
                            catch {}
                        }

                        // Update/Add new using the collected allMods
                        foreach (var m in allMods)
                        {
                            var filesForMod = resolvedManifestFiles.Where(file => file.File.ModId == m.Id).Select(file => file.File);
                            foreach (var f in filesForMod)
                            {
                                metadataList.RemoveAll(x => x.FileName == f.FileName);
                                metadataList.Add(new ModMetadata
                                {
                                    FileName = f.FileName,
                                    Name = m.Name,
                                    Slug = m.Slug,
                                    ProjectId = m.Id.ToString(),
                                    FileId = f.Id.ToString(),
                                    Source = "CurseForge",
                                    Summary = m.Summary,
                                    Categories = m.Categories?.Select(c => c.Name).ToList() ?? new List<string>(),
                                    IconUrl = m.Logo?.ThumbnailUrl,
                                    WebLink = m.Links?.WebsiteUrl,
                                    DownloadUrl = f.DownloadUrl ?? string.Empty,
                                    IsEnabled = true
                                });
                            }
                        }
                        
                        var metaJson = JsonSerializer.Serialize(metadataList, new JsonSerializerOptions { WriteIndented = true });
                        File.WriteAllText(metadataPath, metaJson);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to save mod metadata: {ex.Message}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to resolve mod categories: {ex.Message}");
                }

                // 3. Smart Update Logic
                var modsDir = Path.Combine(installPath, "mods");
                var rpDir = Path.Combine(installPath, "resourcepacks");
                var shaderDir = Path.Combine(installPath, "shaderpacks");
                
                Directory.CreateDirectory(modsDir);
                Directory.CreateDirectory(rpDir);
                Directory.CreateDirectory(shaderDir);

                var canUpdateTrackedFiles = unresolvedManifestFiles.Count == 0;

                // Load previously installed files tracking
                var installedFilesPath = Path.Combine(installPath, "installed_files.json");
                HashSet<string> previouslyInstalledFiles = new HashSet<string>();
                if (File.Exists(installedFilesPath))
                {
                    try 
                    {
                        var json = await File.ReadAllTextAsync(installedFilesPath);
                        previouslyInstalledFiles = JsonSerializer.Deserialize<HashSet<string>>(json) ?? new HashSet<string>();
                    }
                    catch {}
                }

                // A. Delete old mods (ONLY if they were installed by us previously)
                // We scan for both active (.jar) and disabled (.jar.disabled) files
                var jarFiles = Directory.GetFiles(modsDir, "*.jar");
                var disabledFiles = Directory.GetFiles(modsDir, "*.jar.disabled");
                var existingFiles = jarFiles.Concat(disabledFiles).ToArray();
                
                var newModNames = resolvedManifestFiles
                    .Select(file => file.File.FileName)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                if (canUpdateTrackedFiles)
                {
                    foreach (var file in existingFiles)
                    {
                        var fileName = Path.GetFileName(file);
                        var pureFileName = fileName.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase)
                            ? fileName.Substring(0, fileName.Length - ".disabled".Length)
                            : fileName;

                        if (!newModNames.Contains(pureFileName) && previouslyInstalledFiles.Contains(pureFileName))
                        {
                            TryDeleteFile(file);
                            StatusChanged?.Invoke($"Odstraňuji starý soubor: {fileName}");
                        }
                    }

                    await File.WriteAllTextAsync(installedFilesPath, JsonSerializer.Serialize(newModNames));
                }
                else
                {
                    debugLog.WriteLine("Skipping tracked file prune/update because some manifest file metadata could not be resolved.");
                }

                // B. Download new/updated mods
                int started = 0;
                int completed = 0;
                int total = resolvedManifestFiles.Count;
                int downloaded = 0;
                int skipped = 0;
                
                object logLock = new object();
                object progressLock = new object();
                object failureLock = new object();
                var semaphore = new SemaphoreSlim(10);
                var failedRequiredDownloads = new List<string>();
                var failedOptionalDownloads = new List<string>();
                
                var downloadTasks = resolvedManifestFiles.Select(async resolvedFile => 
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        var mod = resolvedFile.File;

                        // Determine Target Directory
                        string targetDir = modsDir; // Default
                        if (modClassMap.TryGetValue(mod.ModId, out int classId))
                        {
                            if (classId == 12) targetDir = rpDir; // Resource Pack
                            else if (classId == 6552 || classId == 4546) targetDir = shaderDir; // Shader Pack
                        }

                        var destPath = Path.Combine(targetDir, mod.FileName);
                        var disabledPath = destPath + ".disabled";
                        var preferredPath = File.Exists(disabledPath) ? disabledPath : destPath;
                        var existingPath = File.Exists(disabledPath)
                            ? disabledPath
                            : File.Exists(destPath)
                                ? destPath
                                : null;

                        if (!string.IsNullOrWhiteSpace(existingPath))
                        {
                            if (ValidateDownloadedCurseFile(existingPath, mod, message =>
                                {
                                    lock (logLock)
                                    {
                                        debugLog.WriteLine(message);
                                    }
                                }))
                            {
                                Interlocked.Increment(ref skipped);
                                return;
                            }

                            TryDeleteFile(existingPath);
                            lock (logLock)
                            {
                                debugLog.WriteLine($"Deleting invalid existing file: {existingPath}");
                            }
                        }

                        var startedIndex = Interlocked.Increment(ref started);
                        lock (progressLock)
                        {
                            StatusChanged?.Invoke($"Stahuji ({startedIndex}/{total}): {mod.DisplayName}");
                        }

                        await DownloadCurseFileWithFallbackAsync(resolvedFile, preferredPath, message =>
                        {
                            lock (logLock)
                            {
                                debugLog.WriteLine(message);
                            }
                        });
                        Interlocked.Increment(ref downloaded);
                    }
                    catch (Exception ex)
                    {
                        var failureLabel = $"{resolvedFile.File.DisplayName} ({resolvedFile.File.FileName})";
                        lock (failureLock)
                        {
                            if (resolvedFile.Manifest.Required)
                            {
                                failedRequiredDownloads.Add(failureLabel);
                            }
                            else
                            {
                                failedOptionalDownloads.Add(failureLabel);
                            }
                        }

                        lock (logLock)
                        {
                            debugLog.WriteLine($"FINAL DOWNLOAD FAILURE: {failureLabel} | {ex.Message}");
                        }
                    }
                    finally
                    {
                        var completedCount = Interlocked.Increment(ref completed);
                        lock (progressLock)
                        {
                            ProgressChanged?.Invoke(total == 0 ? 1d : (double)completedCount / total);
                        }

                        semaphore.Release();
                    }
                });

                await Task.WhenAll(downloadTasks);

                if (failedRequiredDownloads.Count > 0)
                {
                    throw new Exception($"Nepodařilo se stáhnout {failedRequiredDownloads.Count} povinných souborů modpacku. Např.: {string.Join(", ", failedRequiredDownloads.Take(3))}");
                }

                if (failedOptionalDownloads.Count > 0)
                {
                    debugLog.WriteLine($"Optional download failures: {string.Join(", ", failedOptionalDownloads)}");
                    completionStatus = $"Instalace dokončena s omezeními ({failedOptionalDownloads.Count} volitelných souborů nešlo stáhnout).";
                }

                // 4. Extract Overrides (Configs, Scripts, etc.)
                // Smart Config Update: compare hashes to detect modpack-changed configs
                StatusChanged?.Invoke("Aplikuji nastavení (overrides)...");
                var overridesPath = (manifest.Overrides ?? "overrides").Replace('\\', '/');
                var overridesPrefix = overridesPath + "/";
                
                debugLog.WriteLine($"Overrides Logic: Path='{overridesPath}', Prefix='{overridesPrefix}'");
                
                // Load previous config hashes for smart config update
                var configHashes = LoadConfigHashes(installPath);
                var newConfigHashes = new Dictionary<string, string>();
                
                int extractedCount = 0;
                foreach (var entry in archive.Entries)
                {
                    // Normalize entry path to forward slashes for comparison
                    var entryFullName = entry.FullName.Replace('\\', '/');
                    
                    if (extractedCount < 20) debugLog.WriteLine($"ZIP Entry Sample: {entryFullName}");

                    if (entryFullName.StartsWith(overridesPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        // Remove prefix
                        var relativePath = entryFullName.Substring(overridesPrefix.Length);
                        
                        // If empty (just the folder itself), skip
                        if (string.IsNullOrEmpty(relativePath)) continue;

                        var targetPath = Path.Combine(installPath, relativePath);

                        // If it is a directory entry (ends with /)
                        if (entryFullName.EndsWith("/"))
                        {
                            debugLog.WriteLine($"Creating Directory: {targetPath}");
                            Directory.CreateDirectory(targetPath);
                            continue;
                        }

                        // It is a file
                        debugLog.WriteLine($"MATCH Overrides File: {entryFullName}");

                        var normalizedPath = relativePath.Replace('\\', '/').ToLowerInvariant();
                        bool isConfigFile = normalizedPath.StartsWith("config/");

                        if (isConfigFile)
                        {
                            // Smart config update: hash-based comparison
                            byte[] fileData;
                            using (var entryStream = entry.Open())
                            using (var ms = new MemoryStream())
                            {
                                await entryStream.CopyToAsync(ms);
                                fileData = ms.ToArray();
                            }
                            
                            var incomingHash = ComputeHashFromBytes(fileData);
                            newConfigHashes[relativePath] = incomingHash;
                            
                            if (File.Exists(targetPath))
                            {
                                if (configHashes.TryGetValue(relativePath, out var prevHash) && prevHash == incomingHash)
                                {
                                    // Modpack author didn't change this file → preserve user's version
                                    debugLog.WriteLine($"CONFIG SKIP (unchanged): {relativePath}");
                                    continue;
                                }
                                debugLog.WriteLine($"CONFIG UPDATE (hash changed): {relativePath}");
                            }
                            else
                            {
                                debugLog.WriteLine($"CONFIG NEW: {relativePath}");
                            }
                            
                            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                            await File.WriteAllBytesAsync(targetPath, fileData);
                            extractedCount++;
                            continue;
                        }

                        // PROTECTED PATHS check (options.txt, saves/, shaderpacks/ etc.)
                        if (IsProtected(relativePath))
                        {
                            // If file exists, SKIPPING to preserve user data
                            if (File.Exists(targetPath)) continue;
                        }
                        
                        // For non-protected files (like mods, scripts), we should OVERWRITE
                        // to ensure the modpack is up to date.
                        
                        // Ensure directory exists (in case directory entry was missing)
                        Directory.CreateDirectory(Path.GetDirectoryName(targetPath));

                        bool extracted = false;
                        for (int attempt = 1; attempt <= 3 && !extracted; attempt++)
                        {
                            try
                            {
                                entry.ExtractToFile(targetPath, overwrite: true);
                                extracted = true;
                            }
                            catch (IOException ioEx)
                            {
                                if (attempt < 3)
                                {
                                    await Task.Delay(250);
                                    continue;
                                }

                                // Locked/used file should not fail the whole update
                                StatusChanged?.Invoke($"Soubor je používán, přeskakuji: {relativePath}");
                                debugLog.WriteLine($"SKIP Locked Override: {relativePath} | {ioEx.Message}");
                            }
                        }

                        if (extracted)
                        {
                            extractedCount++;
                        }
                    }
                }
                
                // Save updated config hashes
                foreach (var kv in newConfigHashes)
                    configHashes[kv.Key] = kv.Value;
                SaveConfigHashes(installPath, configHashes);
            }
            
            // Save manifest info to instance folder for future launches
            var manifestInfoPath = Path.Combine(installPath, "manifest_info.json");
            var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
            await File.WriteAllTextAsync(manifestInfoPath, JsonSerializer.Serialize(manifestInfo, jsonOptions));
            
            StatusChanged?.Invoke(completionStatus);
            return manifestInfo;
        }
        
        /// <summary>
        /// Load cached manifest info from instance folder (for when modpack is already installed)
        /// </summary>
        public static ModpackManifestInfo LoadManifestInfo(string instancePath)
        {
            var manifestInfoPath = Path.Combine(instancePath, "manifest_info.json");
            if (File.Exists(manifestInfoPath))
            {
                try
                {
                    var json = File.ReadAllText(manifestInfoPath);
                    return JsonSerializer.Deserialize<ModpackManifestInfo>(json) ?? new ModpackManifestInfo();
                }
                catch { }
            }
            return null;
        }

        private async Task DownloadFileAsync(string url, string path)
        {
            var directoryPath = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            var tempPath = path + ".download";
            Exception? lastException = null;

            for (int attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    TryDeleteFile(tempPath);

                    using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();

                    await using var sourceStream = await response.Content.ReadAsStreamAsync();
                    await using var targetStream = File.Create(tempPath);
                    await sourceStream.CopyToAsync(targetStream);
                    await targetStream.FlushAsync();

                    TryDeleteFile(path);
                    File.Move(tempPath, path, true);
                    return;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    TryDeleteFile(tempPath);

                    if (attempt < 3)
                    {
                        await Task.Delay(350 * attempt);
                    }
                }
            }

            throw lastException ?? new IOException($"Download selhal: {url}");
        }

        private async Task DownloadCurseFileWithFallbackAsync(ResolvedCurseManifestFile resolvedFile, string destinationPath, Action<string>? debugLogAction)
        {
            var initialCandidates = BuildCurseDownloadCandidates(
                resolvedFile.File.Id,
                resolvedFile.File.FileName,
                resolvedFile.File.DownloadUrl);

            if (await TryDownloadCandidatesAsync(
                    initialCandidates,
                    destinationPath,
                    path => ValidateDownloadedCurseFile(path, resolvedFile.File, debugLogAction),
                    debugLogAction))
            {
                return;
            }

            var apiCandidates = await GetCurseApiFallbackCandidatesAsync(resolvedFile, debugLogAction);
            if (await TryDownloadCandidatesAsync(
                    apiCandidates,
                    destinationPath,
                    path => ValidateDownloadedCurseFile(path, resolvedFile.File, debugLogAction),
                    debugLogAction))
            {
                return;
            }

            throw new Exception($"Nepodařilo se stáhnout {resolvedFile.File.FileName} z žádného dostupného zdroje.");
        }

        private async Task DownloadModrinthFileWithFallbackAsync(ModrinthFile file, string destinationPath, Action<string>? debugLogAction)
        {
            if (await TryDownloadCandidatesAsync(
                    file.Downloads ?? new List<string>(),
                    destinationPath,
                    path => ValidateDownloadedModrinthFile(path, file, debugLogAction),
                    debugLogAction))
            {
                return;
            }

            throw new Exception($"Nepodařilo se stáhnout {file.Path} z žádného dostupného Modrinth mirroru.");
        }

        private async Task<bool> TryDownloadCandidatesAsync(IEnumerable<string> candidateUrls, string destinationPath, Func<string, bool> validateDownloadedFile, Action<string>? debugLogAction)
        {
            foreach (var candidateUrl in candidateUrls
                         .Where(url => !string.IsNullOrWhiteSpace(url))
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    debugLogAction?.Invoke($"DOWNLOAD TRY: {candidateUrl}");
                    await DownloadFileAsync(candidateUrl, destinationPath);

                    if (validateDownloadedFile(destinationPath))
                    {
                        debugLogAction?.Invoke($"DOWNLOAD OK: {candidateUrl}");
                        return true;
                    }

                    debugLogAction?.Invoke($"DOWNLOAD INVALID: {candidateUrl}");
                }
                catch (Exception ex)
                {
                    debugLogAction?.Invoke($"DOWNLOAD FAIL: {candidateUrl} | {ex.Message}");
                }

                TryDeleteFile(destinationPath);
            }

            return false;
        }

        private async Task<List<string>> GetCurseApiFallbackCandidatesAsync(ResolvedCurseManifestFile resolvedFile, Action<string>? debugLogAction)
        {
            var fallbackCandidates = new List<string>();

            try
            {
                var refreshedUrl = await _api.GetFileDownloadUrlAsync(resolvedFile.Manifest.ProjectID, resolvedFile.Manifest.FileID);
                AddDownloadCandidate(fallbackCandidates, refreshedUrl);
            }
            catch (Exception ex)
            {
                debugLogAction?.Invoke($"DOWNLOAD REFRESH FAIL: {resolvedFile.Manifest.ProjectID}:{resolvedFile.Manifest.FileID} | {ex.Message}");
            }

            try
            {
                var fileJson = await _api.GetModFileAsync(resolvedFile.Manifest.ProjectID, resolvedFile.Manifest.FileID);
                var fileData = JsonSerializer.Deserialize<CurseSingleFileData>(fileJson);
                AddDownloadCandidate(fallbackCandidates, fileData?.Data?.DownloadUrl);
                foreach (var candidate in BuildCurseDownloadCandidates(resolvedFile.File.Id, fileData?.Data?.FileName ?? resolvedFile.File.FileName))
                {
                    AddDownloadCandidate(fallbackCandidates, candidate);
                }
            }
            catch (Exception ex)
            {
                debugLogAction?.Invoke($"DOWNLOAD API FALLBACK FAIL: {resolvedFile.Manifest.ProjectID}:{resolvedFile.Manifest.FileID} | {ex.Message}");
            }

            foreach (var candidate in BuildCurseDownloadCandidates(resolvedFile.File.Id, resolvedFile.File.FileName))
            {
                AddDownloadCandidate(fallbackCandidates, candidate);
            }

            return fallbackCandidates;
        }

        private static IEnumerable<string> BuildCurseDownloadCandidates(int fileId, string? fileName, params string?[] directUrls)
        {
            foreach (var directUrl in directUrls)
            {
                if (!string.IsNullOrWhiteSpace(directUrl))
                {
                    yield return directUrl;
                }
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                yield break;
            }

            var fileIdString = fileId.ToString();
            if (fileIdString.Length < 5)
            {
                yield break;
            }

            var part1 = fileIdString.Substring(0, 4);
            var part2 = fileIdString.Substring(4);
            yield return $"https://edge.forgecdn.net/files/{part1}/{part2}/{fileName}";
            yield return $"https://mediafilez.forgecdn.net/files/{part1}/{part2}/{fileName}";
            yield return $"https://mediafiles.forgecdn.net/files/{part1}/{part2}/{fileName}";
        }

        private static void AddDownloadCandidate(ICollection<string> candidates, string? url)
        {
            if (!string.IsNullOrWhiteSpace(url))
            {
                candidates.Add(url);
            }
        }

        private static bool ValidateDownloadedCurseFile(string path, CurseFile file, Action<string>? debugLogAction)
        {
            if (!File.Exists(path))
            {
                return false;
            }

            var fileInfo = new FileInfo(path);
            if (file.FileLength > 0 && fileInfo.Length != file.FileLength)
            {
                debugLogAction?.Invoke($"FILE SIZE MISMATCH: {path} expected={file.FileLength} actual={fileInfo.Length}");
                return false;
            }

            var sha1Hash = file.Hashes?.FirstOrDefault(hash => hash.Algo == 1)?.Value;
            if (!string.IsNullOrWhiteSpace(sha1Hash) && !ValidateFileHash(path, "SHA1", sha1Hash, debugLogAction))
            {
                return false;
            }

            var md5Hash = file.Hashes?.FirstOrDefault(hash => hash.Algo == 2)?.Value;
            if (!string.IsNullOrWhiteSpace(md5Hash) && !ValidateFileHash(path, "MD5", md5Hash, debugLogAction))
            {
                return false;
            }

            return true;
        }

        private static bool ValidateDownloadedModrinthFile(string path, ModrinthFile file, Action<string>? debugLogAction)
        {
            if (!File.Exists(path))
            {
                return false;
            }

            var fileInfo = new FileInfo(path);
            if (file.FileSize > 0 && fileInfo.Length != file.FileSize)
            {
                debugLogAction?.Invoke($"MRPACK SIZE MISMATCH: {path} expected={file.FileSize} actual={fileInfo.Length}");
                return false;
            }

            if (file.Hashes != null)
            {
                if (file.Hashes.TryGetValue("sha1", out var sha1Hash) && !ValidateFileHash(path, "SHA1", sha1Hash, debugLogAction))
                {
                    return false;
                }

                if (file.Hashes.TryGetValue("sha512", out var sha512Hash) && !ValidateFileHash(path, "SHA512", sha512Hash, debugLogAction))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ValidateFileHash(string path, string algorithm, string expectedHash, Action<string>? debugLogAction)
        {
            if (string.IsNullOrWhiteSpace(expectedHash))
            {
                return true;
            }

            string actualHash;
            using (var stream = File.OpenRead(path))
            {
                actualHash = algorithm.ToUpperInvariant() switch
                {
                    "SHA1" => Convert.ToHexString(SHA1.HashData(stream)),
                    "MD5" => Convert.ToHexString(MD5.HashData(stream)),
                    "SHA512" => Convert.ToHexString(SHA512.HashData(stream)),
                    _ => string.Empty
                };
            }

            if (string.IsNullOrWhiteSpace(actualHash))
            {
                return true;
            }

            var matches = string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase);
            if (!matches)
            {
                debugLogAction?.Invoke($"FILE HASH MISMATCH: {path} algorithm={algorithm} expected={expectedHash} actual={actualHash}");
            }

            return matches;
        }

        private static bool TryDeleteFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return false;
            }

            try
            {
                File.Delete(path);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Backs up user-modified config files before update.
        /// Returns the backup directory path.
        /// </summary>
        public static string BackupUserConfigs(string installPath, string? backupRoot = null)
        {
            var snapshotRoot = backupRoot ?? installPath;
            Directory.CreateDirectory(snapshotRoot);

            var backupDir = Path.Combine(snapshotRoot, ".config_backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            var protectedPaths = new[] { "config", "saves", "shaderpacks", "options.txt", "servers.dat" };
            
            foreach (var rel in protectedPaths)
            {
                var src = Path.Combine(installPath, rel);
                var dst = Path.Combine(backupDir, rel);
                
                if (File.Exists(src))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
                    File.Copy(src, dst, true);
                }
                else if (Directory.Exists(src))
                {
                    CopyDirectoryRecursive(src, dst);
                }
            }

            return backupDir;
        }

        /// <summary>
        /// Restores user-modified config files after update, keeping modpack defaults for new files.
        /// </summary>
        public static void RestoreUserConfigs(string backupDir, string installPath)
        {
            if (!Directory.Exists(backupDir)) return;

            foreach (var file in Directory.GetFiles(backupDir, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(backupDir, file);
                var targetPath = Path.Combine(installPath, relativePath);
                
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                File.Copy(file, targetPath, true);
            }

            // Clean up backup
            try { Directory.Delete(backupDir, true); } catch { }
        }

        private static void CopyDirectoryRecursive(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);
            foreach (var file in Directory.GetFiles(sourceDir))
                File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)), true);
            foreach (var dir in Directory.GetDirectories(sourceDir))
                CopyDirectoryRecursive(dir, Path.Combine(targetDir, Path.GetFileName(dir)));
        }

        private static string ComputeHashFromBytes(byte[] data)
        {
            var hash = SHA256.HashData(data);
            return Convert.ToHexString(hash);
        }

        private static Dictionary<string, string> LoadConfigHashes(string installPath)
        {
            var path = Path.Combine(installPath, "config_hashes.json");
            if (File.Exists(path))
            {
                try
                {
                    var json = File.ReadAllText(path);
                    return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
                }
                catch { }
            }
            return new();
        }

        private static void SaveConfigHashes(string installPath, Dictionary<string, string> hashes)
        {
            var path = Path.Combine(installPath, "config_hashes.json");
            File.WriteAllText(path, JsonSerializer.Serialize(hashes, new JsonSerializerOptions { WriteIndented = true }));
        }

        private bool IsProtected(string relativePath)
        {
            // Normalize path separators
            var path = relativePath.Replace('\\', '/').ToLowerInvariant();

            if (path == "options.txt") return true;
            if (path == "servers.dat") return true;
            if (path.StartsWith("saves/")) return true;
            if (path.StartsWith("shaderpacks/")) return true;
            // config/ is handled separately by hash-based smart update
            
            return false;
        }
        private async Task<ModpackManifestInfo> InstallModrinthAsync(ZipArchive archive, string installPath, Action<string> statusCallback, Action<double> progressCallback, string? targetVersion)
        {
            var indexEntry = archive.GetEntry("modrinth.index.json");
            if (indexEntry == null) throw new Exception("Neplatný Modrinth balíček: chybí modrinth.index.json");

            ModrinthIndex index;
            using (var stream = indexEntry.Open())
            {
                index = await JsonSerializer.DeserializeAsync<ModrinthIndex>(stream);
            }

            statusCallback?.Invoke($"Instaluji Modrinth balíček: {index.Name}...");

            // Extract manifest info from dependencies
            var manifestInfo = new ModpackManifestInfo
            {
                PackName = index.Name ?? string.Empty,
                ModCount = index.Files?.Count ?? 0,
                Version = targetVersion ?? ""
            };
            
            // Parse dependencies (e.g. "minecraft": "1.20.1", "fabric-loader": "0.14.21")
            if (index.Dependencies != null)
            {
                if (index.Dependencies.TryGetValue("minecraft", out var mcVersion))
                    manifestInfo.MinecraftVersion = mcVersion;
                if (index.Dependencies.TryGetValue("fabric-loader", out var fabricVersion))
                {
                    manifestInfo.ModLoaderType = "fabric";
                    manifestInfo.ModLoaderId = $"fabric-{fabricVersion}";
                }
                else if (index.Dependencies.TryGetValue("quilt-loader", out var quiltVersion))
                {
                    manifestInfo.ModLoaderType = "quilt";
                    manifestInfo.ModLoaderId = $"quilt-{quiltVersion}";
                }
                else if (index.Dependencies.TryGetValue("forge", out var forgeVersion))
                {
                    manifestInfo.ModLoaderType = "forge";
                    manifestInfo.ModLoaderId = $"forge-{forgeVersion}";
                }
                else if (index.Dependencies.TryGetValue("neoforge", out var neoforgeVersion))
                {
                    manifestInfo.ModLoaderType = "neoforge";
                    manifestInfo.ModLoaderId = $"neoforge-{neoforgeVersion}";
                }
            }

            // Download Files
            int started = 0;
            int completed = 0;
            int total = index.Files?.Count ?? 0;

            object progressLock = new object();
            object failureLock = new object();
            var semaphore = new SemaphoreSlim(10);
            var failedDownloads = new List<string>();

            var downloadTasks = (index.Files ?? new List<ModrinthFile>()).Select(async file =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var targetPath = Path.Combine(installPath, file.Path);
                    
                    if (File.Exists(targetPath))
                    {
                        if (ValidateDownloadedModrinthFile(targetPath, file, message => LogService.Log(message, "MRPACK")))
                        {
                            return;
                        }

                        TryDeleteFile(targetPath);
                    }

                    var startedIndex = Interlocked.Increment(ref started);
                    lock (progressLock)
                    {
                        statusCallback?.Invoke($"Stahuji ({startedIndex}/{total}): {Path.GetFileName(file.Path)}");
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
                    await DownloadModrinthFileWithFallbackAsync(file, targetPath, message => LogService.Log(message, "MRPACK"));
                }
                catch (Exception ex)
                {
                    lock (failureLock)
                    {
                        failedDownloads.Add(Path.GetFileName(file.Path));
                    }

                    LogService.Error($"[Modrinth Download] Failed: {file.Path}", ex);
                }
                finally
                {
                    var completedCount = Interlocked.Increment(ref completed);
                    lock (progressLock)
                    {
                        progressCallback?.Invoke(total == 0 ? 1d : (double)completedCount / total);
                    }

                    semaphore.Release();
                }
            });

            await Task.WhenAll(downloadTasks);

            if (failedDownloads.Count > 0)
            {
                throw new Exception($"Nepodařilo se stáhnout {failedDownloads.Count} souborů Modrinth balíčku. Např.: {string.Join(", ", failedDownloads.Take(3))}");
            }

            // Extract Overrides with smart config update
            var mrConfigHashes = LoadConfigHashes(installPath);
            var mrNewConfigHashes = new Dictionary<string, string>();
            
            foreach (var entry in archive.Entries)
            {
                if (entry.FullName.StartsWith("overrides/") && !entry.FullName.EndsWith("/"))
                {
                    var relativePath = entry.FullName.Substring("overrides/".Length);
                    var targetPath = Path.Combine(installPath, relativePath);
                    
                    var normalizedPath = relativePath.Replace('\\', '/').ToLowerInvariant();
                    bool isConfigFile = normalizedPath.StartsWith("config/");
                    
                    if (isConfigFile)
                    {
                        // Smart config update: hash-based comparison
                        byte[] fileData;
                        using (var entryStream = entry.Open())
                        using (var ms = new MemoryStream())
                        {
                            await entryStream.CopyToAsync(ms);
                            fileData = ms.ToArray();
                        }
                        
                        var incomingHash = ComputeHashFromBytes(fileData);
                        mrNewConfigHashes[relativePath] = incomingHash;
                        
                        if (File.Exists(targetPath))
                        {
                            if (mrConfigHashes.TryGetValue(relativePath, out var prevHash) && prevHash == incomingHash)
                            {
                                // Modpack author didn't change this file → preserve user's version
                                continue;
                            }
                        }
                        
                        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                        await File.WriteAllBytesAsync(targetPath, fileData);
                        continue;
                    }
                    
                    if (IsProtected(relativePath) && File.Exists(targetPath)) continue;
                    
                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath));

                    bool extracted = false;
                    for (int attempt = 1; attempt <= 3 && !extracted; attempt++)
                    {
                        try
                        {
                            entry.ExtractToFile(targetPath, overwrite: true);
                            extracted = true;
                        }
                        catch (IOException ioEx)
                        {
                            if (attempt < 3)
                            {
                                await Task.Delay(250);
                                continue;
                            }

                            statusCallback?.Invoke($"Soubor je používán, přeskakuji: {relativePath}");
                            LogService.Error($"[Modrinth Overrides] Locked file skipped: {relativePath}", ioEx);
                        }
                    }
                }
            }
            
            // Save updated config hashes
            foreach (var kv in mrNewConfigHashes)
                mrConfigHashes[kv.Key] = kv.Value;
            SaveConfigHashes(installPath, mrConfigHashes);
            
            // Save manifest info
            var manifestInfoPath = Path.Combine(installPath, "manifest_info.json");
            var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
            await File.WriteAllTextAsync(manifestInfoPath, JsonSerializer.Serialize(manifestInfo, jsonOptions));
            
            statusCallback?.Invoke("Instalace dokončena!");
            return manifestInfo;
        }
    }

    // Modrinth DTOs
    public class ModrinthIndex
    {
        [System.Text.Json.Serialization.JsonPropertyName("name")]
        public string Name { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("dependencies")]
        public Dictionary<string, string> Dependencies { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("files")]
        public List<ModrinthFile> Files { get; set; }
    }

    public class ModrinthFile
    {
        [System.Text.Json.Serialization.JsonPropertyName("path")]
        public string Path { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("downloads")]
        public List<string> Downloads { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("hashes")]
        public Dictionary<string, string> Hashes { get; set; } = new();

        [System.Text.Json.Serialization.JsonPropertyName("fileSize")]
        public long FileSize { get; set; }
    }

    internal sealed class ResolvedCurseManifestFile
    {
        public ResolvedCurseManifestFile(ManifestFile manifest, CurseFile file)
        {
            Manifest = manifest;
            File = file;
        }

        public ManifestFile Manifest { get; }

        public CurseFile File { get; }
    }
}
