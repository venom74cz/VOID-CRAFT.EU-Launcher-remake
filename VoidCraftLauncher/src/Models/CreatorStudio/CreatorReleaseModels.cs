using System;
using System.Collections.Generic;

namespace VoidCraftLauncher.Models.CreatorStudio;

public enum CreatorReleasePipelineStep
{
    Version,
    Snapshot,
    Validate,
    Notes,
    Publish
}

public enum CreatorReleaseStepStatus
{
    Pending,
    InProgress,
    Completed,
    Warning,
    Failed
}

public sealed class CreatorReleaseStep
{
    public CreatorReleasePipelineStep Step { get; set; }

    public CreatorReleaseStepStatus Status { get; set; } = CreatorReleaseStepStatus.Pending;

    public string Label => Step switch
    {
        CreatorReleasePipelineStep.Version => "Version",
        CreatorReleasePipelineStep.Snapshot => "Snapshot",
        CreatorReleasePipelineStep.Validate => "Validate",
        CreatorReleasePipelineStep.Notes => "Notes",
        CreatorReleasePipelineStep.Publish => "Publish",
        _ => "?"
    };

    public string StatusMarker => Status switch
    {
        CreatorReleaseStepStatus.Completed => "ok",
        CreatorReleaseStepStatus.InProgress => "...",
        CreatorReleaseStepStatus.Warning => "!",
        CreatorReleaseStepStatus.Failed => "x",
        _ => "o"
    };

    public bool IsActive => Status == CreatorReleaseStepStatus.InProgress;

    public bool IsCompleted => Status == CreatorReleaseStepStatus.Completed;
}

public sealed class CreatorValidationCheck
{
    public string Label { get; set; } = string.Empty;

    public bool Passed { get; set; }

    public bool IsWarning { get; set; }

    public string StatusIcon => Passed ? "ok" : IsWarning ? "!" : "x";
}

public sealed class CreatorExportProfile
{
    public string Format { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public bool IsReady { get; set; }

    public string StatusLabel => IsReady ? "Pripraveno" : "Chybi pozadavky";

    public string BlockerLabel { get; set; } = string.Empty;
}

public sealed class CreatorReleaseHistoryEntry
{
    public string Version { get; set; } = string.Empty;

    public string Channel { get; set; } = string.Empty;

    public DateTimeOffset ReleasedUtc { get; set; }

    public int ModCount { get; set; }

    public string ExportFormat { get; set; } = string.Empty;

    public string DateLabel => ReleasedUtc.LocalDateTime.ToString("yyyy-MM-dd");

    public string Summary => $"{Version}  {Channel}  {DateLabel}  {ModCount} mods  {ExportFormat}";
}
