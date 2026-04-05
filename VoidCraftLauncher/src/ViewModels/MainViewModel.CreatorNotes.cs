using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VoidCraftLauncher.Models;
using VoidCraftLauncher.Models.CreatorStudio;
using VoidCraftLauncher.Services;

namespace VoidCraftLauncher.ViewModels;

public partial class MainViewModel
{
    private static readonly HashSet<string> CreatorCanvasNodeTransientProperties = new(StringComparer.Ordinal)
    {
        nameof(CreatorCanvasNode.IsInlineEditing)
    };

    private static readonly string[] CreatorCanvasNodeTypeValues =
    {
        "idea",
        "questline",
        "gate",
        "boss",
        "dimension",
        "recipe",
        "blocker"
    };

    private static readonly string[] CreatorCanvasContentKindValues =
    {
        "text",
        "image",
        "file",
        "link"
    };

    private CreatorCanvasGraph? _observedCreatorCanvasGraph;
    private bool _isHydratingCreatorCanvasState;

    // ──── Notes State ────
    [ObservableProperty]
    private int _creatorNotesModeIndex;

    [ObservableProperty]
    private CreatorNoteDocument? _selectedCreatorNoteDocument;

    [ObservableProperty]
    private string _creatorNoteEditorContent = string.Empty;

    [ObservableProperty]
    private bool _isCreatorNoteSaving;

    [ObservableProperty]
    private CreatorCanvasGraph? _selectedCreatorCanvasGraph;

    [ObservableProperty]
    private CreatorCanvasNode? _selectedCreatorCanvasNode;

    [ObservableProperty]
    private bool _isCreatorCanvasSaving;

    [ObservableProperty]
    private bool _isCreatorCanvasDirty;

    [ObservableProperty]
    private bool _isCreatorCanvasExpanded;

    public ObservableCollection<CreatorNoteDocument> CreatorNoteDocuments { get; } = new();

    public ObservableCollection<CreatorNoteDocument> CreatorWikiDocuments { get; } = new();

    public ObservableCollection<CreatorCanvasGraph> CreatorCanvasGraphs { get; } = new();

    public ObservableCollection<CreatorCanvasNode> CreatorCanvasConnectedNodes { get; } = new();

    public ObservableCollection<CreatorCanvasNode> CreatorCanvasAvailableNodes { get; } = new();

    public IReadOnlyList<string> CreatorCanvasNodeTypes => CreatorCanvasNodeTypeValues;

    public IReadOnlyList<string> CreatorCanvasContentKinds => CreatorCanvasContentKindValues;

    public bool HasCreatorNoteDocuments => CreatorNoteDocuments.Count > 0;

    public bool HasCreatorWikiDocuments => CreatorWikiDocuments.Count > 0;

    public bool HasSelectedCreatorNoteDocument => SelectedCreatorNoteDocument != null;

    public bool HasCreatorCanvasGraphs => CreatorCanvasGraphs.Count > 0;

    public bool HasSelectedCreatorCanvasGraph => SelectedCreatorCanvasGraph != null;

    public bool HasSelectedCreatorCanvasNode => SelectedCreatorCanvasNode != null;

    public bool HasCreatorCanvasConnectedNodes => CreatorCanvasConnectedNodes.Count > 0;

    public bool HasCreatorCanvasAvailableNodes => CreatorCanvasAvailableNodes.Count > 0;

    public bool HasSelectedCreatorNoteLinks => SelectedCreatorNoteDocument?.LinkCount > 0;

    public bool ShowCreatorWikiLinksEmptyState => HasSelectedCreatorNoteDocument && !HasSelectedCreatorNoteLinks;

    public bool HasSelectedCreatorCanvasGraphNodes => SelectedCreatorCanvasGraph?.HasNodes == true;

    public string CreatorNotesModeLabel => CreatorNotesModeIndex switch
    {
        0 => "Docs",
        1 => "Wiki",
        2 => "Canvas",
        _ => "Docs"
    };

    public bool IsCreatorNotesModeDocs => CreatorNotesModeIndex == 0;
    public bool IsCreatorNotesModeWiki => CreatorNotesModeIndex == 1;
    public bool IsCreatorNotesModeCanvas => CreatorNotesModeIndex == 2;

    public bool IsCreatorNotesModeMindMap => false;

    public string CreatorNotesDocumentsSummary =>
        $"{CreatorNoteDocuments.Count} docs • {CreatorWikiDocuments.Count} wiki • {CreatorCanvasGraphs.Count} grafu";

    public string CreatorNotesPrimaryActionLabel => CreatorNotesModeIndex switch
    {
        1 => "Nova wiki stranka",
        2 => "Novy canvas",
        _ => "Novy dokument"
    };

    public string CreatorCanvasInteractionHint =>
        "Pravy klik prida kartu presne do mista kliknuti. Prostredni tlacitko taha canvas. Tazenim presouvas kartu, dvojklik ji edituje inline a Ctrl + levy klik propoji vybrany node s cilem.";

    public string CreatorCanvasExpandLabel => IsCreatorCanvasExpanded
        ? "Normalni layout"
        : "Rozsirit canvas";

    public GridLength CreatorCanvasSidebarWidth => IsCreatorCanvasExpanded
        ? new GridLength(0)
        : new GridLength(300);

    public GridLength CreatorCanvasSidebarGapWidth => IsCreatorCanvasExpanded
        ? new GridLength(0)
        : new GridLength(16);

    public GridLength CreatorCanvasInspectorWidth => IsCreatorCanvasExpanded
        ? new GridLength(0)
        : new GridLength(320);

    public string CreatorCanvasEmptyTitle => HasSelectedCreatorCanvasGraph
        ? "Prazdny canvas"
        : "Vyber nebo vytvor canvas";

    public string CreatorCanvasEmptySubtitle => HasSelectedCreatorCanvasGraph
        ? "Pravy klik do gridu prida text, obrazek, file link nebo web link."
        : "Vytvor novy canvas nebo vyber existujici graph vlevo.";

    public string CreatorSelectedNoteLinkSummary => SelectedCreatorNoteDocument?.LinkSummary ?? "Bez wiki odkazu";

    public string CreatorSelectedCanvasGraphStatus => SelectedCreatorCanvasGraph == null
        ? "Vyber graf vlevo."
        : $"{SelectedCreatorCanvasGraph.Summary} • {SelectedCreatorCanvasGraph.RelativePath}";

    public string CreatorSelectedCanvasNodeStatus => SelectedCreatorCanvasNode == null
        ? "Vyber uzel pro detail."
        : $"{SelectedCreatorCanvasNode.TypeIcon} • {SelectedCreatorCanvasNode.ConnectionSummary}";

    public bool CanSaveCreatorNote =>
        HasSelectedCreatorNoteDocument &&
        !IsCreatorNoteSaving &&
        !string.Equals(SelectedCreatorNoteDocument?.Content ?? string.Empty, CreatorNoteEditorContent, StringComparison.Ordinal);

    public bool CanSaveCreatorCanvasGraph =>
        HasSelectedCreatorCanvasGraph &&
        !IsCreatorCanvasSaving &&
        IsCreatorCanvasDirty;

    public bool CanSaveActiveCreatorNotesSurface =>
        IsCreatorNotesModeCanvas
            ? CanSaveCreatorCanvasGraph
            : CanSaveCreatorNote;

    // ──── Notes Commands ────

    [RelayCommand]
    private async Task RefreshCreatorNotes()
    {
        var workspacePath = SkinStudioSelectedInstancePath;
        if (string.IsNullOrWhiteSpace(workspacePath))
            return;

        var selectedNotePath = SelectedCreatorNoteDocument?.FullPath;
        var selectedGraphPath = SelectedCreatorCanvasGraph?.FullPath;
        var selectedNodeId = SelectedCreatorCanvasNode?.Id;

        var docs = _creatorNotesService.DiscoverDocuments(workspacePath);
        var wikiDocs = _creatorNotesService.DiscoverWikiDocuments(workspacePath);
        var graphs = _creatorNotesService.DiscoverCanvasGraphs(workspacePath);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _isHydratingCreatorCanvasState = true;

            ReplaceCollectionItems(CreatorNoteDocuments, docs);
            ReplaceCollectionItems(CreatorWikiDocuments, wikiDocs);
            ReplaceCollectionItems(CreatorCanvasGraphs, graphs);

            SelectedCreatorNoteDocument = ResolveSelectedNoteDocument(selectedNotePath);
            if (SelectedCreatorNoteDocument == null)
            {
                EnsureNotesSelectionForCurrentMode();
            }

            SelectedCreatorCanvasGraph = ResolveSelectedCanvasGraph(selectedGraphPath) ?? CreatorCanvasGraphs.FirstOrDefault();
            SelectedCreatorCanvasNode = ResolveSelectedCanvasNode(SelectedCreatorCanvasGraph, selectedNodeId)
                ?? SelectedCreatorCanvasGraph?.Nodes.FirstOrDefault();

            IsCreatorCanvasDirty = false;
            _isHydratingCreatorCanvasState = false;

            NotifyCreatorNotesStateChanged();
        });
    }

    [RelayCommand]
    private async Task CreateCreatorNoteDocument(string? title)
    {
        var workspacePath = SkinStudioSelectedInstancePath;
        if (string.IsNullOrWhiteSpace(workspacePath))
            return;

        if (IsCreatorNotesModeCanvas)
        {
            var graphName = string.IsNullOrWhiteSpace(title)
                ? "Novy canvas"
                : title.Trim();

            var createdGraph = await _creatorNotesService.CreateCanvasGraphAsync(workspacePath, graphName);
            await RefreshCreatorNotes();
            SelectedCreatorCanvasGraph = ResolveSelectedCanvasGraph(createdGraph.FullPath) ?? SelectedCreatorCanvasGraph;
            SelectedCreatorCanvasNode = SelectedCreatorCanvasGraph?.Nodes.FirstOrDefault();
            ShowToast("Notes", $"Graf \"{graphName}\" vytvoren.", ToastSeverity.Success, 2200);
            TrackCreatorActivity($"Novy graph: {graphName}");
            return;
        }

        var storageKind = IsCreatorNotesModeWiki ? CreatorNoteStorageKind.Wiki : CreatorNoteStorageKind.Docs;
        var defaultTitle = storageKind == CreatorNoteStorageKind.Wiki ? "Nova wiki stranka" : "Novy dokument";
        var docTitle = string.IsNullOrWhiteSpace(title) ? defaultTitle : title.Trim();
        var createdDoc = await _creatorNotesService.CreateDocumentAsync(workspacePath, docTitle, storageKind);

        await RefreshCreatorNotes();
        SelectedCreatorNoteDocument = ResolveSelectedNoteDocument(createdDoc.FullPath) ?? SelectedCreatorNoteDocument;
        ShowToast("Notes", $"Dokument \"{docTitle}\" vytvoren.", ToastSeverity.Success, 2200);
        TrackCreatorActivity($"Novy dokument: {docTitle}");
    }

    [RelayCommand]
    private async Task SaveActiveCreatorNotesSurface()
    {
        if (IsCreatorNotesModeCanvas)
        {
            await SaveCreatorCanvasGraph();
            return;
        }

        await SaveCreatorNote();
    }

    [RelayCommand]
    private async Task SaveCreatorNote()
    {
        if (SelectedCreatorNoteDocument == null)
            return;

        IsCreatorNoteSaving = true;
        try
        {
            await _creatorNotesService.SaveDocumentAsync(SelectedCreatorNoteDocument.FullPath, CreatorNoteEditorContent);
            SelectedCreatorNoteDocument.Content = CreatorNoteEditorContent;
            SelectedCreatorNoteDocument.LinkedTitles = _creatorNotesService.ExtractWikiLinks(CreatorNoteEditorContent).ToList();
            SelectedCreatorNoteDocument.LastModifiedUtc = DateTimeOffset.UtcNow;

            ShowToast("Notes", "Dokument ulozen.", ToastSeverity.Success, 2000);
            TrackCreatorActivity($"Ulozen dokument: {SelectedCreatorNoteDocument.Title}");
            NotifyCreatorNotesStateChanged();
        }
        catch (Exception ex)
        {
            LogService.Error("Note save failed", ex);
            ShowToast("Notes", "Ulozeni selhalo.", ToastSeverity.Error);
        }
        finally
        {
            IsCreatorNoteSaving = false;
        }
    }

    [RelayCommand]
    private async Task DeleteCreatorNoteDocument(CreatorNoteDocument? doc)
    {
        if (doc == null)
            return;

        if (!_creatorNotesService.DeleteDocument(doc.FullPath))
        {
            ShowToast("Notes", "Smazani dokumentu selhalo.", ToastSeverity.Error, 2200);
            return;
        }

        var deletedTitle = doc.Title;
        if (string.Equals(SelectedCreatorNoteDocument?.FullPath, doc.FullPath, StringComparison.OrdinalIgnoreCase))
        {
            SelectedCreatorNoteDocument = null;
            CreatorNoteEditorContent = string.Empty;
        }

        await RefreshCreatorNotes();
        ShowToast("Notes", $"Dokument \"{deletedTitle}\" smazan.", ToastSeverity.Success, 2000);
        TrackCreatorActivity($"Smazan dokument: {deletedTitle}");
    }

    [RelayCommand]
    private void OpenCreatorWikiLink(string? linkTitle)
    {
        if (string.IsNullOrWhiteSpace(linkTitle))
            return;

        var target = CreatorWikiDocuments.Concat(CreatorNoteDocuments)
            .FirstOrDefault(doc => string.Equals(doc.Title, linkTitle, StringComparison.OrdinalIgnoreCase));

        if (target == null)
        {
            ShowToast("Wiki", $"Stranka \"{linkTitle}\" v notes workspace neexistuje.", ToastSeverity.Warning, 2200);
            return;
        }

        SetCreatorNotesMode(target.IsWikiPage ? "wiki" : "docs");
        SelectedCreatorNoteDocument = target;
    }

    [RelayCommand]
    private void SetCreatorNotesMode(string? mode)
    {
        CreatorNotesModeIndex = mode?.Trim().ToLowerInvariant() switch
        {
            "wiki" => 1,
            "canvas" or "mindmap" => 2,
            _ => 0
        };
    }

    [RelayCommand]
    private void ToggleCreatorCanvasExpanded()
    {
        IsCreatorCanvasExpanded = !IsCreatorCanvasExpanded;
    }

    [RelayCommand]
    private void SelectCreatorCanvasNode(CreatorCanvasNode? node)
    {
        if (node == null)
            return;

        SelectedCreatorCanvasNode = node;
    }

    [RelayCommand]
    private void CreateCreatorCanvasNode()
    {
        var graph = SelectedCreatorCanvasGraph;
        if (graph == null)
            return;

        CreateCreatorCanvasNodeCore(new CreatorCanvasSpawnRequest
        {
            NodeKind = "text",
            X = 72 + (graph.Nodes.Count * 18),
            Y = 72 + (graph.Nodes.Count * 18)
        });
    }

    [RelayCommand]
    private void CreateCreatorCanvasNodeAt(CreatorCanvasSpawnRequest? request)
    {
        CreateCreatorCanvasNodeCore(request);
    }

    [RelayCommand]
    private void DeleteCreatorCanvasNode(CreatorCanvasNode? node)
    {
        if (SelectedCreatorCanvasGraph == null || node == null)
            return;

        if (!SelectedCreatorCanvasGraph.Nodes.Remove(node))
            return;

        foreach (var graphNode in SelectedCreatorCanvasGraph.Nodes)
        {
            if (graphNode.ConnectedNodeIds.RemoveAll(id => string.Equals(id, node.Id, StringComparison.OrdinalIgnoreCase)) > 0)
            {
                graphNode.NotifyConnectionsChanged();
            }
        }

        SelectedCreatorCanvasGraph.NotifyNodesChanged();
        SelectedCreatorCanvasNode = SelectedCreatorCanvasGraph.Nodes.FirstOrDefault();
        IsCreatorCanvasDirty = true;
        RebuildCreatorCanvasConnectionCollections();
        TrackCreatorActivity($"Smazan canvas node: {node.Label}");
    }

    [RelayCommand]
    private async Task SaveCreatorCanvasGraph()
    {
        if (SelectedCreatorCanvasGraph == null)
            return;

        var workspacePath = SkinStudioSelectedInstancePath;
        if (string.IsNullOrWhiteSpace(workspacePath))
            return;

        IsCreatorCanvasSaving = true;
        try
        {
            await _creatorNotesService.SaveCanvasGraphAsync(workspacePath, SelectedCreatorCanvasGraph);
            IsCreatorCanvasDirty = false;
            ShowToast("Notes", "Canvas graf ulozen.", ToastSeverity.Success, 2200);
            TrackCreatorActivity($"Ulozen graph: {SelectedCreatorCanvasGraph.Name}");
            await RefreshCreatorNotes();
        }
        catch (Exception ex)
        {
            LogService.Error("Canvas graph save failed", ex);
            ShowToast("Notes", "Ulozeni graphu selhalo.", ToastSeverity.Error, 2600);
        }
        finally
        {
            IsCreatorCanvasSaving = false;
        }
    }

    [RelayCommand]
    private async Task DeleteCreatorCanvasGraph(CreatorCanvasGraph? graph)
    {
        if (graph == null)
            return;

        if (!_creatorNotesService.DeleteCanvasGraph(graph.FullPath))
        {
            ShowToast("Notes", "Smazani graphu selhalo.", ToastSeverity.Error, 2200);
            return;
        }

        var graphName = graph.Name;
        if (string.Equals(SelectedCreatorCanvasGraph?.FullPath, graph.FullPath, StringComparison.OrdinalIgnoreCase))
        {
            SelectedCreatorCanvasGraph = null;
            SelectedCreatorCanvasNode = null;
        }

        await RefreshCreatorNotes();
        ShowToast("Notes", $"Graf \"{graphName}\" smazan.", ToastSeverity.Success, 2200);
        TrackCreatorActivity($"Smazan graph: {graphName}");
    }

    [RelayCommand]
    private void LinkCreatorCanvasNode(CreatorCanvasNode? target)
    {
        if (SelectedCreatorCanvasNode == null || target == null)
            return;

        if (string.Equals(SelectedCreatorCanvasNode.Id, target.Id, StringComparison.OrdinalIgnoreCase))
            return;

        if (SelectedCreatorCanvasNode.ConnectedNodeIds.Any(id => string.Equals(id, target.Id, StringComparison.OrdinalIgnoreCase)))
            return;

        SelectedCreatorCanvasNode.ConnectedNodeIds.Add(target.Id);
        SelectedCreatorCanvasNode.NotifyConnectionsChanged();
        IsCreatorCanvasDirty = true;
        RebuildCreatorCanvasConnectionCollections();
        TrackCreatorActivity($"Propojeny uzly: {SelectedCreatorCanvasNode.Label} -> {target.Label}");
    }

    [RelayCommand]
    private void UnlinkCreatorCanvasNode(CreatorCanvasNode? target)
    {
        if (SelectedCreatorCanvasNode == null || target == null)
            return;

        if (SelectedCreatorCanvasNode.ConnectedNodeIds.RemoveAll(id => string.Equals(id, target.Id, StringComparison.OrdinalIgnoreCase)) == 0)
            return;

        SelectedCreatorCanvasNode.NotifyConnectionsChanged();
        IsCreatorCanvasDirty = true;
        RebuildCreatorCanvasConnectionCollections();
        TrackCreatorActivity($"Odpojeny uzly: {SelectedCreatorCanvasNode.Label} -> {target.Label}");
    }

    [RelayCommand]
    private void UseCurrentCreatorFileForCanvasNode()
    {
        if (SelectedCreatorCanvasNode == null)
            return;

        if (!HasSelectedCreatorWorkbenchFile)
        {
            ShowToast("Canvas", "Nejdřív vyber soubor ve Files tabu.", ToastSeverity.Warning, 2200);
            return;
        }

        SelectedCreatorCanvasNode.LinkedFilePath = SelectedCreatorWorkbenchFile!.RelativePath;
        IsCreatorCanvasDirty = true;
        OnPropertyChanged(nameof(CreatorSelectedCanvasNodeStatus));
        TrackCreatorActivity($"Node {SelectedCreatorCanvasNode.Label} navazan na {SelectedCreatorWorkbenchFile.RelativePath}");
    }

    [RelayCommand]
    private void OpenCreatorCanvasNodeLinkedFile()
    {
        if (SelectedCreatorCanvasNode == null || string.IsNullOrWhiteSpace(SelectedCreatorCanvasNode.LinkedFilePath))
            return;

        var targetFile = FilteredCreatorWorkbenchFiles.FirstOrDefault(file =>
            string.Equals(file.RelativePath, SelectedCreatorCanvasNode.LinkedFilePath, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(file.FullPath, SelectedCreatorCanvasNode.LinkedFilePath, StringComparison.OrdinalIgnoreCase));

        if (targetFile == null)
        {
            ShowToast("Canvas", "Navazany soubor ve Files tabu nebyl nalezen.", ToastSeverity.Warning, 2200);
            return;
        }

        SetCreatorSelectedTab(CreatorShellTab.Files);
        SelectedCreatorWorkbenchFile = targetFile;
    }

    partial void OnSelectedCreatorNoteDocumentChanged(CreatorNoteDocument? value)
    {
        OnPropertyChanged(nameof(HasSelectedCreatorNoteDocument));
        OnPropertyChanged(nameof(HasSelectedCreatorNoteLinks));
        OnPropertyChanged(nameof(ShowCreatorWikiLinksEmptyState));
        OnPropertyChanged(nameof(CreatorSelectedNoteLinkSummary));
        OnPropertyChanged(nameof(CanSaveCreatorNote));
        OnPropertyChanged(nameof(CanSaveActiveCreatorNotesSurface));

        if (value != null)
        {
            _ = LoadCreatorNoteContentAsync(value);
        }
        else
        {
            CreatorNoteEditorContent = string.Empty;
        }
    }

    partial void OnSelectedCreatorCanvasGraphChanged(CreatorCanvasGraph? value)
    {
        AttachCreatorCanvasGraphObservers(value);

        if (!_isHydratingCreatorCanvasState)
        {
            SelectedCreatorCanvasNode = value?.Nodes.FirstOrDefault();
        }

        RebuildCreatorCanvasConnectionCollections();
        OnPropertyChanged(nameof(HasSelectedCreatorCanvasGraph));
        OnPropertyChanged(nameof(HasSelectedCreatorCanvasGraphNodes));
        OnPropertyChanged(nameof(CreatorSelectedCanvasGraphStatus));
        OnPropertyChanged(nameof(CreatorCanvasEmptyTitle));
        OnPropertyChanged(nameof(CreatorCanvasEmptySubtitle));
        OnPropertyChanged(nameof(CanSaveCreatorCanvasGraph));
        OnPropertyChanged(nameof(CanSaveActiveCreatorNotesSurface));
    }

    partial void OnSelectedCreatorCanvasNodeChanged(CreatorCanvasNode? value)
    {
        RebuildCreatorCanvasConnectionCollections();
        OnPropertyChanged(nameof(HasSelectedCreatorCanvasNode));
        OnPropertyChanged(nameof(CreatorSelectedCanvasNodeStatus));
    }

    partial void OnCreatorNotesModeIndexChanged(int value)
    {
        EnsureNotesSelectionForCurrentMode();
        OnPropertyChanged(nameof(CreatorNotesModeLabel));
        OnPropertyChanged(nameof(IsCreatorNotesModeDocs));
        OnPropertyChanged(nameof(IsCreatorNotesModeWiki));
        OnPropertyChanged(nameof(IsCreatorNotesModeCanvas));
        OnPropertyChanged(nameof(CreatorNotesPrimaryActionLabel));
        OnPropertyChanged(nameof(CanSaveActiveCreatorNotesSurface));
        OnPropertyChanged(nameof(CreatorCanvasEmptyTitle));
        OnPropertyChanged(nameof(CreatorCanvasEmptySubtitle));
    }

    partial void OnIsCreatorCanvasExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(CreatorCanvasExpandLabel));
        OnPropertyChanged(nameof(CreatorCanvasSidebarWidth));
        OnPropertyChanged(nameof(CreatorCanvasSidebarGapWidth));
        OnPropertyChanged(nameof(CreatorCanvasInspectorWidth));
    }

    partial void OnCreatorNoteEditorContentChanged(string value)
    {
        OnPropertyChanged(nameof(CanSaveCreatorNote));
        OnPropertyChanged(nameof(CanSaveActiveCreatorNotesSurface));
    }

    partial void OnIsCreatorNoteSavingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanSaveCreatorNote));
        OnPropertyChanged(nameof(CanSaveActiveCreatorNotesSurface));
    }

    partial void OnIsCreatorCanvasSavingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanSaveCreatorCanvasGraph));
        OnPropertyChanged(nameof(CanSaveActiveCreatorNotesSurface));
    }

    partial void OnIsCreatorCanvasDirtyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanSaveCreatorCanvasGraph));
        OnPropertyChanged(nameof(CanSaveActiveCreatorNotesSurface));
        OnPropertyChanged(nameof(CreatorSelectedCanvasGraphStatus));
    }

    private async Task LoadCreatorNoteContentAsync(CreatorNoteDocument doc)
    {
        var content = await _creatorNotesService.LoadDocumentContentAsync(doc.FullPath);
        doc.Content = content;
        doc.LinkedTitles = _creatorNotesService.ExtractWikiLinks(content).ToList();
        CreatorNoteEditorContent = content;
        OnPropertyChanged(nameof(HasSelectedCreatorNoteLinks));
        OnPropertyChanged(nameof(CreatorSelectedNoteLinkSummary));
    }

    private void NotifyCreatorNotesStateChanged()
    {
        OnPropertyChanged(nameof(HasCreatorNoteDocuments));
        OnPropertyChanged(nameof(HasCreatorWikiDocuments));
        OnPropertyChanged(nameof(HasSelectedCreatorNoteDocument));
        OnPropertyChanged(nameof(HasCreatorCanvasGraphs));
        OnPropertyChanged(nameof(HasSelectedCreatorCanvasGraph));
        OnPropertyChanged(nameof(HasSelectedCreatorCanvasNode));
        OnPropertyChanged(nameof(HasCreatorCanvasConnectedNodes));
        OnPropertyChanged(nameof(HasCreatorCanvasAvailableNodes));
        OnPropertyChanged(nameof(HasSelectedCreatorNoteLinks));
        OnPropertyChanged(nameof(ShowCreatorWikiLinksEmptyState));
        OnPropertyChanged(nameof(CreatorNotesModeLabel));
        OnPropertyChanged(nameof(CreatorNotesDocumentsSummary));
        OnPropertyChanged(nameof(CreatorNotesPrimaryActionLabel));
        OnPropertyChanged(nameof(CreatorSelectedNoteLinkSummary));
        OnPropertyChanged(nameof(HasSelectedCreatorCanvasGraphNodes));
        OnPropertyChanged(nameof(CreatorSelectedCanvasGraphStatus));
        OnPropertyChanged(nameof(CreatorSelectedCanvasNodeStatus));
        OnPropertyChanged(nameof(CreatorCanvasEmptyTitle));
        OnPropertyChanged(nameof(CreatorCanvasEmptySubtitle));
        OnPropertyChanged(nameof(CanSaveCreatorNote));
        OnPropertyChanged(nameof(CanSaveCreatorCanvasGraph));
        OnPropertyChanged(nameof(CanSaveActiveCreatorNotesSurface));
    }

    private void EnsureNotesSelectionForCurrentMode()
    {
        if (IsCreatorNotesModeWiki)
        {
            if (SelectedCreatorNoteDocument == null || !SelectedCreatorNoteDocument.IsWikiPage)
            {
                SelectedCreatorNoteDocument = CreatorWikiDocuments.FirstOrDefault();
            }
            return;
        }

        if (IsCreatorNotesModeDocs)
        {
            if (SelectedCreatorNoteDocument == null || SelectedCreatorNoteDocument.IsWikiPage)
            {
                SelectedCreatorNoteDocument = CreatorNoteDocuments.FirstOrDefault();
            }
            return;
        }

        SelectedCreatorCanvasGraph ??= CreatorCanvasGraphs.FirstOrDefault();
        SelectedCreatorCanvasNode ??= SelectedCreatorCanvasGraph?.Nodes.FirstOrDefault();
    }

    private CreatorNoteDocument? ResolveSelectedNoteDocument(string? fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
            return null;

        return CreatorNoteDocuments.Concat(CreatorWikiDocuments)
            .FirstOrDefault(doc => string.Equals(doc.FullPath, fullPath, StringComparison.OrdinalIgnoreCase));
    }

    private CreatorCanvasGraph? ResolveSelectedCanvasGraph(string? fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
            return null;

        return CreatorCanvasGraphs.FirstOrDefault(graph =>
            string.Equals(graph.FullPath, fullPath, StringComparison.OrdinalIgnoreCase));
    }

    private static CreatorCanvasNode? ResolveSelectedCanvasNode(CreatorCanvasGraph? graph, string? nodeId)
    {
        if (graph == null || string.IsNullOrWhiteSpace(nodeId))
            return null;

        return graph.Nodes.FirstOrDefault(node => string.Equals(node.Id, nodeId, StringComparison.OrdinalIgnoreCase));
    }

    private void ReplaceCollectionItems<TItem>(ObservableCollection<TItem> target, IEnumerable<TItem> items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }

    private string BuildNextCreatorCanvasNodeLabel(CreatorCanvasGraph graph)
    {
        var index = graph.Nodes.Count + 1;
        while (graph.Nodes.Any(node => string.Equals(node.Label, $"Node {index}", StringComparison.OrdinalIgnoreCase)))
        {
            index++;
        }

        return $"Node {index}";
    }

    private void CreateCreatorCanvasNodeCore(CreatorCanvasSpawnRequest? request)
    {
        if (SelectedCreatorCanvasGraph == null)
            return;

        var node = BuildCreatorCanvasNode(request);
        SelectedCreatorCanvasGraph.Nodes.Add(node);
        SelectedCreatorCanvasGraph.NotifyNodesChanged();
        SelectedCreatorCanvasNode = node;
        IsCreatorCanvasDirty = true;
        RebuildCreatorCanvasConnectionCollections();
        TrackCreatorActivity($"Pridan canvas node: {node.Label}");
    }

    private CreatorCanvasNode BuildCreatorCanvasNode(CreatorCanvasSpawnRequest? request)
    {
        var kind = request?.NodeKind?.Trim().ToLowerInvariant() switch
        {
            "image" => "image",
            "file" => "file",
            "link" => "link",
            _ => "text"
        };

        var graph = SelectedCreatorCanvasGraph!;
        var label = BuildDefaultCreatorCanvasNodeLabel(graph, kind);
        var node = new CreatorCanvasNode
        {
            Label = label,
            NodeType = "idea",
            ContentKind = kind,
            X = Math.Max(24, request?.X ?? 72),
            Y = Math.Max(24, request?.Y ?? 72),
            Description = kind == "text" ? "Nova textova karta" : string.Empty,
            ContentValue = kind == "link" ? "https://" : string.Empty
        };

        if (kind == "file" && HasSelectedCreatorWorkbenchFile)
        {
            node.LinkedFilePath = SelectedCreatorWorkbenchFile!.RelativePath;
            node.ContentValue = SelectedCreatorWorkbenchFile.RelativePath;
            node.Label = Path.GetFileNameWithoutExtension(SelectedCreatorWorkbenchFile.RelativePath);
            node.Description = "Soubor navazany z Files tabu";
        }

        node.NormalizeCanvasCardSize();
        return node;
    }

    private string BuildDefaultCreatorCanvasNodeLabel(CreatorCanvasGraph graph, string kind)
    {
        var baseLabel = kind switch
        {
            "image" => "Image",
            "file" => "File",
            "link" => "Link",
            _ => "Text"
        };

        var index = 1;
        while (graph.Nodes.Any(node => string.Equals(node.Label, $"{baseLabel} {index}", StringComparison.OrdinalIgnoreCase)))
        {
            index++;
        }

        return $"{baseLabel} {index}";
    }

    private void AttachCreatorCanvasGraphObservers(CreatorCanvasGraph? graph)
    {
        if (ReferenceEquals(_observedCreatorCanvasGraph, graph))
            return;

        DetachCreatorCanvasGraphObservers();
        _observedCreatorCanvasGraph = graph;

        if (_observedCreatorCanvasGraph == null)
            return;

        _observedCreatorCanvasGraph.PropertyChanged += OnObservedCreatorCanvasGraphPropertyChanged;
        _observedCreatorCanvasGraph.Nodes.CollectionChanged += OnObservedCreatorCanvasNodesCollectionChanged;
        foreach (var node in _observedCreatorCanvasGraph.Nodes)
        {
            node.PropertyChanged += OnObservedCreatorCanvasNodePropertyChanged;
        }
    }

    private void DetachCreatorCanvasGraphObservers()
    {
        if (_observedCreatorCanvasGraph == null)
            return;

        _observedCreatorCanvasGraph.PropertyChanged -= OnObservedCreatorCanvasGraphPropertyChanged;
        _observedCreatorCanvasGraph.Nodes.CollectionChanged -= OnObservedCreatorCanvasNodesCollectionChanged;
        foreach (var node in _observedCreatorCanvasGraph.Nodes)
        {
            node.PropertyChanged -= OnObservedCreatorCanvasNodePropertyChanged;
        }

        _observedCreatorCanvasGraph = null;
    }

    private void OnObservedCreatorCanvasGraphPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isHydratingCreatorCanvasState)
            return;

        if (e.PropertyName == nameof(CreatorCanvasGraph.Name))
        {
            IsCreatorCanvasDirty = true;
            OnPropertyChanged(nameof(CreatorSelectedCanvasGraphStatus));
        }
    }

    private void OnObservedCreatorCanvasNodesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (var item in e.OldItems.OfType<CreatorCanvasNode>())
            {
                item.PropertyChanged -= OnObservedCreatorCanvasNodePropertyChanged;
            }
        }

        if (e.NewItems != null)
        {
            foreach (var item in e.NewItems.OfType<CreatorCanvasNode>())
            {
                item.PropertyChanged += OnObservedCreatorCanvasNodePropertyChanged;
            }
        }

        SelectedCreatorCanvasGraph?.NotifyNodesChanged();
        if (!_isHydratingCreatorCanvasState)
        {
            IsCreatorCanvasDirty = true;
        }

        RebuildCreatorCanvasConnectionCollections();
        OnPropertyChanged(nameof(HasSelectedCreatorCanvasGraphNodes));
        OnPropertyChanged(nameof(CreatorSelectedCanvasGraphStatus));
        OnPropertyChanged(nameof(CreatorCanvasEmptyTitle));
        OnPropertyChanged(nameof(CreatorCanvasEmptySubtitle));
    }

    private void OnObservedCreatorCanvasNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isHydratingCreatorCanvasState)
            return;

        if (!string.IsNullOrWhiteSpace(e.PropertyName) && CreatorCanvasNodeTransientProperties.Contains(e.PropertyName))
            return;

        IsCreatorCanvasDirty = true;
        if (ReferenceEquals(sender, SelectedCreatorCanvasNode))
        {
            OnPropertyChanged(nameof(CreatorSelectedCanvasNodeStatus));
        }

        RebuildCreatorCanvasConnectionCollections();
    }

    private void RebuildCreatorCanvasConnectionCollections()
    {
        CreatorCanvasConnectedNodes.Clear();
        CreatorCanvasAvailableNodes.Clear();

        if (SelectedCreatorCanvasGraph == null || SelectedCreatorCanvasNode == null)
        {
            OnPropertyChanged(nameof(HasCreatorCanvasConnectedNodes));
            OnPropertyChanged(nameof(HasCreatorCanvasAvailableNodes));
            return;
        }

        foreach (var node in SelectedCreatorCanvasGraph.Nodes.Where(node => !string.Equals(node.Id, SelectedCreatorCanvasNode.Id, StringComparison.OrdinalIgnoreCase)))
        {
            if (SelectedCreatorCanvasNode.ConnectedNodeIds.Any(id => string.Equals(id, node.Id, StringComparison.OrdinalIgnoreCase)))
            {
                CreatorCanvasConnectedNodes.Add(node);
            }
            else
            {
                CreatorCanvasAvailableNodes.Add(node);
            }
        }

        OnPropertyChanged(nameof(HasCreatorCanvasConnectedNodes));
        OnPropertyChanged(nameof(HasCreatorCanvasAvailableNodes));
    }
}