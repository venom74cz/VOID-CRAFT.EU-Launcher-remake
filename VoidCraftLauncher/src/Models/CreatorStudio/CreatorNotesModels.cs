using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace VoidCraftLauncher.Models.CreatorStudio;

public enum CreatorNotesMode
{
    Docs,
    Wiki,
    Canvas,
    MindMap
}

public enum CreatorNoteStorageKind
{
    Docs,
    Wiki
}

public partial class CreatorNoteDocument : ObservableObject
{
    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString("N")[..8];

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private string _fullPath = string.Empty;

    [ObservableProperty]
    private string _relativePath = string.Empty;

    [ObservableProperty]
    private string _content = string.Empty;

    [ObservableProperty]
    private DateTimeOffset _lastModifiedUtc = DateTimeOffset.UtcNow;

    [ObservableProperty]
    private CreatorNoteStorageKind _storageKind = CreatorNoteStorageKind.Docs;

    [ObservableProperty]
    private List<string> _linkedTitles = new();

    public string ModifiedLabel => LastModifiedUtc.LocalDateTime.ToString("dd.MM. HH:mm");

    public bool HasContent => !string.IsNullOrWhiteSpace(Content);

    public bool IsWikiPage => StorageKind == CreatorNoteStorageKind.Wiki;

    public int LinkCount => LinkedTitles.Count;

    public string LinkSummary => LinkCount switch
    {
        0 => "Bez wiki odkazu",
        1 => "1 wiki odkaz",
        _ => $"{LinkCount} wiki odkazu"
    };

    public string LocationLabel => string.IsNullOrWhiteSpace(RelativePath) ? FileName : RelativePath;

    partial void OnContentChanged(string value)
    {
        OnPropertyChanged(nameof(HasContent));
    }

    partial void OnLastModifiedUtcChanged(DateTimeOffset value)
    {
        OnPropertyChanged(nameof(ModifiedLabel));
    }

    partial void OnStorageKindChanged(CreatorNoteStorageKind value)
    {
        OnPropertyChanged(nameof(IsWikiPage));
        OnPropertyChanged(nameof(LocationLabel));
    }

    partial void OnRelativePathChanged(string value)
    {
        OnPropertyChanged(nameof(LocationLabel));
    }

    partial void OnLinkedTitlesChanged(List<string> value)
    {
        OnPropertyChanged(nameof(LinkCount));
        OnPropertyChanged(nameof(LinkSummary));
    }
}

public partial class CreatorCanvasNode : ObservableObject
{
    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString("N")[..8];

    [ObservableProperty]
    private string _label = string.Empty;

    [ObservableProperty]
    private string _nodeType = "idea";

    [ObservableProperty]
    private string _contentKind = "text";

    [ObservableProperty]
    private double _x;

    [ObservableProperty]
    private double _y;

    [ObservableProperty]
    private double _width = 240;

    [ObservableProperty]
    private double _height = 152;

    [ObservableProperty]
    [JsonIgnore]
    private bool _isInlineEditing;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _contentValue = string.Empty;

    [ObservableProperty]
    private string? _linkedFilePath;

    [ObservableProperty]
    private List<string> _connectedNodeIds = new();

    public string TypeIcon => NodeType switch
    {
        "questline" => "Questline",
        "gate" => "Gate",
        "boss" => "Boss",
        "dimension" => "Dimension",
        "recipe" => "Recipe Tier",
        "blocker" => "Blocker",
        _ => "Idea"
    };

    public string ContentKindLabel => ContentKind switch
    {
        "image" => "Image",
        "file" => "File",
        "link" => "Link",
        _ => "Text"
    };

    public bool IsTextContent => string.Equals(ContentKind, "text", StringComparison.OrdinalIgnoreCase);

    public bool IsImageContent => string.Equals(ContentKind, "image", StringComparison.OrdinalIgnoreCase);

    public bool IsFileLinkContent => string.Equals(ContentKind, "file", StringComparison.OrdinalIgnoreCase);

    public bool IsExternalLinkContent => string.Equals(ContentKind, "link", StringComparison.OrdinalIgnoreCase);

    public bool HasContentValue => !string.IsNullOrWhiteSpace(ContentValue);

    public bool HasLinkedFile => !string.IsNullOrWhiteSpace(LinkedFilePath);

    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

    public string FileLinkLabel => !string.IsNullOrWhiteSpace(LinkedFilePath)
        ? LinkedFilePath!
        : !string.IsNullOrWhiteSpace(ContentValue)
            ? ContentValue
            : "Zatim bez navazaneho souboru";

    public string ExternalLinkLabel => !string.IsNullOrWhiteSpace(ContentValue)
        ? ContentValue
        : "https://";

    public int ConnectionCount => ConnectedNodeIds.Count;

    public string ConnectionSummary => ConnectionCount switch
    {
        0 => "Bez navazanych uzlu",
        1 => "1 navazany uzel",
        _ => $"{ConnectionCount} navazanych uzlu"
    };

    public void NotifyConnectionsChanged()
    {
        OnPropertyChanged(nameof(ConnectionCount));
        OnPropertyChanged(nameof(ConnectionSummary));
    }

    partial void OnNodeTypeChanged(string value)
    {
        OnPropertyChanged(nameof(TypeIcon));
    }

    partial void OnContentKindChanged(string value)
    {
        NormalizeCanvasCardSize();
        OnPropertyChanged(nameof(ContentKindLabel));
        OnPropertyChanged(nameof(IsTextContent));
        OnPropertyChanged(nameof(IsImageContent));
        OnPropertyChanged(nameof(IsFileLinkContent));
        OnPropertyChanged(nameof(IsExternalLinkContent));
    }

    partial void OnDescriptionChanged(string value)
    {
        OnPropertyChanged(nameof(HasDescription));
    }

    partial void OnContentValueChanged(string value)
    {
        OnPropertyChanged(nameof(HasContentValue));
        OnPropertyChanged(nameof(FileLinkLabel));
        OnPropertyChanged(nameof(ExternalLinkLabel));
    }

    partial void OnLinkedFilePathChanged(string? value)
    {
        OnPropertyChanged(nameof(HasLinkedFile));
        OnPropertyChanged(nameof(FileLinkLabel));
    }

    partial void OnConnectedNodeIdsChanged(List<string> value)
    {
        NotifyConnectionsChanged();
    }

    public void NormalizeCanvasCardSize()
    {
        switch (ContentKind?.Trim().ToLowerInvariant())
        {
            case "image":
                Width = Math.Max(260, Width);
                Height = Math.Max(196, Height);
                break;
            case "file":
            case "link":
                Width = Math.Max(240, Width);
                Height = Math.Max(132, Height);
                break;
            default:
                Width = Math.Max(220, Width);
                Height = Math.Max(152, Height);
                break;
        }
    }
}

public partial class CreatorCanvasGraph : ObservableObject
{
    [ObservableProperty]
    private string _name = "Untitled";

    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private string _fullPath = string.Empty;

    [ObservableProperty]
    private string _relativePath = string.Empty;

    [ObservableProperty]
    private ObservableCollection<CreatorCanvasNode> _nodes = new();

    [ObservableProperty]
    private DateTimeOffset _lastModifiedUtc = DateTimeOffset.UtcNow;

    public bool HasNodes => Nodes.Count > 0;

    public string Summary => HasNodes ? $"{Nodes.Count} uzlu" : "Prazdny graf";

    public void NotifyNodesChanged()
    {
        OnPropertyChanged(nameof(HasNodes));
        OnPropertyChanged(nameof(Summary));
    }

    partial void OnRelativePathChanged(string value)
    {
        OnPropertyChanged(nameof(FileName));
    }
}

public sealed class CreatorGlobalSearchResult
{
    public string Label { get; set; } = string.Empty;

    public string Subtitle { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string TargetTab { get; set; } = string.Empty;

    public string? TargetPath { get; set; }

    public string CategoryIcon => Category switch
    {
        "Soubory" => "file",
        "Mody" => "mod",
        "Poznamky" => "note",
        "Git" => "git",
        "Manifest" => "manifest",
        _ => "search"
    };
}

public sealed class CreatorCanvasSpawnRequest
{
    public string NodeKind { get; set; } = "text";

    public double X { get; set; }

    public double Y { get; set; }
}
