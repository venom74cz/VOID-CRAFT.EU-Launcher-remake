using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VoidCraftLauncher.Models;
using VoidCraftLauncher.Models.CreatorStudio;

namespace VoidCraftLauncher.ViewModels;

public partial class MainViewModel
{
    private const string CreatorWorkbenchModeStructuredId = "structured";
    private const string CreatorWorkbenchModeRawId = "raw";
    private const string CreatorWorkbenchModeSplitId = "split";
    private const string CreatorWorkbenchModeDiffId = "diff";

    private CreatorWorkbenchDocumentAnalysis? _creatorWorkbenchDocumentAnalysis;
    private List<CreatorWorkbenchValidationIssue> _creatorWorkbenchStructuredRenderIssues = new();
    private readonly Dictionary<string, string> _creatorWorkbenchComparisonCache = new(StringComparer.OrdinalIgnoreCase);
    private bool _isAdjustingCreatorWorkbenchMode;
    private bool _suppressCreatorWorkbenchRawAnalysis;
    private int _creatorWorkbenchDiffRefreshVersion;

    public ObservableCollection<SelectionOption> CreatorWorkbenchModeOptions { get; } = new();

    public ObservableCollection<CreatorWorkbenchOutlineItem> CreatorWorkbenchOutlineItems { get; } = new();

    public ObservableCollection<CreatorWorkbenchOutlineItem> FilteredCreatorWorkbenchOutlineItems { get; } = new();

    public ObservableCollection<CreatorWorkbenchStructuredField> CreatorWorkbenchStructuredFields { get; } = new();

    public ObservableCollection<CreatorWorkbenchStructuredField> FilteredCreatorWorkbenchStructuredFields { get; } = new();

    public ObservableCollection<CreatorWorkbenchValidationIssue> CreatorWorkbenchValidationIssues { get; } = new();

    public ObservableCollection<CreatorWorkbenchComparisonTarget> CreatorWorkbenchComparisonTargets { get; } = new();

    public ObservableCollection<CreatorWorkbenchDiffLine> CreatorWorkbenchDiffLines { get; } = new();

    [ObservableProperty]
    private SelectionOption? _selectedCreatorWorkbenchMode;

    [ObservableProperty]
    private CreatorWorkbenchOutlineItem? _selectedCreatorWorkbenchOutlineItem;

    [ObservableProperty]
    private CreatorWorkbenchComparisonTarget? _selectedCreatorWorkbenchComparisonTarget;

    [ObservableProperty]
    private string _creatorWorkbenchInspectorQuery = string.Empty;

    [ObservableProperty]
    private string _creatorWorkbenchDiffStatus = "Porovnání čeká na otevřený soubor.";

    public string CreatorWorkbenchModeLabel => SelectedCreatorWorkbenchMode?.Label ?? "Editor";

    public string CreatorWorkbenchDocumentKindLabel => _creatorWorkbenchDocumentAnalysis?.KindLabel ?? "Text";

    public string CreatorWorkbenchParserLabel => _creatorWorkbenchDocumentAnalysis?.ParserLabel ?? "Textový soubor";

    public string CreatorWorkbenchDocumentSummary => _creatorWorkbenchDocumentAnalysis?.Summary ?? "Vyber soubor vlevo a jeho obsah se otevře tady.";

    public bool CanUseCreatorWorkbenchStructuredEditing => _creatorWorkbenchDocumentAnalysis?.SupportsStructuredEditing == true && CreatorWorkbenchStructuredFields.Count > 0;

    public bool HasCreatorWorkbenchOutlineItems => CreatorWorkbenchOutlineItems.Count > 0;

    public bool HasFilteredCreatorWorkbenchOutlineItems => FilteredCreatorWorkbenchOutlineItems.Count > 0;

    public bool HasCreatorWorkbenchStructuredFields => CreatorWorkbenchStructuredFields.Count > 0;

    public bool HasFilteredCreatorWorkbenchStructuredFields => FilteredCreatorWorkbenchStructuredFields.Count > 0;

    public bool HasCreatorWorkbenchValidationIssues => CreatorWorkbenchValidationIssues.Count > 0;

    public bool HasCreatorWorkbenchComparisonTargets => CreatorWorkbenchComparisonTargets.Count > 0;

    public bool HasCreatorWorkbenchDiffLines => CreatorWorkbenchDiffLines.Count > 0;

    public bool HasCreatorWorkbenchStructureWarning => !string.IsNullOrWhiteSpace(CreatorWorkbenchStructureWarning);

    public string CreatorWorkbenchStructureWarning => _creatorWorkbenchDocumentAnalysis?.StructuredSaveWarning ?? string.Empty;

    public bool CanOpenCreatorWorkbenchFileLocation => SelectedCreatorWorkbenchFile != null;

    public bool CanResetCreatorWorkbenchInspector => !string.IsNullOrWhiteSpace(CreatorWorkbenchInspectorQuery) || SelectedCreatorWorkbenchOutlineItem != null;

    public bool IsCreatorWorkbenchStructuredMode => string.Equals(SelectedCreatorWorkbenchMode?.Id, CreatorWorkbenchModeStructuredId, StringComparison.OrdinalIgnoreCase);

    public bool IsCreatorWorkbenchRawMode => string.Equals(SelectedCreatorWorkbenchMode?.Id, CreatorWorkbenchModeRawId, StringComparison.OrdinalIgnoreCase);

    public bool IsCreatorWorkbenchSplitMode => string.Equals(SelectedCreatorWorkbenchMode?.Id, CreatorWorkbenchModeSplitId, StringComparison.OrdinalIgnoreCase);

    public bool IsCreatorWorkbenchDiffMode => string.Equals(SelectedCreatorWorkbenchMode?.Id, CreatorWorkbenchModeDiffId, StringComparison.OrdinalIgnoreCase);

    public bool ShowCreatorWorkbenchStructuredInspector => CanUseCreatorWorkbenchStructuredEditing && (IsCreatorWorkbenchStructuredMode || IsCreatorWorkbenchSplitMode);

    public bool ShowCreatorWorkbenchRawEditor => IsCreatorWorkbenchRawMode || IsCreatorWorkbenchSplitMode;

    public bool ShowCreatorWorkbenchStructuredPreview => IsCreatorWorkbenchStructuredMode;

    public bool ShowCreatorWorkbenchDiffViewer => IsCreatorWorkbenchDiffMode;

    public string CreatorWorkbenchOutlineSummary
    {
        get
        {
            if (!HasCreatorWorkbenchOutlineItems)
            {
                return "Pro tenhle soubor zatím není k dispozici rychlá navigace.";
            }

            if (SelectedCreatorWorkbenchOutlineItem != null)
            {
                return $"Zaměřeno na: {SelectedCreatorWorkbenchOutlineItem.DisplayPath}";
            }

            return string.IsNullOrWhiteSpace(CreatorWorkbenchInspectorQuery)
                ? $"{CreatorWorkbenchOutlineItems.Count} navigačních bodů"
                : $"{FilteredCreatorWorkbenchOutlineItems.Count}/{CreatorWorkbenchOutlineItems.Count} bodů po filtru";
        }
    }

    public string CreatorWorkbenchStructuredSummary
    {
        get
        {
            if (!CanUseCreatorWorkbenchStructuredEditing)
            {
                return "U tohohle souboru zůstává hlavní práce v textovém editoru.";
            }

            return string.IsNullOrWhiteSpace(CreatorWorkbenchInspectorQuery) && SelectedCreatorWorkbenchOutlineItem == null
                ? $"{CreatorWorkbenchStructuredFields.Count} hodnot připravených k úpravě"
                : $"{FilteredCreatorWorkbenchStructuredFields.Count}/{CreatorWorkbenchStructuredFields.Count} hodnot po filtru";
        }
    }

    public string CreatorWorkbenchValidationSummary
    {
        get
        {
            if (!HasCreatorWorkbenchValidationIssues)
            {
                return CanUseCreatorWorkbenchStructuredEditing
                    ? "Soubor je připravený k úpravám."
                    : "Bez aktivních problémů v textovém režimu.";
            }

            var errorCount = CreatorWorkbenchValidationIssues.Count(issue => issue.IsError);
            var warningCount = CreatorWorkbenchValidationIssues.Count - errorCount;
            return $"{errorCount} chyb, {warningCount} upozornění";
        }
    }

    public string CreatorWorkbenchComparisonSummary => SelectedCreatorWorkbenchComparisonTarget == null
        ? "Vyber verzi souboru, proti které chceš porovnávat změny."
        : $"{SelectedCreatorWorkbenchComparisonTarget.Label} • {SelectedCreatorWorkbenchComparisonTarget.Summary}";

    public string CreatorWorkbenchDiffSummary
    {
        get
        {
            if (!HasSelectedCreatorWorkbenchFile)
            {
                return "Porovnání čeká na otevřený soubor.";
            }

            var changedLines = CreatorWorkbenchDiffLines.Count(line => line.Kind != CreatorWorkbenchDiffKind.Unchanged);
            return changedLines == 0
                ? CreatorWorkbenchDiffStatus
                : $"{changedLines} změněných řádků • {CreatorWorkbenchDiffStatus}";
        }
    }

    private void InitializeCreatorWorkbenchEditorState()
    {
        if (CreatorWorkbenchModeOptions.Count == 0)
        {
            CreatorWorkbenchModeOptions.Add(new SelectionOption { Id = CreatorWorkbenchModeStructuredId, Label = "Form" });
            CreatorWorkbenchModeOptions.Add(new SelectionOption { Id = CreatorWorkbenchModeSplitId, Label = "Editor" });
            CreatorWorkbenchModeOptions.Add(new SelectionOption { Id = CreatorWorkbenchModeDiffId, Label = "Porovnání" });
        }

        SetCreatorWorkbenchModeById(CreatorWorkbenchModeSplitId);
        NotifyCreatorWorkbenchEditorVisualStateChanged();
    }

    private void NotifyCreatorWorkbenchEditorVisualStateChanged()
    {
        OnPropertyChanged(nameof(CreatorWorkbenchModeLabel));
        OnPropertyChanged(nameof(CreatorWorkbenchDocumentKindLabel));
        OnPropertyChanged(nameof(CreatorWorkbenchParserLabel));
        OnPropertyChanged(nameof(CreatorWorkbenchDocumentSummary));
        OnPropertyChanged(nameof(CanUseCreatorWorkbenchStructuredEditing));
        OnPropertyChanged(nameof(HasCreatorWorkbenchOutlineItems));
        OnPropertyChanged(nameof(HasFilteredCreatorWorkbenchOutlineItems));
        OnPropertyChanged(nameof(HasCreatorWorkbenchStructuredFields));
        OnPropertyChanged(nameof(HasFilteredCreatorWorkbenchStructuredFields));
        OnPropertyChanged(nameof(HasCreatorWorkbenchValidationIssues));
        OnPropertyChanged(nameof(HasCreatorWorkbenchComparisonTargets));
        OnPropertyChanged(nameof(HasCreatorWorkbenchDiffLines));
        OnPropertyChanged(nameof(HasCreatorWorkbenchStructureWarning));
        OnPropertyChanged(nameof(CreatorWorkbenchStructureWarning));
        OnPropertyChanged(nameof(CreatorWorkbenchOutlineSummary));
        OnPropertyChanged(nameof(CreatorWorkbenchStructuredSummary));
        OnPropertyChanged(nameof(CreatorWorkbenchValidationSummary));
        OnPropertyChanged(nameof(CreatorWorkbenchComparisonSummary));
        OnPropertyChanged(nameof(CreatorWorkbenchDiffSummary));
        OnPropertyChanged(nameof(CanOpenCreatorWorkbenchFileLocation));
        OnPropertyChanged(nameof(CanResetCreatorWorkbenchInspector));
        OnPropertyChanged(nameof(IsCreatorWorkbenchStructuredMode));
        OnPropertyChanged(nameof(IsCreatorWorkbenchRawMode));
        OnPropertyChanged(nameof(IsCreatorWorkbenchSplitMode));
        OnPropertyChanged(nameof(IsCreatorWorkbenchDiffMode));
        OnPropertyChanged(nameof(ShowCreatorWorkbenchStructuredInspector));
        OnPropertyChanged(nameof(ShowCreatorWorkbenchRawEditor));
        OnPropertyChanged(nameof(ShowCreatorWorkbenchStructuredPreview));
        OnPropertyChanged(nameof(ShowCreatorWorkbenchDiffViewer));
        OnPropertyChanged(nameof(CreatorWorkbenchSelectionMeta));
    }

    private void ClearCreatorWorkbenchEditorState()
    {
        foreach (var field in CreatorWorkbenchStructuredFields)
        {
            field.PropertyChanged -= OnCreatorWorkbenchStructuredFieldChanged;
        }

        _creatorWorkbenchDocumentAnalysis = null;
        _creatorWorkbenchStructuredRenderIssues = new List<CreatorWorkbenchValidationIssue>();
        _creatorWorkbenchComparisonCache.Clear();
        CreatorWorkbenchOutlineItems.Clear();
        FilteredCreatorWorkbenchOutlineItems.Clear();
        CreatorWorkbenchStructuredFields.Clear();
        FilteredCreatorWorkbenchStructuredFields.Clear();
        CreatorWorkbenchValidationIssues.Clear();
        CreatorWorkbenchComparisonTargets.Clear();
        CreatorWorkbenchDiffLines.Clear();
        SelectedCreatorWorkbenchOutlineItem = null;
        SelectedCreatorWorkbenchComparisonTarget = null;
        CreatorWorkbenchDiffStatus = "Porovnání čeká na otevřený soubor.";
        EnsureCreatorWorkbenchMode(preferRecommendedMode: true);
        NotifyCreatorWorkbenchEditorVisualStateChanged();
    }

    private void HandleCreatorWorkbenchContentChanged()
    {
        if (_suppressCreatorWorkbenchRawAnalysis)
        {
            NotifyCreatorWorkbenchEditorVisualStateChanged();
            return;
        }

        if (SelectedCreatorWorkbenchFile == null)
        {
            ClearCreatorWorkbenchEditorState();
            return;
        }

        _creatorWorkbenchStructuredRenderIssues = new List<CreatorWorkbenchValidationIssue>();
        RebuildCreatorWorkbenchEditorState(SelectedCreatorWorkbenchFile, CreatorWorkbenchContent ?? string.Empty, preferRecommendedMode: false);
        CreatorWorkbenchStatus = $"Rozpracováno: {SelectedCreatorWorkbenchFile.RelativePath}";
    }

    private void RebuildCreatorWorkbenchEditorState(CreatorWorkbenchFile file, string content, bool preferRecommendedMode)
    {
        _creatorWorkbenchDocumentAnalysis = _creatorWorkbenchEditorService.Analyze(file, content);
        ReplaceCreatorWorkbenchOutlineItems(_creatorWorkbenchDocumentAnalysis.Outline);
        ReplaceCreatorWorkbenchStructuredFields(_creatorWorkbenchDocumentAnalysis.Fields);
        RebuildCreatorWorkbenchComparisonTargets(file);

        if (SelectedCreatorWorkbenchOutlineItem != null &&
            !CreatorWorkbenchOutlineItems.Any(item => string.Equals(item.PathKey, SelectedCreatorWorkbenchOutlineItem.PathKey, StringComparison.Ordinal)))
        {
            SelectedCreatorWorkbenchOutlineItem = null;
        }

        ApplyCreatorWorkbenchInspectorFilter();
        UpdateCreatorWorkbenchValidationIssues();
        EnsureCreatorWorkbenchMode(preferRecommendedMode);
        _ = RefreshCreatorWorkbenchDiffAsync();
        NotifyCreatorWorkbenchEditorVisualStateChanged();
    }

    private void ReplaceCreatorWorkbenchOutlineItems(IEnumerable<CreatorWorkbenchOutlineItem> items)
    {
        CreatorWorkbenchOutlineItems.Clear();
        foreach (var item in items)
        {
            CreatorWorkbenchOutlineItems.Add(item);
        }
    }

    private void ReplaceCreatorWorkbenchStructuredFields(IEnumerable<CreatorWorkbenchStructuredField> fields)
    {
        foreach (var field in CreatorWorkbenchStructuredFields)
        {
            field.PropertyChanged -= OnCreatorWorkbenchStructuredFieldChanged;
        }

        CreatorWorkbenchStructuredFields.Clear();
        foreach (var field in fields)
        {
            field.PropertyChanged += OnCreatorWorkbenchStructuredFieldChanged;
            CreatorWorkbenchStructuredFields.Add(field);
        }
    }

    private void RebuildCreatorWorkbenchComparisonTargets(CreatorWorkbenchFile file)
    {
        var previousSelectionId = SelectedCreatorWorkbenchComparisonTarget?.Id;
        _creatorWorkbenchComparisonCache.Clear();

        var targets = _creatorWorkbenchEditorService.BuildComparisonTargets(
            SelectedSkinStudioInstance?.Id ?? CreatorPreferences.SelectedWorkspaceId,
            CreatorStudioInstancePath,
            file);

        CreatorWorkbenchComparisonTargets.Clear();
        foreach (var target in targets)
        {
            CreatorWorkbenchComparisonTargets.Add(target);
        }

        SelectedCreatorWorkbenchComparisonTarget = CreatorWorkbenchComparisonTargets.FirstOrDefault(target =>
                string.Equals(target.Id, previousSelectionId, StringComparison.OrdinalIgnoreCase))
            ?? CreatorWorkbenchComparisonTargets.FirstOrDefault();
    }

    private void ApplyCreatorWorkbenchInspectorFilter()
    {
        var query = CreatorWorkbenchInspectorQuery?.Trim();
        var outlineItems = string.IsNullOrWhiteSpace(query)
            ? CreatorWorkbenchOutlineItems
            : new ObservableCollection<CreatorWorkbenchOutlineItem>(CreatorWorkbenchOutlineItems.Where(item =>
                item.DisplayPath.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.Label.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.Summary.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.KindLabel.Contains(query, StringComparison.OrdinalIgnoreCase)));

        FilteredCreatorWorkbenchOutlineItems.Clear();
        foreach (var item in outlineItems)
        {
            FilteredCreatorWorkbenchOutlineItems.Add(item);
        }

        IEnumerable<CreatorWorkbenchStructuredField> structuredFields = CreatorWorkbenchStructuredFields;
        if (SelectedCreatorWorkbenchOutlineItem != null)
        {
            var focusPath = SelectedCreatorWorkbenchOutlineItem.PathKey;
            structuredFields = structuredFields.Where(field =>
                string.Equals(field.PathKey, focusPath, StringComparison.Ordinal) ||
                (!string.IsNullOrWhiteSpace(focusPath) && field.PathKey.StartsWith(focusPath + "/", StringComparison.Ordinal)));
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            structuredFields = structuredFields.Where(field =>
                field.DisplayPath.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                field.SectionLabel.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                field.EditableValue.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                field.ValueTypeLabel.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        FilteredCreatorWorkbenchStructuredFields.Clear();
        foreach (var field in structuredFields)
        {
            FilteredCreatorWorkbenchStructuredFields.Add(field);
        }

        NotifyCreatorWorkbenchEditorVisualStateChanged();
    }

    private void UpdateCreatorWorkbenchValidationIssues()
    {
        CreatorWorkbenchValidationIssues.Clear();
        var uniqueIssues = (_creatorWorkbenchDocumentAnalysis?.Issues ?? Array.Empty<CreatorWorkbenchValidationIssue>())
            .Concat(_creatorWorkbenchStructuredRenderIssues)
            .GroupBy(issue => $"{issue.SeverityLabel}|{issue.Scope}|{issue.Message}", StringComparer.Ordinal)
            .Select(group => group.First());

        foreach (var issue in uniqueIssues)
        {
            CreatorWorkbenchValidationIssues.Add(issue);
        }
    }

    private void EnsureCreatorWorkbenchMode(bool preferRecommendedMode)
    {
        var preferredModeId = preferRecommendedMode
            ? ResolveRecommendedCreatorWorkbenchModeId()
            : (SelectedCreatorWorkbenchMode?.Id ?? ResolveRecommendedCreatorWorkbenchModeId());

        if (!CanActivateCreatorWorkbenchMode(preferredModeId))
        {
            preferredModeId = ResolveRecommendedCreatorWorkbenchModeId();
        }

        SetCreatorWorkbenchModeById(preferredModeId);
    }

    private string ResolveRecommendedCreatorWorkbenchModeId()
    {
        return CreatorWorkbenchModeSplitId;
    }

    private bool CanActivateCreatorWorkbenchMode(string? modeId)
    {
        if (!HasSelectedCreatorWorkbenchFile)
        {
            return false;
        }

        return modeId switch
        {
            CreatorWorkbenchModeStructuredId => CanUseCreatorWorkbenchStructuredEditing,
            CreatorWorkbenchModeSplitId => true,
            CreatorWorkbenchModeRawId or CreatorWorkbenchModeDiffId => true,
            _ => true
        };
    }

    private void SetCreatorWorkbenchModeById(string modeId)
    {
        if (CreatorWorkbenchModeOptions.Count == 0)
        {
            return;
        }

        var target = CreatorWorkbenchModeOptions.FirstOrDefault(option => string.Equals(option.Id, modeId, StringComparison.OrdinalIgnoreCase))
            ?? CreatorWorkbenchModeOptions.First();

        if (ReferenceEquals(SelectedCreatorWorkbenchMode, target))
        {
            return;
        }

        _isAdjustingCreatorWorkbenchMode = true;
        SelectedCreatorWorkbenchMode = target;
        _isAdjustingCreatorWorkbenchMode = false;
        NotifyCreatorWorkbenchEditorVisualStateChanged();
    }

    private void OnCreatorWorkbenchStructuredFieldChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(CreatorWorkbenchStructuredField.EditableValue))
        {
            return;
        }

        ApplyCreatorWorkbenchStructuredChanges();
    }

    private void ApplyCreatorWorkbenchStructuredChanges()
    {
        if (_creatorWorkbenchDocumentAnalysis == null || !CanUseCreatorWorkbenchStructuredEditing || SelectedCreatorWorkbenchFile == null)
        {
            return;
        }

        var renderResult = _creatorWorkbenchEditorService.RenderStructuredDocument(
            _creatorWorkbenchDocumentAnalysis,
            CreatorWorkbenchStructuredFields,
            CreatorWorkbenchContent ?? string.Empty);

        _creatorWorkbenchStructuredRenderIssues = renderResult.Issues.ToList();
        UpdateCreatorWorkbenchValidationIssues();

        _suppressCreatorWorkbenchRawAnalysis = true;
        CreatorWorkbenchContent = renderResult.Content;
        _suppressCreatorWorkbenchRawAnalysis = false;

        CreatorWorkbenchStatus = renderResult.Issues.Any(issue => issue.IsError)
            ? "Úprava má problém. Zkontroluj panel kontrol vpravo."
            : $"Připraven nový obsah pro {SelectedCreatorWorkbenchFile.RelativePath}.";

        _ = RefreshCreatorWorkbenchDiffAsync();
        NotifyCreatorWorkbenchEditorVisualStateChanged();
    }

    private async Task<string?> GetCreatorWorkbenchComparisonContentAsync(CreatorWorkbenchComparisonTarget? target)
    {
        if (target == null)
        {
            return null;
        }

        if (target.Kind == CreatorWorkbenchComparisonTargetKind.Loaded)
        {
            return _creatorWorkbenchLoadedContent;
        }

        if (_creatorWorkbenchComparisonCache.TryGetValue(target.Id, out var cachedContent))
        {
            return cachedContent;
        }

        var loadedContent = await _creatorWorkbenchEditorService.LoadComparisonContentAsync(target, _creatorWorkbenchLoadedContent);
        if (loadedContent != null)
        {
            _creatorWorkbenchComparisonCache[target.Id] = loadedContent;
        }

        return loadedContent;
    }

    private async Task RefreshCreatorWorkbenchDiffAsync(bool invalidateSelectedTargetCache = false)
    {
        if (SelectedCreatorWorkbenchFile == null || SelectedCreatorWorkbenchComparisonTarget == null)
        {
            CreatorWorkbenchDiffLines.Clear();
            CreatorWorkbenchDiffStatus = "Diff ceka na otevreny soubor.";
            NotifyCreatorWorkbenchEditorVisualStateChanged();
            return;
        }

        var selectedTarget = SelectedCreatorWorkbenchComparisonTarget;
        if (invalidateSelectedTargetCache)
        {
            _creatorWorkbenchComparisonCache.Remove(selectedTarget.Id);
        }

        var refreshVersion = ++_creatorWorkbenchDiffRefreshVersion;
        var baselineContent = await GetCreatorWorkbenchComparisonContentAsync(selectedTarget);
        if (refreshVersion != _creatorWorkbenchDiffRefreshVersion)
        {
            return;
        }

        if (baselineContent == null)
        {
            CreatorWorkbenchDiffLines.Clear();
            CreatorWorkbenchDiffStatus = $"Zdroj {selectedTarget.Label} uz neni dostupny.";
            NotifyCreatorWorkbenchEditorVisualStateChanged();
            return;
        }

        var diffLines = _creatorWorkbenchEditorService.BuildDiff(baselineContent, CreatorWorkbenchContent ?? string.Empty);
        CreatorWorkbenchDiffLines.Clear();
        foreach (var line in diffLines)
        {
            CreatorWorkbenchDiffLines.Add(line);
        }

        var changedLines = CreatorWorkbenchDiffLines.Count(line => line.Kind != CreatorWorkbenchDiffKind.Unchanged);
        CreatorWorkbenchDiffStatus = changedLines == 0
            ? $"Bez rozdilu proti {selectedTarget.Label.ToLowerInvariant()}."
            : $"Srovnano proti {selectedTarget.Label.ToLowerInvariant()}.";

        NotifyCreatorWorkbenchEditorVisualStateChanged();
    }

    [RelayCommand]
    private async Task RefreshCreatorWorkbenchDiff()
    {
        await RefreshCreatorWorkbenchDiffAsync(invalidateSelectedTargetCache: true);
    }

    [RelayCommand]
    private void ResetCreatorWorkbenchInspector()
    {
        SelectedCreatorWorkbenchOutlineItem = null;
        CreatorWorkbenchInspectorQuery = string.Empty;
        CreatorWorkbenchStatus = HasSelectedCreatorWorkbenchFile
            ? "Focus i filtr byly vycisteny."
            : "Bez otevreneho souboru neni co resetovat.";
    }

    [RelayCommand]
    private void OpenCreatorWorkbenchFileLocation()
    {
        if (SelectedCreatorWorkbenchFile == null)
        {
            ShowToast("Creator Studio", "Nejdřív otevri soubor, pak ma smysl otevirat jeho slozku.", ToastSeverity.Warning);
            return;
        }

        var directoryPath = System.IO.Path.GetDirectoryName(SelectedCreatorWorkbenchFile.FullPath);
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return;
        }

        OpenFolder(directoryPath);
        TrackCreatorActivity($"Otevrena slozka pro {SelectedCreatorWorkbenchFile.RelativePath}.");
    }

    partial void OnSelectedCreatorWorkbenchModeChanged(SelectionOption? value)
    {
        if (_isAdjustingCreatorWorkbenchMode)
        {
            return;
        }

        var requestedModeId = value?.Id ?? ResolveRecommendedCreatorWorkbenchModeId();
        if (!CanActivateCreatorWorkbenchMode(requestedModeId))
        {
            CreatorWorkbenchStatus = CanUseCreatorWorkbenchStructuredEditing
                ? "Tenhle rezim ted nejde aktivovat."
                : "Structured rezim je dostupny jen pro parsovatelne configy.";
            SetCreatorWorkbenchModeById(ResolveRecommendedCreatorWorkbenchModeId());
            return;
        }

        if (string.Equals(requestedModeId, CreatorWorkbenchModeDiffId, StringComparison.OrdinalIgnoreCase))
        {
            _ = RefreshCreatorWorkbenchDiffAsync();
        }

        NotifyCreatorWorkbenchEditorVisualStateChanged();
    }

    partial void OnSelectedCreatorWorkbenchOutlineItemChanged(CreatorWorkbenchOutlineItem? value)
    {
        ApplyCreatorWorkbenchInspectorFilter();
        if (value != null)
        {
            CreatorWorkbenchStatus = $"Focus na {value.DisplayPath}.";
        }

        NotifyCreatorWorkbenchEditorVisualStateChanged();
    }

    partial void OnSelectedCreatorWorkbenchComparisonTargetChanged(CreatorWorkbenchComparisonTarget? value)
    {
        _ = RefreshCreatorWorkbenchDiffAsync();
        NotifyCreatorWorkbenchEditorVisualStateChanged();
    }

    partial void OnCreatorWorkbenchInspectorQueryChanged(string value)
    {
        ApplyCreatorWorkbenchInspectorFilter();
    }
}