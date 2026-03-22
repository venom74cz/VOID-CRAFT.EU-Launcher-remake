using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VoidCraftLauncher.Models;
using VoidCraftLauncher.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace VoidCraftLauncher.ViewModels;

/// <summary>
/// Game launching, JVM args, GTNH detection, process management.
/// </summary>
public partial class MainViewModel
{
    [RelayCommand]
    public async Task PlayModpack()
    {
        if (CurrentModpack == null || IsLaunching) return;

        try
        {
            IsLaunching = true;
            TargetModpack = CurrentModpack; // Track which modpack is being launched
            LaunchProgress = 0;
            LaunchStatus = "Připravuji...";

            var modpackDir = _launcherService.GetModpackPath(CurrentModpack.Name);
            var modsDir = Path.Combine(modpackDir, "mods");

            // Determine the version to install based on dropdown selection
            var selectedVersion = CurrentModpack.CurrentVersion;
            var selectedFileId = selectedVersion?.FileId ?? "0";
            Debug.WriteLine($"[PlayModpack] Selected version: {selectedVersion?.Name}, FileId: {selectedFileId}");

            // INSTALL / UPDATE
            if (CurrentModpack.IsCustomProfile)
            {
                LaunchStatus = "Vlastní profil – přeskočena kontrola aktualizací.";
            }
            else
            {
                LaunchStatus = "Kontroluji aktualizace...";
                // Use CurseForge for standard modpacks
                if (CurrentModpack.ProjectId > 0)
                {
                    var filesJson = await _curseForgeApi.GetModpackFilesAsync(CurrentModpack.ProjectId);
                    var filesNode = System.Text.Json.Nodes.JsonNode.Parse(filesJson);
                    var files = filesNode?["data"]?.AsArray();

                    if (files != null && files.Count > 0)
                    {
                        // Pick exact file ID if selected, else latest
                        var targetFile = files.FirstOrDefault(f => f?["id"]?.ToString() == selectedFileId)
                                         ?? files.OrderByDescending(f => f?["fileDate"]?.ToString()).FirstOrDefault();

                        if (targetFile != null)
                        {
                            var fileId = targetFile["id"]?.GetValue<int>() ?? 0;
                            var displayName = targetFile["displayName"]?.ToString() ?? "Unknown";
                            var downloadUrl = targetFile["downloadUrl"]?.ToString();

                            // Check if already installed by comparing manifest FileId
                            var installedManifest = ModpackInstaller.LoadManifestInfo(modpackDir);
                            if (installedManifest?.FileId == fileId && Directory.Exists(modsDir))
                            {
                                LaunchStatus = $"Máte nejnovější verzi ({displayName}).";
                            }
                            else
                            {
                                LaunchStatus = $"Stahuji {displayName}...";

                                // Download the zip/mrpack
                                if (string.IsNullOrEmpty(downloadUrl))
                                {
                                    // CurseForge sometimes hides download URL; try constructing
                                    downloadUrl = await _curseForgeApi.GetFileDownloadUrlAsync(CurrentModpack.ProjectId, fileId);
                                }

                                if (!string.IsNullOrEmpty(downloadUrl))
                                {
                                    var tempPath = Path.Combine(Path.GetTempPath(), $"voidcraft_{fileId}.zip");
                                    var data = await _httpClient.GetByteArrayAsync(downloadUrl);
                                    await File.WriteAllBytesAsync(tempPath, data);

                                    // Install
                                    LaunchStatus = "Instaluji...";
                                    _lastManifestInfo = await _modpackInstaller.InstallOrUpdateAsync(tempPath, modpackDir);

                                    // Update current version
                                    if (_lastManifestInfo != null)
                                    {
                                        CurrentModpack.CurrentVersion = new ModpackVersion
                                        {
                                            Name = displayName,
                                            FileId = fileId.ToString()
                                        };
                                        SaveModpacks();
                                    }

                                    // Cleanup temp
                                    if (File.Exists(tempPath)) File.Delete(tempPath);
                                }
                                else
                                {
                                    LaunchStatus = "Nepodařilo se získat URL ke stažení.";
                                    await Task.Delay(3000);
                                    IsLaunching = false;
                                    TargetModpack = null;
                                    return;
                                }
                            }

                            // Set manifest info for launch
                            _lastManifestInfo = ModpackInstaller.LoadManifestInfo(modpackDir) ?? new ModpackManifestInfo();
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(CurrentModpack.ModrinthId))
                {
                    // Modrinth modpack handling
                    LaunchStatus = "Kontroluji Modrinth aktualizace...";
                    var versionsJson = await _modrinthApi.GetProjectVersionsAsync(CurrentModpack.ModrinthId);
                    var versions = System.Text.Json.Nodes.JsonNode.Parse(versionsJson)?.AsArray();
                    
                    var targetVersion = versions?.FirstOrDefault(v => v?["id"]?.ToString() == selectedFileId)
                                        ?? versions?.FirstOrDefault(v => v?["version_type"]?.ToString() == "release")
                                        ?? versions?.FirstOrDefault();

                    if (targetVersion != null)
                    {
                        var versionId = targetVersion["id"]?.ToString() ?? "0";
                        var versionName = targetVersion["version_number"]?.ToString() ?? "Unknown";
                        
                        var installedManifest = ModpackInstaller.LoadManifestInfo(modpackDir);
                        if (installedManifest?.Version == versionName && Directory.Exists(modsDir))
                        {
                            LaunchStatus = $"Máte nejnovější verzi ({versionName}).";
                        }
                        else
                        {
                            var primaryFile = targetVersion["files"]?.AsArray()
                                ?.FirstOrDefault(f => f?["primary"]?.GetValue<bool>() == true)
                                ?? targetVersion["files"]?.AsArray()?.FirstOrDefault();

                            var downloadUrl = primaryFile?["url"]?.ToString();
                            var fileName = primaryFile?["filename"]?.ToString() ?? "modpack.mrpack";

                            if (!string.IsNullOrEmpty(downloadUrl))
                            {
                                LaunchStatus = $"Stahuji {versionName}...";
                                var tempPath = Path.Combine(Path.GetTempPath(), fileName);
                                var data = await _httpClient.GetByteArrayAsync(downloadUrl);
                                await File.WriteAllBytesAsync(tempPath, data);

                                LaunchStatus = "Instaluji...";
                                _lastManifestInfo = await _modpackInstaller.InstallOrUpdateAsync(tempPath, modpackDir);

                                if (_lastManifestInfo != null)
                                {
                                    CurrentModpack.CurrentVersion = new ModpackVersion
                                    {
                                        Name = versionName,
                                        FileId = versionId
                                    };
                                    SaveModpacks();
                                }

                                if (File.Exists(tempPath)) File.Delete(tempPath);
                            }
                        }

                        _lastManifestInfo = ModpackInstaller.LoadManifestInfo(modpackDir) ?? new ModpackManifestInfo();
                    }
                }
            }
            
            LaunchStatus = "Spouštím hru...";

            // Get Minecraft version from manifest (dynamically)
            var mcVersion = _lastManifestInfo?.MinecraftVersion ?? "1.21.1";
            var modLoaderId = _lastManifestInfo?.ModLoaderId ?? "";

            // DETECT JAVA VERSION FOR DYNAMIC FLAGS
            LaunchStatus = "Detekuji Javu...";
            
            int? requiredJava = null;

            var javaPath = await _launcherService.GetJavaPathAsync(_lastManifestInfo?.MinecraftVersion ?? mcVersion, requiredJava);
            int javaVersion = _launcherService.GetJavaVersion(javaPath ?? "");
            Debug.WriteLine($"[JavaDetection] Detected Java {javaVersion} at {javaPath}");
            
            var jvmArgs = new List<string>();

            // Check for instance-specific overrides or global config
            bool enableOptimizations = Config.InstanceOverrides.TryGetValue(CurrentModpack.Name, out var overrideConfig) 
                ? (overrideConfig.OverrideEnableOptimizationFlags ?? Config.EnableOptimizationFlags)
                : Config.EnableOptimizationFlags;
            
            if (enableOptimizations)
            {
                // General Optimizations (Safe for all)
                jvmArgs.Add("-XX:+UnlockExperimentalVMOptions");
                jvmArgs.Add("-XX:+DisableExplicitGC");
                jvmArgs.Add("-XX:+AlwaysPreTouch");
                jvmArgs.Add("-XX:+PerfDisableSharedMem");
                jvmArgs.Add("-XX:+UseNUMA");

                // Determine GC Type (Override > Global)
                var effectiveGc = overrideConfig?.OverrideGcType ?? Config.SelectedGc;

                if (effectiveGc == Models.GcType.ZGC)
                {
                    jvmArgs.Add("-XX:+UseZGC");
                    if (javaVersion >= 21)
                    {
                        jvmArgs.Add("-XX:+ZGenerational");
                    }
                }
                else if (effectiveGc == Models.GcType.G1GC)
                {
                    jvmArgs.Add("-XX:+UseG1GC");
                    jvmArgs.Add("-XX:+ParallelRefProcEnabled");
                    jvmArgs.Add("-XX:MaxGCPauseMillis=200");
                    jvmArgs.Add("-XX:G1NewSizePercent=30");
                    jvmArgs.Add("-XX:G1MaxNewSizePercent=40");
                    jvmArgs.Add("-XX:G1HeapRegionSize=8M");
                    jvmArgs.Add("-XX:G1ReservePercent=20");
                    jvmArgs.Add("-XX:G1HeapWastePercent=5");
                    jvmArgs.Add("-XX:G1MixedGCCountTarget=4");
                    jvmArgs.Add("-XX:InitiatingHeapOccupancyPercent=15");
                    jvmArgs.Add("-XX:G1MixedGCLiveThresholdPercent=90");
                    jvmArgs.Add("-XX:G1RSetUpdatingPauseTimePercent=5");
                    jvmArgs.Add("-XX:SurvivorRatio=32");
                    jvmArgs.Add("-XX:MaxTenuringThreshold=1");
                }
                // If None, do not add any GC flags (Manual Control)
            }

            // --- GTNH Detection: auto-add --add-opens JVM flags and fix 403 errors ---
            if (IsGTNHModpack(CurrentModpack.Name, modpackDir))
            {
                jvmArgs.Add("-Dhttp.agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                jvmArgs.Add("-Dfile.encoding=UTF-8");

                if (javaVersion >= 9)
                {
                    LaunchStatus = $"Detekován GTNH s Java {javaVersion} – aplikuji kompatibilní argumenty...";
                    jvmArgs.AddRange(new[] {
                        "--add-opens", "java.base/jdk.internal.loader=ALL-UNNAMED",
                        "--add-opens", "java.base/java.net=ALL-UNNAMED",
                        "--add-opens", "java.base/java.nio=ALL-UNNAMED",
                        "--add-opens", "java.base/java.io=ALL-UNNAMED",
                        "--add-opens", "java.base/java.lang=ALL-UNNAMED",
                        "--add-opens", "java.base/java.lang.reflect=ALL-UNNAMED",
                        "--add-opens", "java.base/java.util=ALL-UNNAMED",
                        "--add-opens", "java.base/java.util.concurrent=ALL-UNNAMED",
                        "--add-opens", "java.base/sun.nio.ch=ALL-UNNAMED",
                        "--add-opens", "java.base/sun.security.ssl=ALL-UNNAMED",
                        "--add-opens", "java.base/sun.security.util=ALL-UNNAMED",
                        "--add-opens", "java.base/sun.net.www.protocol.jar=ALL-UNNAMED",
                        "--add-opens", "java.desktop/sun.awt=ALL-UNNAMED",
                        "--add-opens", "java.desktop/sun.font=ALL-UNNAMED",
                        "--add-opens", "java.desktop/sun.java2d=ALL-UNNAMED",
                        "--add-opens", "jdk.naming.dns/com.sun.jndi.dns=ALL-UNNAMED",
                        "-Djava.security.manager=allow"
                    });
                }
            }

            // Append per-instance custom JVM arguments if any
            if (overrideConfig?.CustomJvmArguments != null && overrideConfig.CustomJvmArguments.Length > 0)
            {
                jvmArgs.AddRange(overrideConfig.CustomJvmArguments);
            }

            // Append user custom arguments from global config if any
            if (Config.JvmArguments != null && Config.JvmArguments.Length > 0)
            {
                jvmArgs.AddRange(Config.JvmArguments);
            }

            // APPLY POTATO MODE
            bool potatoMode = overrideConfig?.PotatoModeEnabled ?? false;
            LaunchStatus = potatoMode ? "Aplikuji Potato Mode (vypínám mody)..." : "Kontrola Potato Mode...";
            
            try
            {
               ModUtils.ApplyPotatoMode(modsDir, modpackDir, potatoMode);
            }
            catch (Exception ex)
            {
                LogService.Error("Failed to apply Potato Mode", ex);
            }

            var gameArguments = GetAutoConnectArgs();
            
            var gameProcess = await _launcherService.LaunchAsync(
                mcVersion,
                UserSession,
                Config,
                modpackDir,
                modLoaderId,
                jvmArgs.ToArray(),
                gameArguments,
                requiredJava,
                (status) => Avalonia.Threading.Dispatcher.UIThread.Post(() => LaunchStatus = status),
                (percent) => Avalonia.Threading.Dispatcher.UIThread.Post(() => LaunchProgress = percent),
                (file) => Avalonia.Threading.Dispatcher.UIThread.Post(() => LaunchStatus = $"Stahuji: {file}")
            );
            
            // Redirect output for debugging
            gameProcess.StartInfo.RedirectStandardOutput = true;
            gameProcess.StartInfo.RedirectStandardError = true;
            
            LogService.Log($"--- GAME START ---", "GAME");
            LogService.Log($"Java Args: {string.Join(" ", jvmArgs)}", "GAME");

            void LogTo(string prefix, string data)
            {
                if (string.IsNullOrEmpty(data)) return;
                LogService.Log(data, $"G-{prefix}");
            }

            gameProcess.OutputDataReceived += (s, e) => LogTo("STDOUT", e.Data);
            gameProcess.ErrorDataReceived += (s, e) => LogTo("STDERR", e.Data);
            
            gameProcess.Start();
            gameProcess.BeginOutputReadLine();
            gameProcess.BeginErrorReadLine();

            IsLaunching = false;
            IsGameRunning = true;
            RunningModpack = TargetModpack ?? CurrentModpack;
            _discordRpcService.SetPlayingState(RunningModpack?.Name ?? "Minecraft");
            LaunchProgress = 100;
            TargetModpack = null;

            // Minimize to tray while game is running
            App.MinimizeToTray();
            
            // Wait for game to exit in background
            var launchedModpackName = RunningModpack?.Name ?? "Minecraft";
            var gameStartTime = DateTime.UtcNow;
            _ = Task.Run(async () => 
            {
                await gameProcess.WaitForExitAsync();
                var exitCode = gameProcess.ExitCode;
                var runtime = DateTime.UtcNow - gameStartTime;
                LogTo("LAUNCHER", $"Game exited with code {exitCode}");
                
                Avalonia.Threading.Dispatcher.UIThread.Post(() => 
                {
                    IsGameRunning = false;
                    RunningModpack = null;
                    UpdateDiscordPresence();
                    App.RestoreMainWindow();

                    if (exitCode != 0)
                    {
                        // Read last ~50 lines of game log for crash drawer
                        var logTail = "";
                        string? logPath = null;
                        try
                        {
                            var logDir = Path.Combine(_launcherService.GetModpackPath(launchedModpackName), "logs");
                            logPath = Path.Combine(logDir, "latest.log");
                            if (File.Exists(logPath))
                            {
                                var lines = File.ReadAllLines(logPath);
                                logTail = string.Join("\n", lines.Skip(Math.Max(0, lines.Length - 50)));
                            }
                        }
                        catch { /* ignore log read errors */ }

                        ShowCrashDrawer(launchedModpackName, exitCode, runtime, logTail, logPath);
                        ShowToast("Crash", $"{launchedModpackName} se ukončil s kódem {exitCode}.", ToastSeverity.Error);
                    }
                    else
                    {
                        Greeting = $"{launchedModpackName} ukončen normálně.";
                    }
                });
            });
        }
        catch (Exception ex)
        {
            LaunchStatus = $"Chyba: {ex.Message}";
            await Task.Delay(3000);
            IsLaunching = false;
            TargetModpack = null;
            RunningModpack = null;
            UpdateDiscordPresence();
        }
    }

    [RelayCommand]
    public void StopGame()
    {
        _launcherService.StopGame();
        IsGameRunning = false;
        RunningModpack = null;
        Greeting = "Hra ukončena.";
        UpdateDiscordPresence();
    }

    [RelayCommand]
    public void ManageMods()
    {
        if (CurrentModpack != null)
        {
            var modpackPath = _launcherService.GetModpackPath(CurrentModpack.Name);
            var vm = new ModManagerViewModel(CurrentModpack.Name, modpackPath);
            var window = new VoidCraftLauncher.Views.ModManagerWindow { DataContext = vm };
            vm.RequestClose += window.Close;

            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                window.ShowDialog(desktop.MainWindow);
            }
        }
    }

    [RelayCommand]
    public void OpenModpackFolder()
    {
        if (CurrentModpack != null)
        {
            var path = _launcherService.GetModpackPath(CurrentModpack.Name);
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
                Verb = "open"
            });
        }
    }

    // Context menu versions that accept modpack parameter
    [RelayCommand]
    public void OpenModpackFolderContext(ModpackInfo modpack)
    {
        if (modpack != null)
        {
            var path = _launcherService.GetModpackPath(modpack.Name);
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
                Verb = "open"
            });
        }
    }

    [RelayCommand]
    public void ManageModsContext(ModpackInfo modpack)
    {
        if (modpack != null)
        {
            var modpackPath = _launcherService.GetModpackPath(modpack.Name);
            var vm = new ModManagerViewModel(modpack.Name, modpackPath);
            var window = new VoidCraftLauncher.Views.ModManagerWindow { DataContext = vm };
            vm.RequestClose += window.Close;

            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                window.ShowDialog(desktop.MainWindow);
            }
        }
    }

    [RelayCommand]
    public async Task DeleteModpackContext(ModpackInfo modpack)
    {
        if (modpack == null) return;
        if (!modpack.IsDeletable) 
        {
            Greeting = "Tento modpack nelze odstranit.";
            return;
        }

        if (!await ConfirmBackupBeforeDestructiveActionAsync(modpack, "delete-instance"))
        {
            return;
        }
        
        var modpackName = modpack.Name;
        var modpackPath = _launcherService.GetModpackPath(modpackName);
        
        InstalledModpacks.Remove(modpack);
        
        if (Directory.Exists(modpackPath))
        {
            try
            {
                Directory.Delete(modpackPath, recursive: true);
                Greeting = $"Modpack {modpackName} byl smazán.";
            }
            catch (Exception ex)
            {
                Greeting = $"Chyba při mazání: {ex.Message}";
            }
        }
        else
        {
            Greeting = $"Modpack {modpackName} odebrán z knihovny.";
        }
        
        if (CurrentModpack == modpack)
        {
            CurrentModpack = InstalledModpacks.FirstOrDefault();
        }
        
        SaveModpacks();
    }

    [RelayCommand]
    public async Task DeleteModpack()
    {
        if (CurrentModpack == null) return;

        if (!await ConfirmBackupBeforeDestructiveActionAsync(CurrentModpack, "delete-instance"))
        {
            return;
        }
        
        var modpackName = CurrentModpack.Name;
        var modpackPath = _launcherService.GetModpackPath(modpackName);
        
        InstalledModpacks.Remove(CurrentModpack);
        
        if (Directory.Exists(modpackPath))
        {
            try
            {
                Directory.Delete(modpackPath, recursive: true);
                Greeting = $"Modpack {modpackName} byl smazán.";
            }
            catch (Exception ex)
            {
                Greeting = $"Chyba při mazání: {ex.Message}";
            }
        }
        else
        {
            Greeting = $"Modpack {modpackName} odebrán z knihovny.";
        }
        
        CurrentModpack = InstalledModpacks.FirstOrDefault();
        GoToHome();
    }

    [RelayCommand]
    public void SelectAndConfigure(ModpackInfo modpack)
    {
        CurrentModpack = modpack;
        GoToModpackSettings();
        LoadInstalledMods();
    }

    [RelayCommand]
    public async Task SelectAndPlay(ModpackInfo modpack)
    {
        CurrentModpack = modpack;
        TargetModpack = modpack;
        await PlayModpack();
    }

    [RelayCommand]
    public async Task ReinstallModpackContext(ModpackInfo modpack)
    {
        if (modpack == null) return;

        if (!await ConfirmBackupBeforeDestructiveActionAsync(modpack, "reinstall-instance"))
        {
            return;
        }

        var modpackPath = _launcherService.GetModpackPath(modpack.Name);
        
        try
        {
            Greeting = $"Přeinstalovávám {modpack.Name}...";
            IsLaunching = true;
            LaunchStatus = $"Mažu starý obsah {modpack.Name}...";
            LaunchProgress = 0;
            TargetModpack = modpack;
            
            if (Directory.Exists(modpackPath))
            {
                Directory.Delete(modpackPath, true);
                Directory.CreateDirectory(modpackPath);
            }

            LaunchStatus = "Starý obsah smazán. Spouštím reinstalaci...";
            LaunchProgress = 10;

            modpack.CurrentVersion = new ModpackVersion { Name = "-", FileId = "0" };

            IsLaunching = false;
            await PlayModpack();
        }
        catch (Exception ex)
        {
            Greeting = $"Chyba reinstalace: {ex.Message}";
            IsLaunching = false;
            TargetModpack = null;
        }
    }

    // ===== GTNH DETECTION =====

    private static bool IsGTNHModpack(string name, string modpackDir)
    {
        if (name.Contains("GT New Horizons", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("GTNH", StringComparison.OrdinalIgnoreCase))
            return true;

        var modsDir = Path.Combine(modpackDir, "mods");
        if (Directory.Exists(modsDir))
        {
            return File.Exists(Path.Combine(modpackDir, ".gtnh")) || 
                   Directory.GetFiles(modsDir, "GTNewHorizonsCoreMod*", SearchOption.TopDirectoryOnly).Length > 0;
        }

        return false;
    }
}
