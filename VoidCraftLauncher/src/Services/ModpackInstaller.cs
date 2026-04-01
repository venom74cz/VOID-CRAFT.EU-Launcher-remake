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
                
                // Extract manifest info for launcher
                manifestInfo.PackName = manifest.Name ?? string.Empty;
                manifestInfo.Author = manifest.Author ?? string.Empty;
                manifestInfo.MinecraftVersion = manifest.Minecraft?.Version ?? "1.21.1";
                manifestInfo.ModCount = manifest.Files?.Count ?? 0;
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
                debugLog.WriteLine($"Manifest Files Count: {manifest.Files?.Count}");

                StatusChanged?.Invoke($"Načítám informace o modech ({manifest.Files.Count})...");


                // 2. Resolve URLs for mods - CHUNKED to avoid API limits
                var fileIds = manifest.Files.Select(f => f.FileID).Distinct().ToList();
                var curseFiles = new List<CurseFile>();
                
                for (int i = 0; i < fileIds.Count; i += 40)
                {
                    var chunk = fileIds.Skip(i).Take(40);
                    var filesJson = await _api.GetFilesAsync(chunk);
                    var curseFilesData = JsonSerializer.Deserialize<CurseFileDatas>(filesJson);
                    if (curseFilesData?.Data != null) curseFiles.AddRange(curseFilesData.Data);
                }

                // 2b. RESOLVE CATEGORIES - CHUNKED
                var modIds = curseFiles.Select(f => f.ModId).Distinct().ToList();
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
                            var filesForMod = curseFiles.Where(f => f.ModId == m.Id);
                            foreach(var f in filesForMod)
                            {
                                metadataList.RemoveAll(x => x.FileName == f.FileName);
                                metadataList.Add(new ModMetadata
                                {
                                    FileName = f.FileName,
                                    Name = m.Name,
                                    Slug = m.Slug,
                                    Summary = m.Summary,
                                    Categories = m.Categories?.Select(c => c.Name).ToList() ?? new List<string>(),
                                    IconUrl = m.Logo?.ThumbnailUrl,
                                    WebLink = m.Links?.WebsiteUrl
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
                
                var newModNames = curseFiles.Select(f => f.FileName).ToHashSet();
                
                foreach (var file in existingFiles)
                {
                    var fileName = Path.GetFileName(file);
                    // Remove .disabled suffix for comparison if present
                    var pureFileName = fileName.EndsWith(".disabled") 
                        ? fileName.Substring(0, fileName.Length - ".disabled".Length) 
                        : fileName;
                    
                    // If the pure filename is not in the new manifest...
                    if (!newModNames.Contains(pureFileName))
                    {
                        // ...AND we tracked it as installed previously -> it's an old version -> DELETE
                        if (previouslyInstalledFiles.Contains(pureFileName))
                        {
                            try { File.Delete(file); } catch {}
                            StatusChanged?.Invoke($"Odstraňuji starý soubor: {fileName}");
                        }
                    }
                }
                
                // Track files for next time
                await File.WriteAllTextAsync(installedFilesPath, JsonSerializer.Serialize(newModNames));

                // B. Download new/updated mods
                int current = 0;
                int total = curseFiles.Count;
                int downloaded = 0;
                int skipped = 0;
                
                object logLock = new object();
                object progressLock = new object();
                var semaphore = new SemaphoreSlim(10);
                
                var downloadTasks = curseFiles.Select(async mod => 
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        // Determine Target Directory
                        string targetDir = modsDir; // Default
                        if (modClassMap.TryGetValue(mod.ModId, out int classId))
                        {
                            if (classId == 12) targetDir = rpDir; // Resource Pack
                            else if (classId == 6552 || classId == 4546) targetDir = shaderDir; // Shader Pack
                        }

                        var destPath = Path.Combine(targetDir, mod.FileName);
                        var disabledPath = destPath + ".disabled";

                        // Check if file exists (enabled OR disabled)
                        if (File.Exists(destPath) || File.Exists(disabledPath))
                        {
                            Interlocked.Increment(ref skipped);
                            return;
                        }

                        lock (progressLock)
                        {
                            current++;
                            StatusChanged?.Invoke($"Stahuji ({current}/{total}): {mod.DisplayName}");
                            ProgressChanged?.Invoke((double)current / total);
                        }

                        // Get download URL - use CDN fallback if API returns null
                        var url = mod.DownloadUrl;
                        lock (logLock)
                        {
                            debugLog.WriteLine($"Processing Mod: {mod.FileName} (ID: {mod.Id}, Class: {classId}) - URL: {url}");
                        }

                        if (string.IsNullOrEmpty(url))
                        {
                            // CurseForge CDN pattern: https://edge.forgecdn.net/files/{id[0:4]}/{id[4:]}/{filename}
                            var idStr = mod.Id.ToString();
                            if (idStr.Length >= 4)
                            {
                                var part1 = idStr.Substring(0, 4);
                                var part2 = idStr.Substring(4);
                                url = $"https://edge.forgecdn.net/files/{part1}/{part2}/{mod.FileName}";
                            }
                        }
                        
                        if (!string.IsNullOrEmpty(url))
                        {
                            try 
                            {
                                await DownloadFileAsync(url, destPath);
                                Interlocked.Increment(ref downloaded);
                            }
                            catch (Exception ex)
                            {
                                lock (progressLock)
                                {
                                    StatusChanged?.Invoke($"Chyba stahování {mod.FileName}: {ex.Message}");
                                }
                            }
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                await Task.WhenAll(downloadTasks);

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
            
            StatusChanged?.Invoke("Instalace dokončena!");
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
            var data = await _httpClient.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(path, data);
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
            int current = 0;
            int total = index.Files?.Count ?? 0;

            object progressLock = new object();
            var semaphore = new SemaphoreSlim(10);

            var downloadTasks = (index.Files ?? new List<ModrinthFile>()).Select(async file =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var targetPath = Path.Combine(installPath, file.Path);
                    
                    // Skip if exists
                    if (File.Exists(targetPath)) return;

                    lock (progressLock)
                    {
                        current++;
                        statusCallback?.Invoke($"Stahuji ({current}/{total}): {Path.GetFileName(file.Path)}");
                        progressCallback?.Invoke((double)current / total);
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath));

                    if (file.Downloads?.Count > 0)
                    {
                        await DownloadFileAsync(file.Downloads[0], targetPath);
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(downloadTasks);

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
    }
}
