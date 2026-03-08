using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using CmlLib.Core;

namespace VoidCraftLauncher.Services
{
    /// <summary>
    /// Installs Fabric loader by fetching the version profile from Fabric Meta API
    /// and placing it in the shared versions directory for CmlLib to use.
    /// </summary>
    public class FabricInstaller
    {
        private static readonly HttpClient _httpClient = new();
        private readonly MinecraftLauncher _launcher;
        private readonly MinecraftPath _mcPath;

        private const string FABRIC_META_BASE = "https://meta.fabricmc.net/v2/versions";

        public FabricInstaller(MinecraftLauncher launcher, MinecraftPath mcPath)
        {
            _launcher = launcher;
            _mcPath = mcPath;
        }

        /// <summary>
        /// Install Fabric loader for a given Minecraft version and loader version.
        /// Returns the version name to use for launching (e.g. "fabric-loader-0.16.14-1.21.1").
        /// </summary>
        public async Task<string> InstallAsync(string mcVersion, string loaderVersion, Action<string>? statusCallback = null)
        {
            var versionName = $"fabric-loader-{loaderVersion}-{mcVersion}";
            var versionDir = Path.Combine(_mcPath.Versions, versionName);
            var versionJsonPath = Path.Combine(versionDir, $"{versionName}.json");

            // Skip if already installed
            if (File.Exists(versionJsonPath))
            {
                statusCallback?.Invoke($"Fabric {versionName} již nainstalován.");
                return versionName;
            }

            // 1. Fetch version profile JSON from Fabric Meta API
            statusCallback?.Invoke($"Stahuji Fabric profil: {versionName}...");
            var profileUrl = $"{FABRIC_META_BASE}/loader/{mcVersion}/{loaderVersion}/profile/json";
            
            string profileJson;
            try
            {
                profileJson = await _httpClient.GetStringAsync(profileUrl);
            }
            catch (HttpRequestException ex)
            {
                throw new Exception(
                    $"Nepodařilo se stáhnout Fabric profil pro MC {mcVersion} + loader {loaderVersion}. " +
                    $"Zkontrolujte verze na https://fabricmc.net/use/. Chyba: {ex.Message}", ex);
            }

            // 2. Parse to validate and extract library info
            var profileDoc = JsonDocument.Parse(profileJson);
            var root = profileDoc.RootElement;

            // 3. Save version JSON
            Directory.CreateDirectory(versionDir);
            await File.WriteAllTextAsync(versionJsonPath, profileJson);
            statusCallback?.Invoke($"Fabric profil uložen: {versionName}");

            // 4. Download libraries referenced in the profile
            if (root.TryGetProperty("libraries", out var libraries))
            {
                int total = libraries.GetArrayLength();
                int current = 0;

                foreach (var lib in libraries.EnumerateArray())
                {
                    current++;
                    var libName = lib.GetProperty("name").GetString() ?? "";
                    
                    // Parse Maven coordinate: group:artifact:version
                    var libPath = MavenToPath(libName);
                    if (string.IsNullOrEmpty(libPath)) continue;

                    var localPath = Path.Combine(_mcPath.Library, libPath);

                    // Skip if already exists
                    if (File.Exists(localPath))
                        continue;

                    // Get download URL
                    string? url = null;
                    if (lib.TryGetProperty("url", out var urlProp))
                    {
                        var baseUrl = urlProp.GetString()?.TrimEnd('/');
                        if (!string.IsNullOrEmpty(baseUrl))
                            url = $"{baseUrl}/{libPath}";
                    }

                    if (string.IsNullOrEmpty(url)) continue;

                    statusCallback?.Invoke($"Stahuji knihovnu ({current}/{total}): {Path.GetFileName(libPath)}");

                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
                        var data = await _httpClient.GetByteArrayAsync(url);
                        await File.WriteAllBytesAsync(localPath, data);
                    }
                    catch (Exception ex)
                    {
                        LogService.Error($"Failed to download Fabric library: {libName}", ex);
                        // Non-fatal: CmlLib will try to download missing libs during InstallAsync
                    }
                }
            }

            statusCallback?.Invoke($"Fabric {versionName} nainstalován.");
            return versionName;
        }

        /// <summary>
        /// Converts a Maven coordinate (group:artifact:version) to a relative file path.
        /// e.g. "net.fabricmc:fabric-loader:0.16.14" -> "net/fabricmc/fabric-loader/0.16.14/fabric-loader-0.16.14.jar"
        /// </summary>
        private static string MavenToPath(string mavenCoord)
        {
            var parts = mavenCoord.Split(':');
            if (parts.Length < 3) return "";

            var group = parts[0].Replace('.', Path.DirectorySeparatorChar);
            var artifact = parts[1];
            var version = parts[2];

            // Handle classifier if present (group:artifact:version:classifier)
            var classifier = parts.Length > 3 ? $"-{parts[3]}" : "";

            return Path.Combine(group, artifact, version, $"{artifact}-{version}{classifier}.jar");
        }

        /// <summary>
        /// List available Fabric loader versions for a given MC version.
        /// Returns version strings sorted by newest first.
        /// </summary>
        public static async Task<string[]> GetAvailableVersionsAsync(string mcVersion)
        {
            try
            {
                var url = $"{FABRIC_META_BASE}/loader/{mcVersion}";
                var json = await _httpClient.GetStringAsync(url);
                var doc = JsonDocument.Parse(json);
                
                return doc.RootElement
                    .EnumerateArray()
                    .Select(e => e.GetProperty("loader").GetProperty("version").GetString() ?? "")
                    .Where(v => !string.IsNullOrEmpty(v))
                    .ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }
    }
}
