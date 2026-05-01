using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VoidCraftLauncher.Models.CreatorStudio;

public enum CreatorShellTab
{
    Overview,
    Identity,
    Mods,
    Files,
    Notes,
    Git,
    Release
}

public enum CreatorIdentitySubTab
{
    Profile,
    Branding
}

public enum CreatorModAttentionKind
{
    UpdateAvailable,
    Conflict,
    MissingDependency
}

public sealed class CreatorModAttentionItem
{
    public string ModName { get; set; } = string.Empty;

    public CreatorModAttentionKind Kind { get; set; }

    public string Detail { get; set; } = string.Empty;

    public string KindIcon => Kind switch
    {
        CreatorModAttentionKind.UpdateAvailable => "!",
        CreatorModAttentionKind.Conflict => "x",
        CreatorModAttentionKind.MissingDependency => "?",
        _ => "o"
    };

    public string KindLabel => Kind switch
    {
        CreatorModAttentionKind.UpdateAvailable => "Update",
        CreatorModAttentionKind.Conflict => "Konflikt",
        CreatorModAttentionKind.MissingDependency => "Závislost",
        _ => "Neznámý"
    };
}

public enum CreatorRightDockMode
{
    ContextDock,
    CopilotDesk
}

public enum CreatorSecondaryDrawerMode
{
    None,
    Notes
}

public enum CreatorScopeKind
{
    Workspace,
    SingleFile,
    MultiFile,
    Folder,
    Notes,
    Canvas,
    ReleaseBoard
}

public sealed class CreatorScope
{
    public CreatorScopeKind Kind { get; set; } = CreatorScopeKind.Workspace;

    public string Label { get; set; } = "Workspace";

    public string? RelativePath { get; set; }

    public int ItemCount { get; set; }

    [JsonIgnore]
    public string Summary => Kind switch
    {
        CreatorScopeKind.SingleFile when !string.IsNullOrWhiteSpace(RelativePath) => $"Soubor {RelativePath}",
        CreatorScopeKind.MultiFile => $"{ItemCount} souboru v scope",
        CreatorScopeKind.Folder when !string.IsNullOrWhiteSpace(RelativePath) => $"Slozka {RelativePath}",
        CreatorScopeKind.Notes => string.IsNullOrWhiteSpace(RelativePath) ? "Notes workspace" : $"Notes {RelativePath}",
        CreatorScopeKind.Canvas => "Canvas",
        CreatorScopeKind.ReleaseBoard => "Release board",
        _ => Label
    };
}

public sealed class CreatorShellState
{
    public CreatorShellTab SelectedTab { get; set; } = CreatorShellTab.Overview;

    public string SelectedSubview { get; set; } = "summary";

    public CreatorScope SelectedScope { get; set; } = new();

    public CreatorRightDockMode RightDockMode { get; set; } = CreatorRightDockMode.CopilotDesk;

    public CreatorSecondaryDrawerMode SecondaryDrawerMode { get; set; } = CreatorSecondaryDrawerMode.None;

    public bool HasUnsavedFileChanges { get; set; }

    public bool HasWorkspaceChanges { get; set; }

    public bool HasReleaseWarnings { get; set; }

    public string LastOpenWorkspaceSection { get; set; } = "overview";
}

public sealed class CreatorRecentWorkspace
{
    public string WorkspaceId { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public DateTimeOffset LastAccessedUtc { get; set; }
}

public sealed class CreatorStudioPreferences
{
    public string? SelectedWorkspaceId { get; set; }

    public string GitHubLastRepositoryOwner { get; set; } = string.Empty;

    public bool GitHubDefaultPrivateRepository { get; set; }

    public List<CreatorRecentWorkspace> RecentWorkspaces { get; set; } = new();

    public CreatorShellState ShellState { get; set; } = new();

    public DateTimeOffset? LastActivityUtc { get; set; }

    public string LastActivitySummary { get; set; } = "Creator workspace is ready.";
}

public sealed class CreatorWorkspaceContext
{
    public string WorkspaceId { get; set; } = string.Empty;

    public string WorkspaceLabel { get; set; } = "Bez zvolene instance";

    public string WorkspacePath { get; set; } = string.Empty;

    public string CreatorManifestPath { get; set; } = CreatorManifestFileName;

    public bool HasCreatorManifest { get; set; }

    public string MinecraftVersion { get; set; } = "Nezjisteno";

    public string LoaderLabel { get; set; } = "Nezjisteno";

    public string ModpackVersion { get; set; } = "Nezjisteno";

    public int ModCount { get; set; }

    public string LinkedServersLabel { get; set; } = "Zadne navazane servery";

    public int EditableFileCount { get; set; }

    public string NotesPath { get; set; } = string.Empty;

    public bool HasNotesWorkspace { get; set; }

    public bool HasGitRepository { get; set; }

    public string GitBranchName { get; set; } = "Git neni inicializovan";

    public bool? HasDirtyWorkingTree { get; set; }

    public int SnapshotCount { get; set; }

    public int ExportArtifactCount { get; set; }

    public bool RequiresSnapshotBeforeApply { get; set; } = true;

    public CreatorScope ActiveScope { get; set; } = new();

    public CreatorShellState ShellState { get; set; } = new();

    public DateTimeOffset? LastActivityUtc { get; set; }

    public string LastActivitySummary { get; set; } = "Bez zaznamenane aktivity.";

    public List<CreatorRecentWorkspace> RecentWorkspaces { get; set; } = new();

    public List<string> ExistingStandardFolders { get; set; } = new();

    public List<string> MissingStandardFolders { get; set; } = new();

    [JsonIgnore]
    public bool HasWorkspace => !string.IsNullOrWhiteSpace(WorkspaceId);

    [JsonIgnore]
    public string GitStatusLabel => !HasGitRepository
        ? "Git repository zatim chybi"
        : HasDirtyWorkingTree switch
        {
            true => $"{GitBranchName} • zmeny cekaji v working tree",
            false => $"{GitBranchName} • working tree je cisty",
            _ => GitBranchName
        };

    [JsonIgnore]
    public string WorkspaceFoldersSummary => MissingStandardFolders.Count == 0
        ? $"Workspace obsahuje vsech {ExistingStandardFolders.Count} standardnich slozek."
        : $"Pripraveno {ExistingStandardFolders.Count} slozek, chybi {MissingStandardFolders.Count}.";

    [JsonIgnore]
    public string LastActivityLabel => LastActivityUtc.HasValue
        ? $"{LastActivitySummary} • {LastActivityUtc.Value.LocalDateTime:dd.MM.yyyy HH:mm}"
        : LastActivitySummary;

    public const string CreatorManifestFileName = "creator_manifest.json";
}