using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CmlLib.Core.Auth;
using VoidCraftLauncher.Models;
using VoidCraftLauncher.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json.Nodes;

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

            var launchSession = await ResolveLaunchSessionForStartAsync();

            var modpackDir = _launcherService.GetModpackPath(CurrentModpack.Name);
            var modsDir = Path.Combine(modpackDir, "mods");

            // INSTALL / UPDATE
            if (CurrentModpack.IsCustomProfile && string.IsNullOrWhiteSpace(CurrentModpack.VoidRegistrySlug))
            {
                LaunchStatus = "Vlastní profil – přeskočena kontrola aktualizací.";
            }
            else if (CurrentModpack.IsTrackingLatest && CurrentModpack.IsUpdateAvailable)
            {
                // ⭐ Latest selected & new version exists → show update prompt with changelog
                UpdatePromptModpackName = CurrentModpack.DisplayLabel;
                UpdatePromptTargetVersionName = CurrentModpack.TargetVersionName;
                
                LaunchStatus = "Načítám changelog...";
                UpdatePromptChangelog = await FetchModpackChangelogAsync(CurrentModpack) ?? "Changelog nebyl poskytnut platformou.";

                LaunchStatus = "Čekám na rozhodnutí uživatele...";
                var decision = await ShowUpdatePromptAsync(UpdatePromptModpackName, UpdatePromptTargetVersionName, UpdatePromptChangelog);

                if (decision == "Skip")
                {
                    LaunchStatus = "Přeskakuji update...";
                    _lastManifestInfo = ModpackInstaller.LoadManifestInfo(modpackDir) ?? new ModpackManifestInfo();
                }
                else if (decision == "ConfirmBackup")
                {
                    LaunchStatus = "Spouštím zálohování před updatem...";
                    var backupDecision = await ShowBackupPrompt(CurrentModpack.Name);
                    if (backupDecision == BackupPromptDecision.Cancel)
                    {
                        IsLaunching = false;
                        LaunchStatus = "Zrušeno uživatelem.";
                        return;
                    }
                    
                    if (backupDecision == BackupPromptDecision.BackupAndContinue)
                    {
                        await CreatePersistentBackupSnapshotAsync(CurrentModpack, "Před aktualizací modpacku", notifyUser: true);
                    }

                    LaunchStatus = "Instaluji aktualizaci...";
                    await ExecUpdateImplementation(CurrentModpack, modpackDir, modsDir);
                }
                else // Confirm
                {
                    LaunchStatus = "Instaluji aktualizaci...";
                    await ExecUpdateImplementation(CurrentModpack, modpackDir, modsDir);
                }
            }
            else if (!CurrentModpack.IsTrackingLatest && CurrentModpack.ResolvedTargetVersion != null)
            {
                // Pinned specific version → install silently if differs from installed
                var pinned = CurrentModpack.ResolvedTargetVersion;
                var installedManifest = ModpackInstaller.LoadManifestInfo(modpackDir);
                var pinnedFileId = pinned.FileId;
                var isAlreadyInstalled = installedManifest != null &&
                    (installedManifest.FileId.ToString() == pinnedFileId ||
                     string.Equals(installedManifest.Version, pinned.Name, System.StringComparison.OrdinalIgnoreCase)) &&
                    HasInstalledModpackContent(modpackDir, modsDir);

                if (isAlreadyInstalled)
                {
                    LaunchStatus = $"Verze {pinned.Name} je nainstalována.";
                    _lastManifestInfo = installedManifest ?? new ModpackManifestInfo();
                }
                else
                {
                    LaunchStatus = $"Příprava verze {pinned.Name}...";
                    await ExecUpdateImplementation(CurrentModpack, modpackDir, modsDir, pinned);
                }
            }
            else
            {
                // All up to date or no versions available
                _lastManifestInfo = ModpackInstaller.LoadManifestInfo(modpackDir) ?? new ModpackManifestInfo();
            }
            
            LaunchStatus = "Spouštím hru...";

            _lastManifestInfo = ResolveLaunchManifestInfo(CurrentModpack, modpackDir);

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
                launchSession,
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
            LogService.Log($"Launch session: {launchSession.Username} ({launchSession.UserType ?? "offline"}) UUID={launchSession.UUID}", "GAME");
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
            _discordRpcService.SetPlayingState(RunningModpack?.DisplayLabel ?? RunningModpack?.Name ?? "Minecraft");
            LaunchProgress = 100;

            // Uložit do nedávných instancí
            if (RunningModpack != null && !string.IsNullOrWhiteSpace(RunningModpack.Name))
            {
                Config.RecentInstances.Remove(RunningModpack.Name);
                Config.RecentInstances.Insert(0, RunningModpack.Name);
                if (Config.RecentInstances.Count > 6)
                    Config.RecentInstances.RemoveAt(Config.RecentInstances.Count - 1);
                _launcherService.SaveConfig(Config);
                RefreshRecentModpacks();
            }

            TargetModpack = null;

            // Minimize to tray while game is running
            App.MinimizeToTray();
            
            // Wait for game to exit in background
            var launchedModpackName = RunningModpack?.DisplayLabel ?? RunningModpack?.Name ?? "Minecraft";
            var launchedModpackId = RunningModpack?.Name ?? "Minecraft";
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
                            var logDir = Path.Combine(_launcherService.GetModpackPath(launchedModpackId), "logs");
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

    private async Task EnsureRequiredModpackVersionInstalledAsync(ModpackInfo modpack, string modpackDir, string modsDir, ModpackVersion? targetVersion = null)
    {
        if (modpack.ProjectId > 0)
        {
            await EnsureLatestCurseForgeModpackInstalledAsync(modpack, modpackDir, modsDir, targetVersion);
            return;
        }

        if (!string.IsNullOrWhiteSpace(modpack.VoidRegistrySlug))
        {
            await EnsureLatestVoidRegistryModpackInstalledAsync(modpack, modpackDir, modsDir, targetVersion);
            return;
        }

        if (!string.IsNullOrWhiteSpace(modpack.ModrinthId))
        {
            await EnsureLatestModrinthModpackInstalledAsync(modpack, modpackDir, modsDir, targetVersion);
            return;
        }

        _lastManifestInfo = ModpackInstaller.LoadManifestInfo(modpackDir) ?? new ModpackManifestInfo();
    }

    private async Task EnsureLatestCurseForgeModpackInstalledAsync(ModpackInfo modpack, string modpackDir, string modsDir, ModpackVersion? targetVersion = null)
    {
        var filesJson = await _curseForgeApi.GetModpackFilesAsync(modpack.ProjectId);
        var filesNode = JsonNode.Parse(filesJson);
        var files = filesNode?["data"]?.AsArray();
        if (files == null || files.Count == 0)
        {
            throw new InvalidOperationException("Zdroj nevrátil žádné buildy modpacku.");
        }

        var remoteVersions = files
            .OrderByDescending(file => file?["fileDate"]?.ToString())
            .Select(file => new ModpackVersion
            {
                Name = file?["displayName"]?.ToString() ?? "Unknown",
                FileId = file?["id"]?.ToString() ?? "0",
                ReleaseDate = file?["fileDate"]?.ToString() ?? string.Empty
            })
            .ToList();

        modpack.Versions = new ObservableCollection<ModpackVersion>(remoteVersions);

        // Determine goal version
        ModpackVersion? goalVersion = null;
        if (targetVersion != null && !targetVersion.IsLatestSentinel)
        {
            goalVersion = remoteVersions.FirstOrDefault(v => v.FileId == targetVersion.FileId);
        }

        if (goalVersion == null)
        {
            goalVersion = remoteVersions.FirstOrDefault();
        }

        if (goalVersion == null || !int.TryParse(goalVersion.FileId, out var goalFileId))
        {
            throw new InvalidOperationException("Nepodařilo se určit cílový build CurseForge modpacku.");
        }

        var installedManifest = ModpackInstaller.LoadManifestInfo(modpackDir);
        var isGoalInstalled = HasInstalledModpackContent(modpackDir, modsDir) &&
            ((installedManifest?.FileId == goalFileId) || 
             string.Equals(installedManifest?.Version, goalVersion.Name, StringComparison.OrdinalIgnoreCase));

        if (isGoalInstalled)
        {
            LaunchStatus = $"Verze {goalVersion.Name} je připravena.";
            SyncInstalledVersionState(modpack, goalVersion);
            _lastManifestInfo = installedManifest ?? new ModpackManifestInfo();
            return;
        }

        var goalFileNode = files.FirstOrDefault(file => file?["id"]?.GetValue<int?>() == goalFileId);
        if (goalFileNode == null)
        {
            throw new InvalidOperationException("Cílovou verzi se nepodařilo dohledat v seznamu souborů.");
        }

        var downloadUrl = goalFileNode["downloadUrl"]?.ToString();
        var fileName = goalFileNode["fileName"]?.ToString() ?? $"voidcraft_{modpack.ProjectId}_{goalFileId}.zip";
        var downloadCandidates = await BuildCurseForgeArchiveDownloadCandidatesAsync(modpack.ProjectId, goalFileId, downloadUrl, fileName);
        if (downloadCandidates.Count == 0)
        {
            throw new InvalidOperationException("Nepodařilo se získat URL ke stažení cílové verze.");
        }

        var tempPath = Path.Combine(Path.GetTempPath(), $"voidcraft_{modpack.ProjectId}_{goalFileId}.zip");
        try
        {
            LaunchStatus = $"Stahuji verzi {goalVersion.Name}...";
            await DownloadPackageArchiveAsync(downloadCandidates, tempPath, goalVersion.Name);

            LaunchStatus = "Instaluji soubory modpacku...";
            _lastManifestInfo = await _modpackInstaller.InstallOrUpdateAsync(tempPath, modpackDir, goalFileId, goalVersion.Name);
            SyncInstalledVersionState(modpack, goalVersion);
            SaveModpacks();
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private async Task EnsureLatestModrinthModpackInstalledAsync(ModpackInfo modpack, string modpackDir, string modsDir, ModpackVersion? targetVersion = null)
    {
        var versionsJson = await _modrinthApi.GetProjectVersionsAsync(modpack.ModrinthId);
        var versions = JsonNode.Parse(versionsJson)?.AsArray();
        if (versions == null || versions.Count == 0)
        {
            throw new InvalidOperationException("Zdroj nevrátil žádné buildy Modrinth modpacku.");
        }

        var remoteVersions = versions
            .Select(version => new ModpackVersion
            {
                Name = version?["version_number"]?.ToString() ?? version?["name"]?.ToString() ?? "Unknown",
                FileId = version?["id"]?.ToString() ?? "0",
                ReleaseDate = version?["date_published"]?.ToString() ?? string.Empty
            })
            .ToList();

        modpack.Versions = new ObservableCollection<ModpackVersion>(remoteVersions);

        // Determine goal version
        JsonNode? goalVersionNode = null;
        if (targetVersion != null && !targetVersion.IsLatestSentinel)
        {
            goalVersionNode = versions.FirstOrDefault(v => v?["id"]?.ToString() == targetVersion.FileId);
        }

        if (goalVersionNode == null)
        {
            goalVersionNode = versions.FirstOrDefault(version => string.Equals(version?["version_type"]?.ToString(), "release", StringComparison.OrdinalIgnoreCase))
                ?? versions.FirstOrDefault();
        }

        if (goalVersionNode == null)
        {
            throw new InvalidOperationException("Nepodařilo se určit cílový build Modrinth modpacku.");
        }

        var goalVersionId = goalVersionNode["id"]?.ToString() ?? "0";
        var goalVersionName = goalVersionNode["version_number"]?.ToString() ?? goalVersionNode["name"]?.ToString() ?? "Unknown";
        var goalVersion = remoteVersions.FirstOrDefault(version => string.Equals(version.FileId, goalVersionId, StringComparison.OrdinalIgnoreCase))
            ?? new ModpackVersion { Name = goalVersionName, FileId = goalVersionId };

        var installedManifest = ModpackInstaller.LoadManifestInfo(modpackDir);
        var isGoalInstalled = HasInstalledModpackContent(modpackDir, modsDir) &&
            ((installedManifest?.Version?.Equals(goalVersionName, StringComparison.OrdinalIgnoreCase) ?? false) ||
             string.Equals(modpack.CurrentVersion?.FileId, goalVersionId, StringComparison.OrdinalIgnoreCase));

        if (isGoalInstalled)
        {
            LaunchStatus = $"Verze {goalVersionName} připravena.";
            SyncInstalledVersionState(modpack, goalVersion);
            _lastManifestInfo = installedManifest ?? new ModpackManifestInfo();
            return;
        }

        var primaryFile = goalVersionNode["files"]?.AsArray()
            ?.FirstOrDefault(file => file?["primary"]?.GetValue<bool>() == true)
            ?? goalVersionNode["files"]?.AsArray()?.FirstOrDefault();

        var files = goalVersionNode["files"]?.AsArray();
        var downloadCandidates = BuildModrinthArchiveDownloadCandidates(files, primaryFile);
        var fileName = primaryFile?["filename"]?.ToString() ?? $"{modpack.Name}-{goalVersionName}.mrpack";
        if (downloadCandidates.Count == 0)
        {
            throw new InvalidOperationException("Nepodařilo se získat URL ke stažení cílového Modrinth buildu.");
        }

        var tempPath = Path.Combine(Path.GetTempPath(), fileName);
        try
        {
            LaunchStatus = $"Stahuji verzi {goalVersionName}...";
            await DownloadPackageArchiveAsync(downloadCandidates, tempPath, goalVersionName);

            LaunchStatus = "Instaluji soubory modpacku...";
            _lastManifestInfo = await _modpackInstaller.InstallOrUpdateAsync(tempPath, modpackDir, targetVersion: goalVersionName);
            SyncInstalledVersionState(modpack, goalVersion);
            SaveModpacks();
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private async Task EnsureLatestVoidRegistryModpackInstalledAsync(ModpackInfo modpack, string modpackDir, string modsDir, ModpackVersion? targetVersion = null)
    {
        var slug = modpack.VoidRegistrySlug;
        if (string.IsNullOrWhiteSpace(slug))
        {
            _lastManifestInfo = ModpackInstaller.LoadManifestInfo(modpackDir) ?? new ModpackManifestInfo();
            return;
        }

        var installedManifest = ModpackInstaller.LoadManifestInfo(modpackDir);
        var currentVersionName = FirstNonEmpty(installedManifest?.Version, modpack.CurrentVersion?.Name);
        var hasInstalledContent = HasInstalledModpackContent(modpackDir, modsDir);
        var accessToken = await ResolveVoidRegistryAccessTokenAsync(modpack, requireFreshSession: true);

        if (IsCollaboratorRegistryWorkspace(modpack) && string.IsNullOrWhiteSpace(accessToken))
        {
            if (hasInstalledContent)
            {
                LaunchStatus = $"VOID ID session není aktivní, používám lokální workspace {modpack.DisplayLabel}.";
                _lastManifestInfo = installedManifest ?? new ModpackManifestInfo();
                return;
            }

            throw new InvalidOperationException("Collaborator workspace vyžaduje aktivní VOID ID session, aby šel stáhnout interní release.");
        }

        // Handle specific version if available (pinned mode)
        if (targetVersion != null && !targetVersion.IsLatestSentinel)
        {
            if (hasInstalledContent && currentVersionName == targetVersion.Name)
            {
                LaunchStatus = $"Verze {currentVersionName} je připravena.";
                _lastManifestInfo = installedManifest ?? new ModpackManifestInfo();
                return;
            }
        }

        if (hasInstalledContent && !string.IsNullOrWhiteSpace(currentVersionName))
        {
            var updateCheck = await _voidRegistryService.GetUpdateCheckAsync(slug, currentVersionName, accessToken);
            var latestInfo = updateCheck?.Latest;
            if (latestInfo != null)
            {
                var latestVersion = new ModpackVersion
                {
                    Name = string.IsNullOrWhiteSpace(latestInfo.Version) ? currentVersionName : latestInfo.Version,
                    FileId = !string.IsNullOrWhiteSpace(latestInfo.VersionId) ? latestInfo.VersionId : latestInfo.Version,
                    ReleaseDate = latestInfo.PublishedAtUtc?.ToString("O") ?? string.Empty
                };

                modpack.Versions = new ObservableCollection<ModpackVersion>(new[] { latestVersion });

                if (!updateCheck!.UpdateAvailable)
                {
                    LaunchStatus = $"Máte nejnovější verzi ({currentVersionName}).";
                    modpack.CurrentVersion = new ModpackVersion
                    {
                        Name = currentVersionName,
                        FileId = string.IsNullOrWhiteSpace(modpack.CurrentVersion?.FileId) ? latestVersion.FileId : modpack.CurrentVersion.FileId,
                        ReleaseDate = latestVersion.ReleaseDate
                    };
                    _lastManifestInfo = installedManifest ?? new ModpackManifestInfo();
                    return;
                }
            }
        }

        var manifest = await _voidRegistryService.GetInstallManifestAsync(slug, accessToken);
        if (manifest == null || string.IsNullOrWhiteSpace(manifest.FileUrl))
        {
            throw new InvalidOperationException("VOID Registry nevrátil validní install manifest.");
        }

        var targetVersionName = string.IsNullOrWhiteSpace(manifest.Version) ? "Latest" : manifest.Version;
        var targetVersionId = string.IsNullOrWhiteSpace(manifest.VersionId) ? targetVersionName : manifest.VersionId;
        var latestManifestVersion = new ModpackVersion
        {
            Name = targetVersionName,
            FileId = targetVersionId
        };

        modpack.Versions = new ObservableCollection<ModpackVersion>(new[] { latestManifestVersion });

        if (hasInstalledContent &&
            !string.IsNullOrWhiteSpace(currentVersionName) &&
            string.Equals(currentVersionName, targetVersionName, StringComparison.OrdinalIgnoreCase))
        {
            LaunchStatus = $"Máte nejnovější verzi ({targetVersionName}).";
            SyncInstalledVersionState(modpack, latestManifestVersion);
            _lastManifestInfo = installedManifest ?? new ModpackManifestInfo();
            return;
        }

        var fileName = Path.GetFileName(string.IsNullOrWhiteSpace(manifest.FileName)
            ? $"{slug}-{targetVersionName}.voidpack"
            : manifest.FileName);
        var tempPath = Path.Combine(Path.GetTempPath(), fileName);

        try
        {
            LaunchStatus = $"Stahuji aktualizaci {targetVersionName}...";
            await DownloadPackageArchiveAsync(new[] { manifest.FileUrl }, tempPath, targetVersionName);

            LaunchStatus = "Instaluji aktualizované soubory...";
            _lastManifestInfo = await _modpackInstaller.InstallOrUpdateAsync(tempPath, modpackDir, targetVersion: targetVersionName);
            SyncInstalledVersionState(modpack, latestManifestVersion);
            SaveModpacks();
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static bool HasInstalledModpackContent(string modpackDir, string modsDir)
    {
        if (File.Exists(Path.Combine(modpackDir, "manifest_info.json")))
        {
            return true;
        }

        try
        {
            return Directory.Exists(modsDir) && Directory.EnumerateFileSystemEntries(modsDir).Any();
        }
        catch
        {
            return false;
        }
    }

    private static void SyncInstalledVersionState(ModpackInfo modpack, ModpackVersion installedVersion)
    {
        modpack.CurrentVersion = new ModpackVersion
        {
            Name = installedVersion.Name,
            FileId = installedVersion.FileId,
            ReleaseDate = installedVersion.ReleaseDate
        };
    }

    private async Task<List<string>> BuildCurseForgeArchiveDownloadCandidatesAsync(int projectId, int fileId, string? primaryUrl, string? fileName)
    {
        var candidates = new List<string>();
        AddArchiveDownloadCandidate(candidates, primaryUrl);

        try
        {
            var refreshedUrl = await _curseForgeApi.GetFileDownloadUrlAsync(projectId, fileId);
            AddArchiveDownloadCandidate(candidates, refreshedUrl);
        }
        catch (Exception ex)
        {
            LogService.Error($"[BuildCurseForgeArchiveDownloadCandidates] Download URL refresh failed for {projectId}:{fileId}", ex);
        }

        try
        {
            var fileJson = await _curseForgeApi.GetModFileAsync(projectId, fileId);
            var fileNode = JsonNode.Parse(fileJson)?["data"];
            fileName ??= fileNode?["fileName"]?.ToString();
            AddArchiveDownloadCandidate(candidates, fileNode?["downloadUrl"]?.ToString());
        }
        catch (Exception ex)
        {
            LogService.Error($"[BuildCurseForgeArchiveDownloadCandidates] File metadata fallback failed for {projectId}:{fileId}", ex);
        }

        foreach (var candidate in BuildCurseForgeCdnCandidates(fileId, fileName))
        {
            AddArchiveDownloadCandidate(candidates, candidate);
        }

        return candidates;
    }

    private async Task DownloadPackageArchiveAsync(IEnumerable<string> candidateUrls, string destinationPath, string packageLabel)
    {
        var candidates = candidateUrls
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (candidates.Count == 0)
        {
            throw new InvalidOperationException($"Balíček {packageLabel} nemá žádnou dostupnou download URL.");
        }

        var tempPath = destinationPath + ".download";
        Exception? lastException = null;

        foreach (var candidateUrl in candidates)
        {
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    TryDeleteArchiveFile(tempPath);

                    using var response = await _httpClient.GetAsync(candidateUrl, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();

                    await using (var sourceStream = await response.Content.ReadAsStreamAsync())
                    await using (var targetStream = File.Create(tempPath))
                    {
                        await sourceStream.CopyToAsync(targetStream);
                        await targetStream.FlushAsync();
                    }

                    await MoveArchiveFileWithRetryAsync(tempPath, destinationPath);
                    return;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    TryDeleteArchiveFile(tempPath);

                    if (attempt == 3)
                    {
                        LogService.Error($"[DownloadPackageArchive] Failed for {packageLabel} from {candidateUrl}", ex);
                    }
                }
            }
        }

        throw new InvalidOperationException($"Nepodařilo se stáhnout balíček {packageLabel} z žádného dostupného zdroje.", lastException);
    }

    private static async Task MoveArchiveFileWithRetryAsync(string tempPath, string destinationPath)
    {
        Exception? lastException = null;

        for (int attempt = 1; attempt <= 5; attempt++)
        {
            try
            {
                TryDeleteArchiveFile(destinationPath);
                File.Move(tempPath, destinationPath, true);
                return;
            }
            catch (IOException ex)
            {
                lastException = ex;
            }
            catch (UnauthorizedAccessException ex)
            {
                lastException = ex;
            }

            if (attempt < 5)
            {
                await Task.Delay(120 * attempt);
            }
        }

        throw lastException ?? new IOException($"Nepodařilo se přesunout stažený archiv do {destinationPath}.");
    }

    private static List<string> BuildModrinthArchiveDownloadCandidates(JsonArray? files, JsonNode? preferredFile)
    {
        var candidates = new List<string>();
        AddArchiveDownloadCandidate(candidates, preferredFile?["url"]?.ToString());

        if (files != null)
        {
            foreach (var file in files)
            {
                AddArchiveDownloadCandidate(candidates, file?["url"]?.ToString());
            }
        }

        return candidates;
    }

    private static IEnumerable<string> BuildCurseForgeCdnCandidates(int fileId, string? fileName)
    {
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

    private static void AddArchiveDownloadCandidate(ICollection<string> candidates, string? url)
    {
        if (!string.IsNullOrWhiteSpace(url))
        {
            candidates.Add(url);
        }
    }

    private static void TryDeleteArchiveFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch
        {
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

    private ModpackManifestInfo ResolveLaunchManifestInfo(ModpackInfo modpack, string modpackDir)
    {
        var manifestInfo = ModpackInstaller.LoadManifestInfo(modpackDir) ?? new ModpackManifestInfo();
        var creatorManifest = _creatorManifestService.LoadManifest(modpackDir);

        manifestInfo.PackName = FirstNonEmpty(
            manifestInfo.PackName,
            creatorManifest?.PackName,
            modpack.DisplayLabel,
            modpack.Name);

        manifestInfo.Author = FirstNonEmpty(
            manifestInfo.Author,
            creatorManifest?.Authors.Count > 0 ? string.Join(", ", creatorManifest.Authors) : null,
            modpack.Author);

        manifestInfo.MinecraftVersion = FirstNonEmpty(
            manifestInfo.MinecraftVersion,
            creatorManifest?.MinecraftVersion,
            modpack.CustomMcVersion,
            "1.21.1");

        manifestInfo.ModLoaderType = FirstNonEmpty(
            manifestInfo.ModLoaderType,
            creatorManifest?.ModLoader,
            modpack.CustomModLoader);

        if (string.IsNullOrWhiteSpace(manifestInfo.ModLoaderId))
        {
            manifestInfo.ModLoaderId = ComposeModLoaderId(
                manifestInfo.ModLoaderType,
                FirstNonEmpty(
                    creatorManifest?.ModLoaderVersion,
                    modpack.CustomModLoaderVersion));
        }
        else if (string.IsNullOrWhiteSpace(manifestInfo.ModLoaderType))
        {
            manifestInfo.ModLoaderType = ExtractManifestLoaderType(manifestInfo.ModLoaderId);
        }

        return manifestInfo;
    }

    private static string ComposeModLoaderId(string? modLoaderType, string? modLoaderVersion)
    {
        var loaderType = modLoaderType?.Trim() ?? string.Empty;
        var loaderVersion = modLoaderVersion?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(loaderType))
        {
            return string.Empty;
        }

        return string.IsNullOrWhiteSpace(loaderVersion)
            ? loaderType
            : $"{loaderType}-{loaderVersion}";
    }

    private static string ExtractManifestLoaderType(string? modLoaderId)
    {
        if (string.IsNullOrWhiteSpace(modLoaderId))
        {
            return string.Empty;
        }

        var separatorIndex = modLoaderId.IndexOf('-');
        return separatorIndex > 0 ? modLoaderId[..separatorIndex] : modLoaderId.Trim();
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

    private async Task<MSession> ResolveLaunchSessionForStartAsync()
    {
        if (ActiveAccount != null)
        {
            if (IsSessionCompatibleWithAccount(UserSession, ActiveAccount))
            {
                return UserSession;
            }

            var restoredSession = await TryRestoreSessionForAccountAsync(ActiveAccount);
            if (restoredSession != null)
            {
                await ApplyResolvedLaunchSessionAsync(restoredSession, ActiveAccount);
                return restoredSession;
            }

            throw new InvalidOperationException($"Aktivní účet {ActiveAccount.DisplayName} se nepodařilo obnovit. Přihlas se znovu.");
        }

        if (UserSession != null && !IsGuestSession(UserSession))
        {
            return UserSession;
        }

        if (IsLoggedIn)
        {
            var recoveredSession = await _authService.TrySilentLoginAsync();
            if (recoveredSession != null)
            {
                await ApplyRecoveredMicrosoftLaunchSessionAsync(recoveredSession);
                return recoveredSession;
            }

            throw new InvalidOperationException("Přihlášenou relaci se nepodařilo obnovit. Přihlas se znovu.");
        }

        return UserSession ?? CmlLib.Core.Auth.MSession.CreateOfflineSession("Guest");
    }

    private async Task<MSession?> TryRestoreSessionForAccountAsync(AccountProfile account)
    {
        if (account.Type == AccountType.Offline)
        {
            return _authService.LoginOffline(account.DisplayName);
        }

        MSession? restoredSession = null;

        if (!string.IsNullOrWhiteSpace(account.MsalAccountId))
        {
            restoredSession = await _authService.TrySilentLoginForAccountAsync(account.MsalAccountId);
            if (IsSessionCompatibleWithAccount(restoredSession, account))
            {
                return restoredSession;
            }
        }

        restoredSession = await _authService.TrySilentLoginAsync();
        return IsSessionCompatibleWithAccount(restoredSession, account) ? restoredSession : null;
    }

    private async Task ApplyResolvedLaunchSessionAsync(MSession session, AccountProfile account)
    {
        UserSession = session;
        IsLoggedIn = true;
        ActiveAccount = account;
        account.LastUsed = DateTime.UtcNow;

        if (account.Type == AccountType.Microsoft)
        {
            account.DisplayName = session.Username;
            account.Uuid = session.UUID;

            var msalAccountId = await _authService.GetLastMsalAccountIdAsync();
            if (!string.IsNullOrWhiteSpace(msalAccountId))
            {
                account.MsalAccountId = msalAccountId;
            }
        }

        OnPropertyChanged(nameof(PlayerSkinUrl));
        SaveAccountProfiles();
    }

    private async Task ApplyRecoveredMicrosoftLaunchSessionAsync(MSession session)
    {
        var msalAccountId = await _authService.GetLastMsalAccountIdAsync();
        var existingProfile = Accounts.FirstOrDefault(account =>
            account.Type == AccountType.Microsoft &&
            (
                (!string.IsNullOrWhiteSpace(msalAccountId) && string.Equals(account.MsalAccountId, msalAccountId, StringComparison.Ordinal)) ||
                AreEquivalentUuids(account.Uuid, session.UUID)
            ));

        if (existingProfile == null)
        {
            existingProfile = new AccountProfile
            {
                DisplayName = session.Username,
                Uuid = session.UUID,
                Type = AccountType.Microsoft,
                MsalAccountId = msalAccountId,
                LastUsed = DateTime.UtcNow
            };
            Accounts.Add(existingProfile);
        }
        else
        {
            existingProfile.DisplayName = session.Username;
            existingProfile.Uuid = session.UUID;
            existingProfile.MsalAccountId = msalAccountId ?? existingProfile.MsalAccountId;
            existingProfile.LastUsed = DateTime.UtcNow;
        }

        UserSession = session;
        IsLoggedIn = true;
        ActiveAccount = existingProfile;
        OnPropertyChanged(nameof(PlayerSkinUrl));
        SaveAccountProfiles();
    }

    private static bool IsSessionCompatibleWithAccount(MSession? session, AccountProfile account)
    {
        if (session == null)
        {
            return false;
        }

        if (account.Type == AccountType.Offline)
        {
            return string.Equals(session.Username, account.DisplayName, StringComparison.OrdinalIgnoreCase);
        }

        if (AreEquivalentUuids(account.Uuid, session.UUID))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(account.DisplayName) &&
            string.Equals(account.DisplayName, session.Username, StringComparison.OrdinalIgnoreCase) &&
            !IsGuestSession(session);
    }

    private static bool AreEquivalentUuids(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        return string.Equals(NormalizeUuid(left), NormalizeUuid(right), StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeUuid(string value)
    {
        return value.Replace("-", string.Empty, StringComparison.Ordinal).Trim();
    }

    private static bool IsGuestSession(MSession? session)
    {
        return session == null ||
            string.IsNullOrWhiteSpace(session.Username) ||
            string.Equals(session.Username, "Guest", StringComparison.OrdinalIgnoreCase);
    }

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

    private async Task ExecUpdateImplementation(ModpackInfo modpack, string modpackDir, string modsDir, ModpackVersion? targetVersion = null)
    {
        try
        {
            await EnsureRequiredModpackVersionInstalledAsync(modpack, modpackDir, modsDir, targetVersion);
        }
        catch (Exception ex)
        {
            var hasInstalledContent = HasInstalledModpackContent(modpackDir, modsDir);
            LogService.Error($"[PlayModpack] Auto-update check failed for {modpack.DisplayLabel}", ex);

            if (!hasInstalledContent)
            {
                throw;
            }

            LaunchStatus = "Nepodařilo se ověřit update, spouštím lokální instalaci...";
            ShowToast("Modpack update", $"Online kontrola updatu selhala, spouštím nainstalovanou verzi {modpack.DisplayLabel}.", ToastSeverity.Warning, 3200);
            _lastManifestInfo = ModpackInstaller.LoadManifestInfo(modpackDir) ?? new ModpackManifestInfo();
        }
    }

    private async Task<string?> FetchModpackChangelogAsync(ModpackInfo modpack)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(modpack.VoidRegistrySlug))
            {
                var accessToken = await ResolveVoidRegistryAccessTokenAsync(modpack, requireFreshSession: false);
                var updateCheck = await _voidRegistryService.GetUpdateCheckAsync(modpack.VoidRegistrySlug, modpack.CurrentVersion?.Name ?? "0.0.0", accessToken);
                return updateCheck?.Latest?.Changelog;
            }
            if (modpack.Source == "Modrinth" && !string.IsNullOrEmpty(modpack.ModrinthId) && modpack.TargetVersion != null && !string.IsNullOrEmpty(modpack.TargetVersion.FileId))
            {
                var response = await _httpClient.GetStringAsync($"https://api.modrinth.com/v2/version/{modpack.TargetVersion.FileId}");
                var node = JsonNode.Parse(response);
                return node?["changelog"]?.ToString();
            }
            if (modpack.Source == "CurseForge" && modpack.ProjectId > 0 && modpack.TargetVersion != null && !string.IsNullOrEmpty(modpack.TargetVersion.FileId))
            {
                var changelogHtml = await _curseForgeApi.GetModFileChangelogAsync(modpack.ProjectId, int.Parse(modpack.TargetVersion.FileId));
                return changelogHtml;
            }
        }
        catch (Exception ex)
        {
            LogService.Error($"Failed to fetch changelog for {modpack.Name}", ex);
        }
        return null;
    }
}
