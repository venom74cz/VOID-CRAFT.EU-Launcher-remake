using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace VoidCraftLauncher.Services
{
    public static class ModUtils
    {
        private const string CONFIG_FILE_NAME = "potato_mods.json";

        // Default blacklist if none exists
            "distant-horizons", // Extreme performance impact
            "irisshaders",     // Shaders
            "oculus",          // Shaders alternative
            "shader",          // Generic shader pattern
            "fbp-renewed",     // Fancy Block Particles (Heavy)
            "particular",      // Particles (Heavy)
            "fresh-animations",// Visuals
            "better-third-person", 
            "camera-mod",
            "nice-camera",
            "cameraoverhaul",  // In modlist
            "waveycapes",      // Visual physics
            "skin-layers-3d",  // Visuals
            "3dskinlayers",    // Duplicate pattern
            "not-enough-animations", // Visuals
            "eating-animation", // Visuals
            "effective",       // Visuals (Water splashes etc)
            "visuality",       // Visuals (Particles)
            "legendarytooltips",
            "wakes",           // Simpler physics
            "falling-leaves"   // Visuals
        };

        /// <summary>
        /// Loads potato_mods.json from the instance folder. 
        /// If it doesn't exist, creates it with default values.
        /// </summary>
        public static List<string> GetPotatoModList(string instancePath)
        {
            var configPath = Path.Combine(instancePath, CONFIG_FILE_NAME);
            
            if (!File.Exists(configPath))
            {
                try
                {
                    // Create default config
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    var json = JsonSerializer.Serialize(DEFAULT_POTATO_MODS, options);
                    File.WriteAllText(configPath, json);
                }
                catch (Exception ex)
                {
                    LogService.Error($"Failed to create default {CONFIG_FILE_NAME}", ex);
                    return DEFAULT_POTATO_MODS;
                }
            }

            try
            {
                var json = File.ReadAllText(configPath);
                var list = JsonSerializer.Deserialize<List<string>>(json);
                return list ?? DEFAULT_POTATO_MODS;
            }
            catch (Exception ex)
            {
                LogService.Error($"Failed to load {CONFIG_FILE_NAME}", ex);
                return DEFAULT_POTATO_MODS;
            }
        }

        /// <summary>
        /// Applies or Reverts Potato Mode by renaming files.
        /// </summary>
        /// <param name="modsPath">Path to the 'mods' directory</param>
        /// <param name="instancePath">Path to instance root (where config is located)</param>
        /// <param name="enable">True to DISABLE visual mods (Potato ON), False to ENABLE them (Potato OFF)</param>
        public static void ApplyPotatoMode(string modsPath, string instancePath, bool enable)
        {
            if (!Directory.Exists(modsPath)) return;

            var blacklist = GetPotatoModList(instancePath);
            // Lowercase and distinct for easier matching
            var patterns = blacklist.Select(x => x.ToLowerInvariant()).Distinct().ToList();

            var files = Directory.GetFiles(modsPath);

            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);
                
                // If Potato Mode is ON: We want to disable files (.jar -> .jar.disabled)
                if (enable)
                {
                    // Only process active jars
                    if (!fileName.EndsWith(".jar", StringComparison.OrdinalIgnoreCase)) continue;

                    if (IsMatch(fileName, patterns))
                    {
                        try 
                        {
                            var dest = file + ".disabled";
                            if (File.Exists(dest)) File.Delete(dest); // Cleaner overwrite
                            File.Move(file, dest);
                            LogService.Log($"[PotatoMode] Disabled: {fileName}");
                        }
                        catch (Exception ex)
                        {
                            LogService.Error($"[PotatoMode] Failed to disable {fileName}", ex);
                            // Potentially throw or notify UI? For now just log.
                        }
                    }
                }
                // If Potato Mode is OFF: We want to enable files (.jar.disabled -> .jar)
                else
                {
                    // Only process disabled jars
                    if (!fileName.EndsWith(".jar.disabled", StringComparison.OrdinalIgnoreCase)) continue;

                    // Remove .disabled extension
                    var originalName = fileName.Substring(0, fileName.Length - ".disabled".Length);
                    
                    if (IsMatch(originalName, patterns))
                    {
                        try
                        {
                            var dest = Path.Combine(modsPath, originalName);
                            if (File.Exists(dest)) File.Delete(dest);
                            File.Move(file, dest);
                            LogService.Log($"[PotatoMode] Restored: {originalName}");
                        }
                        catch (Exception ex)
                        {
                            LogService.Error($"[PotatoMode] Failed to restore {fileName}", ex);
                        }
                    }
                }
            }
        }

        private static bool IsMatch(string fileName, List<string> patterns)
        {
            var nameLower = fileName.ToLowerInvariant();
            // simple substring match for now. user can put "oculus" to match "oculus-1.2.3.jar"
            return patterns.Any(p => nameLower.Contains(p));
        }
    }
}
