using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.ProcessBuilder;
using CmlLib.Core.Installer.Forge;
using CmlLib.Core.Installer.NeoForge;
using CmlLib.Core.Installers;
using System.Threading.Tasks;
using System.IO;
using System;
using System.Diagnostics;
using System.Linq;
using VoidCraftLauncher.Models;

namespace VoidCraftLauncher.Services
{
    public class LauncherService
    {
        private MinecraftLauncher _launcher;
        private readonly string _basePath;
        private readonly string _sharedPath;
        private readonly string _instancesPath;
        private readonly MinecraftPath _mcPath;
        private Process _gameProcess;
        private readonly string _configPath;

        public bool IsGameRunning => _gameProcess != null && !_gameProcess.HasExited;

        public LauncherService()
        {
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            _basePath = Path.Combine(documents, ".voidcraft");
            _sharedPath = Path.Combine(_basePath, "shared");
            _instancesPath = Path.Combine(_basePath, "instances");
            _configPath = Path.Combine(_basePath, "launcher_config.json");
            
            // Create folder structure
            Directory.CreateDirectory(_sharedPath);
            Directory.CreateDirectory(_instancesPath);
            
            // MinecraftPath points to shared folder - assets, versions, libraries are shared
            _mcPath = new MinecraftPath(_sharedPath);
            _launcher = new MinecraftLauncher(_mcPath);
        }

        public MinecraftPath GetPath() => _mcPath;
        public string BasePath => _basePath;
        public string SharedPath => _sharedPath;
        public string InstancesPath => _instancesPath;

        public LauncherConfig LoadConfig()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var json = File.ReadAllText(_configPath);
                    return System.Text.Json.JsonSerializer.Deserialize<LauncherConfig>(json) ?? new LauncherConfig();
                }
            }
            catch (Exception ex)
            { 
                LogService.Error("Failed to load config", ex);
            }
            
            return new LauncherConfig();
        }

        public void SaveConfig(LauncherConfig config)
        {
            try
            {
                var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                var json = System.Text.Json.JsonSerializer.Serialize(config, options);
                Directory.CreateDirectory(_basePath);
                File.WriteAllText(_configPath, json);
            }
            catch (Exception ex)
            { 
                LogService.Error("Failed to save config", ex);
            }
        }

        /// <summary>
        /// Launch Minecraft with detailed progress reporting
        /// </summary>
        /// <param name="gameDirectory">Instance folder for mods/config/saves</param>
        /// <param name="modLoaderId">Optional mod loader ID from manifest (e.g. "neoforge-21.1.90" or "forge-47.2.0")</param>
        public async Task<Process> LaunchAsync(
            string versionId, 
            MSession session, 
            LauncherConfig config,
            string gameDirectory,
            string modLoaderId,
            string[]? jvmArguments,
            Action<string> statusCallback,
            Action<double> progressCallback,
            Action<string> fileCallback)
        {
            statusCallback("Kontroluji verzi...");
            progressCallback(0);
            
            // Hook up progress events (CmlLib 4.x API)
            _launcher.FileProgressChanged += (sender, args) =>
            {
                fileCallback($"{args.Name}");
                if (args.TotalTasks > 0)
                {
                    progressCallback((double)args.ProgressedTasks / args.TotalTasks * 100);
                }
            };
            
            _launcher.ByteProgressChanged += (sender, args) =>
            {
                if (args.TotalBytes > 0)
                {
                    progressCallback((double)args.ProgressedBytes / args.TotalBytes * 100);
                }
            };

            string launchVersionName = versionId;
            
            // Install mod loader if specified (Forge/NeoForge/Fabric)
            if (!string.IsNullOrEmpty(modLoaderId))
            {
                statusCallback($"Instaluji mod loader: {modLoaderId}...");
                
                // Parse modLoaderId - format is "neoforge-21.1.90" or "forge-47.2.0"
                var parts = modLoaderId.Split('-', 2);
                if (parts.Length == 2)
                {
                    var loaderType = parts[0].ToLowerInvariant(); // "neoforge", "forge", "fabric"
                    var loaderVersion = parts[1]; // "21.1.90", "47.2.0"
                    
                    if (loaderType == "neoforge")
                    {
                        try
                        {
                            var neoforgeInstaller = new NeoForgeInstaller(_launcher);
                            // NeoForgeInstaller might not support the same options class or I have the wrong name.
                            // Calling without options for now to fix build.
                            launchVersionName = await neoforgeInstaller.Install(versionId, loaderVersion);
                             statusCallback($"NeoForge nainstalován: {launchVersionName}");
                        }
                        catch (Exception ex)
                        {
                            statusCallback($"Chyba instalace NeoForge: {ex.Message}");
                             throw new Exception($"Instalace NeoForge selhala: {ex.Message}", ex);
                        }
                    }
                    else if (loaderType == "forge")
                    {
                        try
                        {
                            var forgeInstaller = new ForgeInstaller(_launcher);
                            
                            launchVersionName = await forgeInstaller.Install(versionId, loaderVersion, new ForgeInstallOptions 
                            {
                                InstallerOutput = new SyncProgress<string>(msg => 
                                {
                                    Debug.WriteLine($"[ForgeInstaller] {msg}");
                                })
                            });
                            statusCallback($"Forge nainstalován: {launchVersionName}");
                        }
                        catch (Exception ex)
                        {
                            statusCallback($"Chyba instalace Forge: {ex.Message}");
                            throw new Exception($"Instalace Forge selhala: {ex.Message}", ex);
                        }
                    }
                    /* 
                    // Fabric Support - TODO: Fix CmlLib.Core.Installer.Fabric dependency
                    else if (loaderType == "fabric")
                    {
                        try
                        {
                            // var fabricInstaller = new FabricInstaller(_launcher);
                            // launchVersionName = await fabricInstaller.Install(versionId, loaderVersion);
                            // statusCallback($"Fabric nainstalován: {launchVersionName}");
                            statusCallback("Fabric auto-install není momentálně podporován. Prosím nainstalujte Fabric manuálně.");
                        }
                        catch (Exception ex)
                        {
                            statusCallback($"Chyba instalace Fabric: {ex.Message}");
                             // throw new Exception($"Instalace Fabric selhala: {ex.Message}", ex);
                        }
                    }
                    */
                } // End if parts.Length == 2
            }
            
            // Step 2: Ensure all files (Java, Assets, Libs) are installed for the target version
            // This is critical for NeoForge which might need a specific Java Runtime (e.g. Java 21)
            statusCallback($"Ověřuji soubory pro verzi: {launchVersionName}...");
            await _launcher.InstallAsync(launchVersionName);
            
            statusCallback("Připravuji spuštění...");

            // Create a custom MinecraftPath for this launch
            // BasePath = Instance Folder (modpackDir) -> This becomes --gameDir
            // Assets/Libs/Versions/Runtime = Shared Folder -> Saves space
            var launchPath = new MinecraftPath(gameDirectory);
            launchPath.Assets = _mcPath.Assets;
            launchPath.Library = _mcPath.Library;
            launchPath.Runtime = _mcPath.Runtime;
            launchPath.Versions = _mcPath.Versions;

            var launchLauncher = new MinecraftLauncher(launchPath);
            
            var launchOption = new MLaunchOption
            {
                Session = session,
                MaximumRamMb = config.MaxRamMb,
                MinimumRamMb = config.MinRamMb,
                GameLauncherName = "VoidCraftLauncher",
                GameLauncherVersion = "2.0",
                // Do NOT add --gameDir manually here, CmlLib adds it based on launchPath.BasePath
                ExtraJvmArguments = (jvmArguments ?? Array.Empty<string>())
                    .Select(arg => new MArgument(arg))
                    .ToArray()
            };

            // Set Java Path if configured
            if (!string.IsNullOrEmpty(config.JavaPath))
            {
                launchOption.JavaPath = config.JavaPath;
            }

            _gameProcess = await launchLauncher.BuildProcessAsync(launchVersionName, launchOption);

            statusCallback("Spouštím hru...");
            progressCallback(100);
            // Process is returned unstarted so MainViewModel can configure redirects
            
            return _gameProcess;
        }

        /// <summary>
        /// Stop the running game
        /// </summary>
        public void StopGame()
        {
            if (_gameProcess != null && !_gameProcess.HasExited)
            {
                _gameProcess.Kill();
                _gameProcess = null;
            }
        }

        public string GetModpackPath(string modpackName)
        {
            var path = Path.Combine(_instancesPath, modpackName);
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
