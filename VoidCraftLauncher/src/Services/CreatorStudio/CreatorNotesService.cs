using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VoidCraftLauncher.Models.CreatorStudio;

namespace VoidCraftLauncher.Services.CreatorStudio;

public sealed class CreatorNotesService
{
    private const string NotesFolder = "notes";
    private const string WikiFolder = "wiki";
    private const string CanvasFolder = "canvas";
    private static readonly Regex WikiLinkRegex = new(@"\[\[([^\]]+)\]\]", RegexOptions.Compiled);

    public string GetNotesPath(string workspacePath)
    {
        return Path.Combine(workspacePath, NotesFolder);
    }

    public string GetWikiPath(string workspacePath)
    {
        return Path.Combine(GetNotesPath(workspacePath), WikiFolder);
    }

    public string GetCanvasPath(string workspacePath)
    {
        return Path.Combine(GetNotesPath(workspacePath), CanvasFolder);
    }

    public void EnsureNotesFolder(string workspacePath)
    {
        var path = GetNotesPath(workspacePath);
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }

    public List<CreatorNoteDocument> DiscoverDocuments(string workspacePath)
    {
        var notesPath = GetNotesPath(workspacePath);
        if (!Directory.Exists(notesPath))
            return new List<CreatorNoteDocument>();

        return EnumerateNoteFiles(notesPath, SearchOption.TopDirectoryOnly)
            .OrderByDescending(f => File.GetLastWriteTimeUtc(f))
            .Select(f => BuildDocument(notesPath, f, CreatorNoteStorageKind.Docs))
            .ToList();
    }

    public List<CreatorNoteDocument> DiscoverWikiDocuments(string workspacePath)
    {
        var notesPath = GetNotesPath(workspacePath);
        var wikiPath = GetWikiPath(workspacePath);
        if (!Directory.Exists(wikiPath))
            return new List<CreatorNoteDocument>();

        return EnumerateNoteFiles(wikiPath, SearchOption.AllDirectories)
            .OrderByDescending(f => File.GetLastWriteTimeUtc(f))
            .Select(f => BuildDocument(notesPath, f, CreatorNoteStorageKind.Wiki))
            .ToList();
    }

    public async Task<string> LoadDocumentContentAsync(string fullPath)
    {
        if (!File.Exists(fullPath))
            return string.Empty;

        return await File.ReadAllTextAsync(fullPath);
    }

    public async Task SaveDocumentAsync(string fullPath, string content)
    {
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        await File.WriteAllTextAsync(fullPath, content);
    }

    public async Task<CreatorNoteDocument> CreateDocumentAsync(string workspacePath, string title, CreatorNoteStorageKind storageKind = CreatorNoteStorageKind.Docs)
    {
        EnsureNotesFolder(workspacePath);
        var notesPath = GetNotesPath(workspacePath);
        var sanitized = SanitizeFileName(title);
        var baseFolder = storageKind == CreatorNoteStorageKind.Wiki ? GetWikiPath(workspacePath) : notesPath;
        Directory.CreateDirectory(baseFolder);

        var path = Path.Combine(baseFolder, $"{sanitized}.md");

        var counter = 1;
        while (File.Exists(path))
        {
            path = Path.Combine(baseFolder, $"{sanitized}-{counter}.md");
            counter++;
        }

        var initialContent = storageKind == CreatorNoteStorageKind.Wiki
            ? $"# {title}\n\n## Odkazy\n\n"
            : $"# {title}\n\n";
        await File.WriteAllTextAsync(path, initialContent);

        return BuildDocument(notesPath, path, storageKind, initialContent);
    }

    public bool DeleteDocument(string fullPath)
    {
        if (!File.Exists(fullPath)) return false;
        File.Delete(fullPath);
        return true;
    }

    public List<CreatorCanvasGraph> DiscoverCanvasGraphs(string workspacePath)
    {
        var notesPath = GetNotesPath(workspacePath);
        var canvasPath = GetCanvasPath(workspacePath);
        if (!Directory.Exists(canvasPath))
            return new List<CreatorCanvasGraph>();

        var graphs = new List<CreatorCanvasGraph>();
        foreach (var file in Directory.EnumerateFiles(canvasPath, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var graph = JsonSerializer.Deserialize<CreatorCanvasGraph>(json);
                if (graph != null)
                {
                    NormalizeGraph(graph);
                    graph.FileName = Path.GetFileName(file);
                    graph.FullPath = file;
                    graph.RelativePath = NormalizeRelativePath(Path.GetRelativePath(notesPath, file));
                    graphs.Add(graph);
                }
            }
            catch
            {
                // Skip invalid files
            }
        }

        return graphs;
    }

    public async Task<CreatorCanvasGraph> CreateCanvasGraphAsync(string workspacePath, string name)
    {
        var canvasPath = GetCanvasPath(workspacePath);
        Directory.CreateDirectory(canvasPath);

        var sanitized = SanitizeFileName(name);
        var fullPath = Path.Combine(canvasPath, $"{sanitized}.json");
        var counter = 1;
        while (File.Exists(fullPath))
        {
            fullPath = Path.Combine(canvasPath, $"{sanitized}-{counter}.json");
            counter++;
        }

        var graph = new CreatorCanvasGraph
        {
            Name = name,
            FileName = Path.GetFileName(fullPath),
            FullPath = fullPath,
            RelativePath = NormalizeRelativePath(Path.GetRelativePath(GetNotesPath(workspacePath), fullPath)),
            Nodes = new ObservableCollection<CreatorCanvasNode>()
        };

        await SaveCanvasGraphAsync(workspacePath, graph);
        return graph;
    }

    public async Task SaveCanvasGraphAsync(string workspacePath, CreatorCanvasGraph graph)
    {
        var canvasPath = GetCanvasPath(workspacePath);
        Directory.CreateDirectory(canvasPath);

        var fullPath = graph.FullPath;
        if (string.IsNullOrWhiteSpace(fullPath))
        {
            var fileName = SanitizeFileName(graph.Name) + ".json";
            fullPath = Path.Combine(canvasPath, fileName);
        }

        graph.FileName = Path.GetFileName(fullPath);
        graph.FullPath = fullPath;
        graph.RelativePath = NormalizeRelativePath(Path.GetRelativePath(GetNotesPath(workspacePath), fullPath));
        graph.LastModifiedUtc = DateTimeOffset.UtcNow;
        NormalizeGraph(graph);

        var json = JsonSerializer.Serialize(graph, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(fullPath, json);
    }

    public bool DeleteCanvasGraph(string fullPath)
    {
        if (!File.Exists(fullPath))
            return false;

        File.Delete(fullPath);
        return true;
    }

    public IReadOnlyList<string> ExtractWikiLinks(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return Array.Empty<string>();

        return WikiLinkRegex.Matches(content)
            .Select(match => match.Groups[1].Value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IEnumerable<string> EnumerateNoteFiles(string rootPath, SearchOption searchOption)
    {
        return Directory.EnumerateFiles(rootPath, "*.md", searchOption)
            .Concat(Directory.EnumerateFiles(rootPath, "*.txt", searchOption));
    }

    private CreatorNoteDocument BuildDocument(string notesPath, string fullPath, CreatorNoteStorageKind storageKind, string? contentOverride = null)
    {
        var info = new FileInfo(fullPath);
        var content = contentOverride ?? SafeReadAllText(fullPath);
        return new CreatorNoteDocument
        {
            Title = ResolveDocumentTitle(fullPath, content),
            FileName = info.Name,
            FullPath = fullPath,
            RelativePath = NormalizeRelativePath(Path.GetRelativePath(notesPath, fullPath)),
            Content = content,
            LastModifiedUtc = info.LastWriteTimeUtc,
            StorageKind = storageKind,
            LinkedTitles = ExtractWikiLinks(content).ToList()
        };
    }

    private static string SafeReadAllText(string fullPath)
    {
        try
        {
            return File.ReadAllText(fullPath);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ResolveDocumentTitle(string fullPath, string content)
    {
        if (!string.IsNullOrWhiteSpace(content))
        {
            foreach (var rawLine in content.Split('\n'))
            {
                var line = rawLine.Trim();
                if (!line.StartsWith('#'))
                    continue;

                var header = line.TrimStart('#').Trim();
                if (!string.IsNullOrWhiteSpace(header))
                    return header;
            }
        }

        return Path.GetFileNameWithoutExtension(fullPath);
    }

    private static void NormalizeGraph(CreatorCanvasGraph graph)
    {
        graph.Nodes ??= new ObservableCollection<CreatorCanvasNode>();

        foreach (var node in graph.Nodes)
        {
            node.Label = string.IsNullOrWhiteSpace(node.Label) ? "Untitled node" : node.Label.Trim();
            node.NodeType = NormalizeNodeType(node.NodeType);
            node.ContentKind = NormalizeContentKind(node.ContentKind);
            node.Description ??= string.Empty;
            node.ContentValue ??= string.Empty;
            node.ConnectedNodeIds ??= new List<string>();
            node.ConnectedNodeIds = node.ConnectedNodeIds
                .Where(id => !string.IsNullOrWhiteSpace(id) && !string.Equals(id, node.Id, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            node.NormalizeCanvasCardSize();
            node.NotifyConnectionsChanged();
        }

        graph.NotifyNodesChanged();
    }

    private static string NormalizeNodeType(string? nodeType)
    {
        return nodeType?.Trim().ToLowerInvariant() switch
        {
            "questline" => "questline",
            "gate" => "gate",
            "boss" => "boss",
            "dimension" => "dimension",
            "recipe" => "recipe",
            "blocker" => "blocker",
            _ => "idea"
        };
    }

    private static string NormalizeContentKind(string? contentKind)
    {
        return contentKind?.Trim().ToLowerInvariant() switch
        {
            "image" => "image",
            "file" => "file",
            "link" => "link",
            _ => "text"
        };
    }

    private static string NormalizeRelativePath(string path)
    {
        return path.Replace('\\', '/');
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Where(c => !invalid.Contains(c)).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "untitled" : sanitized.ToLowerInvariant().Replace(' ', '-');
    }
}
