using System;

namespace VoidCraftLauncher.Models;

/// <summary>
/// Represents theme metadata for the theme engine.
/// </summary>
public class ThemeInfo
{
    public string Id { get; set; } = "obsidian";
    public string Name { get; set; } = "Obsidian";
    public string Description { get; set; } = "";
    public bool IsBuiltIn { get; set; } = true;
    public bool IsActive { get; set; }

    /// <summary>Resource dictionary URI for this theme (e.g. avares://…/Themes/Obsidian.axaml).</summary>
    public string ResourceUri { get; set; } = "";

    /// <summary>Preview swatch colors (hex).</summary>
    public string[] PreviewColors { get; set; } = Array.Empty<string>();

    public string Preview0 => PreviewColors.Length > 0 ? PreviewColors[0] : "#000000";
    public string Preview1 => PreviewColors.Length > 1 ? PreviewColors[1] : Preview0;
    public string Preview2 => PreviewColors.Length > 2 ? PreviewColors[2] : Preview1;
}
