using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using VoidCraftLauncher.Models.CreatorStudio;

namespace VoidCraftLauncher.ViewModels;

public partial class MainViewModel
{
    // ──── Activity Timeline ────
    public ObservableCollection<CreatorActivityEntry> CreatorActivityTimeline { get; } = new();

    public bool HasCreatorActivityTimeline => CreatorActivityTimeline.Count > 0;

    [ObservableProperty]
    private bool _isCreatorActivityTimelineExpanded;

    private const int ActivityPreviewCount = 5;

    public IEnumerable<CreatorActivityEntry> CreatorActivityTimelinePreview =>
        IsCreatorActivityTimelineExpanded
            ? CreatorActivityTimeline
            : CreatorActivityTimeline.Take(ActivityPreviewCount);

    public bool HasMoreCreatorActivity => CreatorActivityTimeline.Count > ActivityPreviewCount;

    public string CreatorActivityExpandLabel => IsCreatorActivityTimelineExpanded
        ? "Zobrazit mene"
        : $"Zobrazit vsech {CreatorActivityTimeline.Count}";

    partial void OnIsCreatorActivityTimelineExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(CreatorActivityTimelinePreview));
        OnPropertyChanged(nameof(CreatorActivityExpandLabel));
    }

    [RelayCommand]
    private void ToggleCreatorActivityExpanded()
    {
        IsCreatorActivityTimelineExpanded = !IsCreatorActivityTimelineExpanded;
    }

    // ──── Quick Links ────
    public ObservableCollection<CreatorQuickLink> CreatorQuickLinks { get; } = new();

    public bool HasCreatorQuickLinks => CreatorQuickLinks.Count > 0;

    // ──── Project Stats ────
    public string CreatorStatMods
    {
        get
        {
            // Prefer actual jar count over manifest
            var modsDir = string.IsNullOrWhiteSpace(CreatorWorkspaceContext.WorkspacePath)
                ? null
                : System.IO.Path.Combine(CreatorWorkspaceContext.WorkspacePath, "mods");
            if (modsDir != null && System.IO.Directory.Exists(modsDir))
            {
                var jarCount = System.IO.Directory.EnumerateFiles(modsDir, "*.jar", System.IO.SearchOption.TopDirectoryOnly).Count();
                if (jarCount > 0) return $"{jarCount}";
            }
            return CreatorWorkspaceContext.ModCount > 0 ? $"{CreatorWorkspaceContext.ModCount}" : "0";
        }
    }

    public string CreatorStatFiles => CreatorWorkspaceContext.EditableFileCount > 0
        ? $"{CreatorWorkspaceContext.EditableFileCount}"
        : $"{CreatorWorkbenchFiles.Count}";

    public string CreatorStatNotes
    {
        get
        {
            var path = CreatorWorkspaceContext.NotesPath;
            if (string.IsNullOrWhiteSpace(path) || !System.IO.Directory.Exists(path))
                return "0";
            try
            {
                return System.IO.Directory.EnumerateFiles(path, "*.*", System.IO.SearchOption.TopDirectoryOnly)
                    .Count(f => f.EndsWith(".md") || f.EndsWith(".txt"))
                    .ToString();
            }
            catch { return "0"; }
        }
    }

    public string CreatorStatSnapshots => $"{CreatorWorkspaceContext.SnapshotCount}";

    public string CreatorStatExports => $"{CreatorWorkspaceContext.ExportArtifactCount}";

    // ──── Identity Sub-Tab ────
    [ObservableProperty]
    private int _creatorIdentitySubTabIndex;

    // ──── Header state ────
    public string CreatorHeaderPackIdentity
    {
        get
        {
            var name = string.IsNullOrWhiteSpace(CreatorMetadataPackName)
                ? CreatorStudioInstanceLabel
                : CreatorMetadataPackName;
            var version = string.IsNullOrWhiteSpace(CreatorMetadataVersion)
                ? ""
                : $" v{CreatorMetadataVersion}";
            var channel = string.IsNullOrWhiteSpace(CreatorMetadataReleaseChannel)
                ? ""
                : $" {CreatorMetadataReleaseChannel}";
            return $"{name}{version}{channel}";
        }
    }

    public string CreatorHeaderTechStack
    {
        get
        {
            var mc = CreatorStudioMinecraftVersion;
            var loader = CreatorStudioModLoader;
            if (mc == "Nezjištěno" && loader == "Nezjištěno")
                return "Runtime nezjisten";
            return $"MC {mc} · {loader}";
        }
    }

    public string CreatorHeaderGitBadge => CreatorWorkspaceContext.GitStatusLabel;

    public string CreatorHeaderDirtyBadge
    {
        get
        {
            var dirtyCount = 0;
            if (IsCreatorMetadataDirty) dirtyCount++;
            if (CanSaveCreatorWorkbenchFile) dirtyCount++;
            return dirtyCount > 0 ? $"{dirtyCount} neulozenych zmen" : "Vse ulozeno";
        }
    }

    public string CreatorHeaderExportBadge
    {
        get
        {
            var exportCount = CreatorWorkspaceContext.ExportArtifactCount;
            return exportCount > 0 ? $"{exportCount} exportu" : "Bez exportu";
        }
    }

    // ──── Overview methods ────
    public void TrackCreatorActivity(string summary, string? tabContext = null)
    {
        var entry = new CreatorActivityEntry
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            Summary = summary,
            TabContext = tabContext ?? GetCreatorTabLabel(CreatorShellState.SelectedTab)
        };

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            CreatorActivityTimeline.Insert(0, entry);
            while (CreatorActivityTimeline.Count > 50)
                CreatorActivityTimeline.RemoveAt(CreatorActivityTimeline.Count - 1);

            OnPropertyChanged(nameof(HasCreatorActivityTimeline));
            OnPropertyChanged(nameof(CreatorActivityTimelinePreview));
            OnPropertyChanged(nameof(HasMoreCreatorActivity));
            OnPropertyChanged(nameof(CreatorActivityExpandLabel));
        });

        CreatorPreferences.LastActivityUtc = entry.TimestampUtc;
        CreatorPreferences.LastActivitySummary = summary;

        if (SelectedSkinStudioInstance != null)
        {
            RememberRecentWorkspace(SelectedSkinStudioInstance.Id, SelectedSkinStudioInstance.Label);
        }

        PersistCreatorPreferences();
    }

    private void RefreshCreatorQuickLinks()
    {
        CreatorQuickLinks.Clear();

        var manifest = CurrentModpackCreatorManifest;

        var brandProfile = manifest?.BrandProfile;
        if (brandProfile != null)
        {
            if (!string.IsNullOrWhiteSpace(brandProfile.Website))
                CreatorQuickLinks.Add(new CreatorQuickLink { Label = "Website", Url = brandProfile.Website, Kind = "web" });
            if (!string.IsNullOrWhiteSpace(brandProfile.Discord))
                CreatorQuickLinks.Add(new CreatorQuickLink { Label = "Discord", Url = brandProfile.Discord, Kind = "discord" });
            if (!string.IsNullOrWhiteSpace(brandProfile.GitHub))
                CreatorQuickLinks.Add(new CreatorQuickLink { Label = "GitHub", Url = brandProfile.GitHub, Kind = "github" });
            if (!string.IsNullOrWhiteSpace(brandProfile.SupportLink))
                CreatorQuickLinks.Add(new CreatorQuickLink { Label = "Support", Url = brandProfile.SupportLink, Kind = "support" });
        }

        if (CreatorQuickLinks.Count == 0)
        {
            // No fallback — show empty state to guide user to Identity tab
        }

        OnPropertyChanged(nameof(HasCreatorQuickLinks));
    }

    [RelayCommand]
    private void OpenCreatorQuickLink(CreatorQuickLink? link)
    {
        if (link == null || !link.HasUrl) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = link.Url,
                UseShellExecute = true
            });
        }
        catch { /* ignore invalid URL */ }
    }

    private void NotifyOverviewStateChanged()
    {
        OnPropertyChanged(nameof(HasCreatorActivityTimeline));
        OnPropertyChanged(nameof(HasCreatorQuickLinks));
        OnPropertyChanged(nameof(CreatorStatMods));
        OnPropertyChanged(nameof(CreatorStatFiles));
        OnPropertyChanged(nameof(CreatorStatNotes));
        OnPropertyChanged(nameof(CreatorStatSnapshots));
        OnPropertyChanged(nameof(CreatorStatExports));
        OnPropertyChanged(nameof(CreatorHeaderPackIdentity));
        OnPropertyChanged(nameof(CreatorHeaderTechStack));
        OnPropertyChanged(nameof(CreatorHeaderGitBadge));
        OnPropertyChanged(nameof(CreatorHeaderDirtyBadge));
        OnPropertyChanged(nameof(CreatorHeaderExportBadge));
        OnPropertyChanged(nameof(CreatorMetadataAuthorsPreview));
    }
}
