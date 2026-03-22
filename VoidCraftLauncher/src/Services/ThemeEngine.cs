using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using VoidCraftLauncher.Models;

namespace VoidCraftLauncher.Services;

/// <summary>
/// Manages runtime theme switching by swapping Avalonia resource dictionaries.
/// Currently ships with a single "Obsidian" theme; prepared for future expansion.
/// </summary>
public class ThemeEngine
{
    public const string MotionPreferenceSystem = "system";
    public const string MotionPreferenceFull = "full";
    public const string MotionPreferenceReduced = "reduced";

    private const string ReducedMotionClass = "reduced-motion";
    private const uint SpiGetClientAreaAnimation = 0x1042;

    private readonly Application _app;
    private string _currentThemeId = "obsidian";

    private static readonly IReadOnlyDictionary<string, ThemePalette> ThemePalettes =
        new Dictionary<string, ThemePalette>(StringComparer.OrdinalIgnoreCase)
        {
            ["obsidian"] = new(
                "#07070b", "#0d0d14", "#141420", "#1b1b29", "#242437",
                "#2d2d42", "#3a3a55", "#f5f7ff", "#bfc4e6", "#8086ab",
                "#5b4ecc", "#7c6fff", "#a89cff", "#00d4aa", "#4bffb0", "#ffb347", "#ff4b6b"),
            ["midnight-blue"] = new(
                "#06101a", "#0b1827", "#13243a", "#183049", "#203b59",
                "#294766", "#39597a", "#f2f7ff", "#c0d3ef", "#8196b6",
                "#2f69d9", "#5e96ff", "#9bc3ff", "#56d3ff", "#5de2b5", "#f5c05d", "#ff6b7f"),
            ["emerald"] = new(
                "#07100d", "#0c1714", "#14231d", "#1a2c26", "#213631",
                "#2a4841", "#356058", "#f4fff8", "#c8e7d7", "#87aa98",
                "#0f9f6e", "#24c78a", "#7ef0bb", "#68d9c0", "#6dffb3", "#f3c96b", "#ff6983"),
            ["crimson"] = new(
                "#11070a", "#180c11", "#27131a", "#331922", "#42212c",
                "#53303c", "#66404e", "#fff4f6", "#efc5cf", "#b68a97",
                "#b23458", "#e24f77", "#ff9ab0", "#ff8a65", "#7af0b3", "#ffc066", "#ff5879")
        };

    public string CurrentThemeId => _currentThemeId;

    public string CurrentMotionPreference { get; private set; } = MotionPreferenceSystem;

    public bool IsReducedMotionActive { get; private set; }

    public static readonly List<ThemeInfo> AvailableThemes = new()
    {
        new ThemeInfo
        {
            Id = "obsidian",
            Name = "Obsidian",
            Description = "Ultra-tmavý motiv s fialovými akcenty. Výchozí identita VOID-CRAFT.",
            IsBuiltIn = true,
            ResourceUri = "avares://VoidCraftLauncher/src/Themes/ObsidianTokens.axaml",
            PreviewColors = new[] { "#07070b", "#0d0d14", "#141420", "#5b4ecc", "#7c6fff", "#00d4aa" }
        },
        new ThemeInfo
        {
            Id = "midnight-blue",
            Name = "Midnight Blue",
            Description = "Chladnější noční varianta s modrými akcenty a jasnější navigací.",
            IsBuiltIn = true,
            PreviewColors = new[] { "#06101a", "#13243a", "#203b59", "#2f69d9", "#5e96ff", "#56d3ff" }
        },
        new ThemeInfo
        {
            Id = "emerald",
            Name = "Emerald",
            Description = "Temný přírodní motiv s technickým zeleným akcentem a čistou kontrastní paletou.",
            IsBuiltIn = true,
            PreviewColors = new[] { "#07100d", "#14231d", "#213631", "#0f9f6e", "#24c78a", "#68d9c0" }
        },
        new ThemeInfo
        {
            Id = "crimson",
            Name = "Crimson",
            Description = "Výraznější tmavá paleta pro alert-heavy workflow a agresivnější vizuální rytmus.",
            IsBuiltIn = true,
            PreviewColors = new[] { "#11070a", "#27131a", "#42212c", "#b23458", "#e24f77", "#ff8a65" }
        }
    };

    private sealed record ThemePalette(
        string BgCanvas,
        string BgShell,
        string BgPanel,
        string BgElevated,
        string BgInteractive,
        string StrokeSoft,
        string StrokeStrong,
        string TextPrimary,
        string TextSecondary,
        string TextMuted,
        string PrimaryA,
        string PrimaryB,
        string PrimaryGlow,
        string AccentTeal,
        string Success,
        string Warning,
        string Danger);

    public ThemeEngine(Application app)
    {
        _app = app;
    }

    /// <summary>
    /// Applies the given theme by loading its resource dictionary and replacing the active one.
    /// </summary>
    public bool ApplyTheme(string themeId)
    {
        var theme = AvailableThemes.FirstOrDefault(t => t.Id == themeId);
        if (theme == null || !ThemePalettes.TryGetValue(themeId, out var palette))
        {
            LogService.Error($"ThemeEngine: unknown theme '{themeId}'");
            return false;
        }

        try
        {
            ApplyPalette(palette);

            foreach (var t in AvailableThemes) t.IsActive = false;
            theme.IsActive = true;

            _currentThemeId = themeId;
            LogService.Log($"ThemeEngine: applied theme '{themeId}'");
            return true;
        }
        catch (Exception ex)
        {
            LogService.Error($"ThemeEngine: failed to apply '{themeId}'", ex);
            return false;
        }
    }

    /// <summary>
    /// Returns the currently active theme info.
    /// </summary>
    public ThemeInfo GetCurrentTheme()
    {
        return AvailableThemes.FirstOrDefault(t => t.Id == _currentThemeId)
               ?? AvailableThemes[0];
    }

    public bool ApplyMotionPreference(string? motionPreference, Window? window)
    {
        if (window == null)
            return false;

        var normalizedPreference = NormalizeMotionPreference(motionPreference);
        var useReducedMotion = normalizedPreference == MotionPreferenceReduced
            || (normalizedPreference == MotionPreferenceSystem && IsSystemReducedMotionEnabled());

        CurrentMotionPreference = normalizedPreference;
        IsReducedMotionActive = useReducedMotion;

        if (useReducedMotion)
        {
            if (!window.Classes.Contains(ReducedMotionClass))
                window.Classes.Add(ReducedMotionClass);
        }
        else
        {
            window.Classes.Remove(ReducedMotionClass);
        }

        LogService.Log($"ThemeEngine: motion preference '{CurrentMotionPreference}', reduced-motion={(useReducedMotion ? "on" : "off")}");
        return true;
    }

    public static bool IsSystemReducedMotionEnabled()
    {
        if (!OperatingSystem.IsWindows())
            return false;

        try
        {
            return SystemParametersInfo(SpiGetClientAreaAnimation, 0, out var animationsEnabled, 0)
                && animationsEnabled == 0;
        }
        catch
        {
            return false;
        }
    }

    private void ApplyPalette(ThemePalette palette)
    {
        ApplyColorAndBrush("BgCanvas", "BgCanvasBrush", palette.BgCanvas);
        ApplyColorAndBrush("BgShell", "BgShellBrush", palette.BgShell);
        ApplyColorAndBrush("BgPanel", "BgPanelBrush", palette.BgPanel);
        ApplyColorAndBrush("BgElevated", "BgElevatedBrush", palette.BgElevated);
        ApplyColorAndBrush("BgInteractive", "BgInteractiveBrush", palette.BgInteractive);
        ApplyColorAndBrush("StrokeSoft", "StrokeSoftBrush", palette.StrokeSoft);
        ApplyColorAndBrush("StrokeStrong", "StrokeStrongBrush", palette.StrokeStrong);
        ApplyColorAndBrush("TextPrimary", "TextPrimaryBrush", palette.TextPrimary);
        ApplyColorAndBrush("TextSecondary", "TextSecondaryBrush", palette.TextSecondary);
        ApplyColorAndBrush("TextMuted", "TextMutedBrush", palette.TextMuted);
        ApplyColorAndBrush("PrimaryA", "PrimaryABrush", palette.PrimaryA);
        ApplyColorAndBrush("PrimaryB", "PrimaryBBrush", palette.PrimaryB);
        ApplyColorAndBrush("PrimaryGlow", "PrimaryGlowBrush", palette.PrimaryGlow);
        ApplyColorAndBrush("AccentTeal", "AccentTealBrush", palette.AccentTeal);
        ApplyColorAndBrush("Success", "SuccessBrush", palette.Success);
        ApplyColorAndBrush("Warning", "WarningBrush", palette.Warning);
        ApplyColorAndBrush("Danger", "DangerBrush", palette.Danger);

        ApplyBrush("VoidPrimary", palette.PrimaryB);
        ApplyBrush("VoidPulseDeep", palette.PrimaryA);
        ApplyBrush("VoidPulseGlow", palette.PrimaryGlow);
        ApplyBrush("VoidCrystalEdge", palette.AccentTeal);
        ApplyBrush("VoidBg0", palette.BgCanvas);
        ApplyBrush("VoidBg1", palette.BgShell);
        ApplyBrush("VoidBg2", palette.BgPanel);
        ApplyBrush("VoidBg3", palette.BgInteractive);
        ApplyBrush("VoidBorder", palette.StrokeSoft);
        ApplyBrush("VoidText", palette.TextPrimary);
        ApplyBrush("VoidMuted", palette.TextSecondary);
        ApplyBrush("VoidDimmed", palette.TextMuted);
        ApplyBrush("VoidDanger", palette.Danger);
        ApplyBrush("VoidWarning", palette.Warning);
        ApplyBrush("VoidSuccess", palette.Success);
    }

    private void ApplyColorAndBrush(string colorKey, string brushKey, string hex)
    {
        _app.Resources[colorKey] = Color.Parse(hex);
        ApplyBrush(brushKey, hex);
    }

    private void ApplyBrush(string brushKey, string hex)
    {
        var color = Color.Parse(hex);
        if (_app.Resources.TryGetValue(brushKey, out var existing) && existing is SolidColorBrush brush)
        {
            brush.Color = color;
        }
        else
        {
            _app.Resources[brushKey] = new SolidColorBrush(color);
        }
    }

    private static string NormalizeMotionPreference(string? motionPreference)
    {
        return motionPreference?.Trim().ToLowerInvariant() switch
        {
            MotionPreferenceFull => MotionPreferenceFull,
            MotionPreferenceReduced => MotionPreferenceReduced,
            _ => MotionPreferenceSystem
        };
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, out int pvParam, uint fWinIni);
}
