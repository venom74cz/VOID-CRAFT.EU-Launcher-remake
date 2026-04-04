using System;
using System.Collections.Generic;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace VoidCraftLauncher.Models.CreatorStudio;

public enum CreatorWorkbenchEditorMode
{
    Structured,
    Raw,
    Split,
    Diff
}

public enum CreatorWorkbenchDocumentKind
{
    Text,
    Json,
    Json5,
    Toml,
    Yaml,
    Ini,
    Properties,
    Markdown,
    Script,
    Csv,
    Snbt
}

public enum CreatorWorkbenchValueKind
{
    String,
    Integer,
    Decimal,
    Boolean,
    Null
}

public enum CreatorWorkbenchNodeKind
{
    Object,
    Array,
    Scalar,
    Section,
    Heading,
    ScriptBlock
}

public enum CreatorWorkbenchIssueSeverity
{
    Info,
    Warning,
    Error
}

public enum CreatorWorkbenchComparisonTargetKind
{
    Loaded,
    Snapshot,
    Export,
    DefaultCounterpart
}

public enum CreatorWorkbenchDiffKind
{
    Unchanged,
    Inserted,
    Deleted,
    Modified
}

public sealed partial class CreatorWorkbenchStructuredField : ObservableObject
{
    public string PathKey { get; init; } = string.Empty;

    public string DisplayPath { get; init; } = string.Empty;

    public string SectionLabel { get; init; } = string.Empty;

    public string ValueTypeLabel { get; init; } = string.Empty;

    public CreatorWorkbenchValueKind ValueKind { get; init; }

    public int Depth { get; init; }

    public string OriginalValue { get; init; } = string.Empty;

    [ObservableProperty]
    private string _editableValue = string.Empty;

    public bool IsModified => !string.Equals(OriginalValue, EditableValue, StringComparison.Ordinal);

    partial void OnEditableValueChanged(string value)
    {
        OnPropertyChanged(nameof(IsModified));
    }
}

public sealed class CreatorWorkbenchOutlineItem
{
    public string PathKey { get; init; } = string.Empty;

    public string DisplayPath { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public string KindLabel { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public int Depth { get; init; }
}

public sealed class CreatorWorkbenchValidationIssue
{
    private static readonly IBrush InfoBrushValue = new SolidColorBrush(Color.Parse("#7bb8ff"));
    private static readonly IBrush WarningBrushValue = new SolidColorBrush(Color.Parse("#f4c46b"));
    private static readonly IBrush ErrorBrushValue = new SolidColorBrush(Color.Parse("#ff6b7a"));

    public CreatorWorkbenchIssueSeverity Severity { get; init; }

    public string Scope { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public string SeverityLabel => Severity switch
    {
        CreatorWorkbenchIssueSeverity.Info => "Info",
        CreatorWorkbenchIssueSeverity.Warning => "Warning",
        CreatorWorkbenchIssueSeverity.Error => "Error",
        _ => "Info"
    };

    public bool IsError => Severity == CreatorWorkbenchIssueSeverity.Error;

    public IBrush AccentBrush => Severity switch
    {
        CreatorWorkbenchIssueSeverity.Info => InfoBrushValue,
        CreatorWorkbenchIssueSeverity.Warning => WarningBrushValue,
        CreatorWorkbenchIssueSeverity.Error => ErrorBrushValue,
        _ => InfoBrushValue
    };
}

public sealed class CreatorWorkbenchComparisonTarget
{
    public string Id { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    internal CreatorWorkbenchComparisonTargetKind Kind { get; init; }

    internal string SourcePath { get; init; } = string.Empty;

    internal string RelativeEntryPath { get; init; } = string.Empty;
}

public sealed class CreatorWorkbenchDiffLine
{
    private static readonly IBrush InsertedBackground = new SolidColorBrush(Color.Parse("#182d2a"));
    private static readonly IBrush DeletedBackground = new SolidColorBrush(Color.Parse("#311c23"));
    private static readonly IBrush ModifiedBackground = new SolidColorBrush(Color.Parse("#1f2742"));
    private static readonly IBrush UnchangedBackground = Brushes.Transparent;
    private static readonly IBrush InsertedForeground = new SolidColorBrush(Color.Parse("#8ff0c6"));
    private static readonly IBrush DeletedForeground = new SolidColorBrush(Color.Parse("#ff9db0"));
    private static readonly IBrush ModifiedForeground = new SolidColorBrush(Color.Parse("#9bb1ff"));
    private static readonly IBrush UnchangedForeground = new SolidColorBrush(Color.Parse("#d9dbe7"));

    public int? OldLineNumber { get; init; }

    public int? NewLineNumber { get; init; }

    public string Text { get; init; } = string.Empty;

    public CreatorWorkbenchDiffKind Kind { get; init; }

    public string ChangeMarker => Kind switch
    {
        CreatorWorkbenchDiffKind.Inserted => "+",
        CreatorWorkbenchDiffKind.Deleted => "-",
        CreatorWorkbenchDiffKind.Modified => "~",
        _ => " "
    };

    public string OldLineLabel => OldLineNumber?.ToString() ?? string.Empty;

    public string NewLineLabel => NewLineNumber?.ToString() ?? string.Empty;

    public IBrush Background => Kind switch
    {
        CreatorWorkbenchDiffKind.Inserted => InsertedBackground,
        CreatorWorkbenchDiffKind.Deleted => DeletedBackground,
        CreatorWorkbenchDiffKind.Modified => ModifiedBackground,
        _ => UnchangedBackground
    };

    public IBrush Foreground => Kind switch
    {
        CreatorWorkbenchDiffKind.Inserted => InsertedForeground,
        CreatorWorkbenchDiffKind.Deleted => DeletedForeground,
        CreatorWorkbenchDiffKind.Modified => ModifiedForeground,
        _ => UnchangedForeground
    };
}

public sealed class CreatorWorkbenchDocumentAnalysis
{
    public CreatorWorkbenchDocumentKind Kind { get; init; }

    public string KindLabel { get; init; } = string.Empty;

    public string ParserLabel { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public bool SupportsStructuredEditing { get; init; }

    public bool StructuredSaveIsLossy { get; init; }

    public string StructuredSaveWarning { get; init; } = string.Empty;

    public string RecommendedModeId { get; init; } = "raw";

    public IReadOnlyList<CreatorWorkbenchStructuredField> Fields { get; init; } = Array.Empty<CreatorWorkbenchStructuredField>();

    public IReadOnlyList<CreatorWorkbenchOutlineItem> Outline { get; init; } = Array.Empty<CreatorWorkbenchOutlineItem>();

    public IReadOnlyList<CreatorWorkbenchValidationIssue> Issues { get; init; } = Array.Empty<CreatorWorkbenchValidationIssue>();

    internal CreatorWorkbenchNodeKind RootNodeKind { get; init; } = CreatorWorkbenchNodeKind.Object;

    internal IReadOnlyList<CreatorWorkbenchNodeDefinition> NodeDefinitions { get; init; } = Array.Empty<CreatorWorkbenchNodeDefinition>();
}

public sealed class CreatorWorkbenchStructuredRenderResult
{
    public string Content { get; init; } = string.Empty;

    public IReadOnlyList<CreatorWorkbenchValidationIssue> Issues { get; init; } = Array.Empty<CreatorWorkbenchValidationIssue>();
}

internal sealed class CreatorWorkbenchNodeDefinition
{
    public string PathKey { get; init; } = string.Empty;

    public CreatorWorkbenchNodeKind Kind { get; init; }

    public int Depth { get; init; }
}
