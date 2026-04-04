using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Tomlyn;
using VoidCraftLauncher.Models;
using VoidCraftLauncher.Models.CreatorStudio;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace VoidCraftLauncher.Services.CreatorStudio;

public sealed class CreatorWorkbenchEditorService
{
    private const string LoadedComparisonId = "loaded";
    private const string ModeStructured = "structured";
    private const string ModeRaw = "raw";
    private const string ModeSplit = "split";

    private static readonly Regex MarkdownHeadingRegex = new(@"^(#{1,6})\s+(.+?)\s*$", RegexOptions.Compiled);
    private static readonly Regex FunctionRegex = new(@"\bfunction\s+([A-Za-z0-9_]+)\s*\(", RegexOptions.Compiled);
    private static readonly Regex ZsEventRegex = new(@"\bonEvent\s*\(\s*['""]([^'""]+)['""]", RegexOptions.Compiled);
    private static readonly Regex KubeEventRegex = new(@"\b(?:ServerEvents|ClientEvents|BlockEvents|ItemEvents|PlayerEvents|EntityEvents|LevelEvents)\.([A-Za-z0-9_]+)\s*\(", RegexOptions.Compiled);

    private readonly LauncherService _launcherService;
    private readonly IDeserializer _yamlDeserializer;
    private readonly ISerializer _yamlSerializer;

    public CreatorWorkbenchEditorService(LauncherService launcherService)
    {
        _launcherService = launcherService;
        _yamlDeserializer = new DeserializerBuilder().Build();
        _yamlSerializer = new SerializerBuilder().Build();
    }

    public CreatorWorkbenchDocumentAnalysis Analyze(CreatorWorkbenchFile file, string content)
    {
        var normalizedContent = content ?? string.Empty;
        var kind = DetectKind(file, normalizedContent);

        return kind switch
        {
            CreatorWorkbenchDocumentKind.Json => AnalyzeJson(normalizedContent, allowJson5: false),
            CreatorWorkbenchDocumentKind.Json5 => AnalyzeJson(normalizedContent, allowJson5: true),
            CreatorWorkbenchDocumentKind.Toml => AnalyzeToml(normalizedContent),
            CreatorWorkbenchDocumentKind.Yaml => AnalyzeYaml(normalizedContent),
            CreatorWorkbenchDocumentKind.Ini => AnalyzeIni(normalizedContent),
            CreatorWorkbenchDocumentKind.Properties => AnalyzeProperties(normalizedContent),
            CreatorWorkbenchDocumentKind.Markdown => AnalyzeMarkdown(normalizedContent),
            CreatorWorkbenchDocumentKind.Script => AnalyzeScript(file, normalizedContent),
            CreatorWorkbenchDocumentKind.Csv => AnalyzeCsv(normalizedContent),
            CreatorWorkbenchDocumentKind.Snbt => BuildPlainTextAnalysis(CreatorWorkbenchDocumentKind.Snbt, "SNBT", "Raw text", normalizedContent, "SNBT zobrazeny v raw rezimu."),
            _ => BuildPlainTextAnalysis(CreatorWorkbenchDocumentKind.Text, "Text", "Raw text", normalizedContent, "Soubor je otevreny v raw rezimu bez parseru.")
        };
    }

    public CreatorWorkbenchStructuredRenderResult RenderStructuredDocument(
        CreatorWorkbenchDocumentAnalysis analysis,
        IEnumerable<CreatorWorkbenchStructuredField> fields,
        string fallbackContent)
    {
        if (!analysis.SupportsStructuredEditing)
        {
            return new CreatorWorkbenchStructuredRenderResult
            {
                Content = fallbackContent ?? string.Empty,
                Issues = new[]
                {
                    CreateIssue(CreatorWorkbenchIssueSeverity.Warning, "Structured", "Tenhle soubor nema aktivni structured editor.")
                }
            };
        }

        var renderIssues = new List<CreatorWorkbenchValidationIssue>();

        try
        {
            var materializedFields = fields.ToList();
            string content = analysis.Kind switch
            {
                CreatorWorkbenchDocumentKind.Json or CreatorWorkbenchDocumentKind.Json5 => RenderJsonLike(analysis, materializedFields, renderIssues),
                CreatorWorkbenchDocumentKind.Toml => RenderToml(analysis, materializedFields, renderIssues),
                CreatorWorkbenchDocumentKind.Yaml => RenderYaml(analysis, materializedFields, renderIssues),
                CreatorWorkbenchDocumentKind.Ini => RenderIni(materializedFields),
                CreatorWorkbenchDocumentKind.Properties => RenderProperties(materializedFields),
                _ => fallbackContent ?? string.Empty
            };

            return new CreatorWorkbenchStructuredRenderResult
            {
                Content = content,
                Issues = renderIssues
            };
        }
        catch (Exception ex)
        {
            return new CreatorWorkbenchStructuredRenderResult
            {
                Content = fallbackContent ?? string.Empty,
                Issues = new[]
                {
                    CreateIssue(CreatorWorkbenchIssueSeverity.Error, "Structured", $"Structured render selhal: {ex.Message}")
                }
            };
        }
    }

    public IReadOnlyList<CreatorWorkbenchComparisonTarget> BuildComparisonTargets(
        string? workspaceId,
        string? workspacePath,
        CreatorWorkbenchFile? file)
    {
        var targets = new List<CreatorWorkbenchComparisonTarget>
        {
            new()
            {
                Id = LoadedComparisonId,
                Label = "Nactena baze",
                Summary = "Stav souboru pri otevreni v Creator Studiu.",
                Kind = CreatorWorkbenchComparisonTargetKind.Loaded
            }
        };

        if (file == null || string.IsNullOrWhiteSpace(workspacePath) || !Directory.Exists(workspacePath))
        {
            return targets;
        }

        var latestSnapshotFile = TryResolveLatestSnapshotFile(workspaceId, file.RelativePath);
        if (!string.IsNullOrWhiteSpace(latestSnapshotFile))
        {
            targets.Add(new CreatorWorkbenchComparisonTarget
            {
                Id = $"snapshot:{latestSnapshotFile}",
                Label = "Posledni snapshot",
                Summary = Path.GetFileName(Path.GetDirectoryName(latestSnapshotFile) ?? latestSnapshotFile),
                Kind = CreatorWorkbenchComparisonTargetKind.Snapshot,
                SourcePath = latestSnapshotFile
            });
        }

        var latestExport = TryResolveLatestExportArchive(workspacePath, file.RelativePath);
        if (latestExport != null)
        {
            targets.Add(new CreatorWorkbenchComparisonTarget
            {
                Id = $"export:{latestExport.Value.ArchivePath}:{latestExport.Value.EntryPath}",
                Label = "Posledni export",
                Summary = Path.GetFileName(latestExport.Value.ArchivePath),
                Kind = CreatorWorkbenchComparisonTargetKind.Export,
                SourcePath = latestExport.Value.ArchivePath,
                RelativeEntryPath = latestExport.Value.EntryPath
            });
        }

        var defaultCounterpart = TryResolveDefaultCounterpart(workspacePath, file.RelativePath);
        if (!string.IsNullOrWhiteSpace(defaultCounterpart))
        {
            targets.Add(new CreatorWorkbenchComparisonTarget
            {
                Id = $"default:{defaultCounterpart}",
                Label = "Default counterpart",
                Summary = Path.GetRelativePath(workspacePath, defaultCounterpart),
                Kind = CreatorWorkbenchComparisonTargetKind.DefaultCounterpart,
                SourcePath = defaultCounterpart
            });
        }

        return targets;
    }

    public async Task<string?> LoadComparisonContentAsync(CreatorWorkbenchComparisonTarget target, string loadedContent)
    {
        if (target.Kind == CreatorWorkbenchComparisonTargetKind.Loaded)
        {
            return loadedContent ?? string.Empty;
        }

        if (target.Kind == CreatorWorkbenchComparisonTargetKind.Export)
        {
            if (!File.Exists(target.SourcePath) || string.IsNullOrWhiteSpace(target.RelativeEntryPath))
            {
                return null;
            }

            using var archive = ZipFile.OpenRead(target.SourcePath);
            var entry = archive.GetEntry(target.RelativeEntryPath.Replace('\\', '/'));
            if (entry == null)
            {
                return null;
            }

            using var stream = entry.Open();
            using var reader = new StreamReader(stream, Encoding.UTF8, true);
            return await reader.ReadToEndAsync();
        }

        if (string.IsNullOrWhiteSpace(target.SourcePath) || !File.Exists(target.SourcePath))
        {
            return null;
        }

        using var fileStream = new FileStream(target.SourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var streamReader = new StreamReader(fileStream, Encoding.UTF8, true);
        return await streamReader.ReadToEndAsync();
    }

    public IReadOnlyList<CreatorWorkbenchDiffLine> BuildDiff(string baselineContent, string currentContent)
    {
        var diffModel = new InlineDiffBuilder(new Differ()).BuildDiffModel(baselineContent ?? string.Empty, currentContent ?? string.Empty);
        return diffModel.Lines.Select(line => new CreatorWorkbenchDiffLine
        {
            OldLineNumber = line.Type == ChangeType.Inserted ? null : line.Position,
            NewLineNumber = line.Type == ChangeType.Deleted ? null : line.Position,
            Text = line.Text ?? string.Empty,
            Kind = line.Type switch
            {
                ChangeType.Inserted => CreatorWorkbenchDiffKind.Inserted,
                ChangeType.Deleted => CreatorWorkbenchDiffKind.Deleted,
                ChangeType.Modified => CreatorWorkbenchDiffKind.Modified,
                _ => CreatorWorkbenchDiffKind.Unchanged
            }
        }).ToList();
    }

    private CreatorWorkbenchDocumentAnalysis AnalyzeJson(string content, bool allowJson5)
    {
        if (!TryParseJson(content, allowJson5, out var token, out var issues))
        {
            return new CreatorWorkbenchDocumentAnalysis
            {
                Kind = allowJson5 ? CreatorWorkbenchDocumentKind.Json5 : CreatorWorkbenchDocumentKind.Json,
                KindLabel = allowJson5 ? "JSON5" : "JSON",
                ParserLabel = "Json.NET",
                Summary = "Parser zrusil structured rezim a soubor zustava v raw editoru.",
                StructuredSaveIsLossy = allowJson5,
                StructuredSaveWarning = allowJson5
                    ? "Structured save prevede JSON5 do cisteho JSON tvaru a zahodi komentare i trailing commas."
                    : string.Empty,
                RecommendedModeId = ModeRaw,
                Issues = issues
            };
        }

        var definitions = new List<CreatorWorkbenchNodeDefinition>();
        var outline = new List<CreatorWorkbenchOutlineItem>();
        var fields = new List<CreatorWorkbenchStructuredField>();
        CollectJsonToken(token!, new List<PathToken>(), definitions, outline, fields);

        if (allowJson5)
        {
            issues.Add(CreateIssue(CreatorWorkbenchIssueSeverity.Warning, "Structured", "Structured save zapisuje JSON5 jako platny JSON subset bez komentaru."));
        }

        return BuildStructuredAnalysis(
            allowJson5 ? CreatorWorkbenchDocumentKind.Json5 : CreatorWorkbenchDocumentKind.Json,
            allowJson5 ? "JSON5" : "JSON",
            "Json.NET",
            fields,
            outline,
            definitions,
            issues,
            allowJson5,
            allowJson5
                ? "Structured save prevede JSON5 do cisteho JSON tvaru a zahodi komentare i trailing commas."
                : string.Empty);
    }

    private CreatorWorkbenchDocumentAnalysis AnalyzeToml(string content)
    {
        var issues = new List<CreatorWorkbenchValidationIssue>();
        try
        {
            var model = NormalizeStructuredValue(TomlSerializer.Deserialize<Dictionary<string, object?>>(content));
            return BuildGenericStructuredAnalysis(
                CreatorWorkbenchDocumentKind.Toml,
                "TOML",
                "Tomlyn",
                model,
                issues,
                structuredWarning: "Structured save normalizuje poradi a odstrani puvodni komentare.");
        }
        catch (TomlException ex)
        {
            issues.Add(CreateIssue(CreatorWorkbenchIssueSeverity.Error, "TOML", ex.Message));
            return new CreatorWorkbenchDocumentAnalysis
            {
                Kind = CreatorWorkbenchDocumentKind.Toml,
                KindLabel = "TOML",
                ParserLabel = "Tomlyn",
                Summary = "TOML parser nasel chyby. Structured edit je proto vypnuty.",
                StructuredSaveIsLossy = true,
                StructuredSaveWarning = "Structured save normalizuje poradi a odstrani puvodni komentare.",
                RecommendedModeId = ModeRaw,
                Issues = issues
            };
        }
    }

    private CreatorWorkbenchDocumentAnalysis AnalyzeYaml(string content)
    {
        var issues = new List<CreatorWorkbenchValidationIssue>();

        try
        {
            var yamlObject = _yamlDeserializer.Deserialize(new StringReader(content));
            var model = NormalizeStructuredValue(yamlObject);
            return BuildGenericStructuredAnalysis(
                CreatorWorkbenchDocumentKind.Yaml,
                "YAML",
                "YamlDotNet",
                model,
                issues,
                structuredWarning: "Structured save serializuje YAML znovu a muze zmenit styl i komentare.");
        }
        catch (YamlException ex)
        {
            issues.Add(CreateIssue(CreatorWorkbenchIssueSeverity.Error, "YAML", ex.Message));
            return new CreatorWorkbenchDocumentAnalysis
            {
                Kind = CreatorWorkbenchDocumentKind.Yaml,
                KindLabel = "YAML",
                ParserLabel = "YamlDotNet",
                Summary = "YAML parser selhal. Soubor zustava v raw editoru.",
                StructuredSaveIsLossy = true,
                StructuredSaveWarning = "Structured save serializuje YAML znovu a muze zmenit styl i komentare.",
                RecommendedModeId = ModeRaw,
                Issues = issues
            };
        }
    }

    private CreatorWorkbenchDocumentAnalysis AnalyzeIni(string content)
    {
        var issues = new List<CreatorWorkbenchValidationIssue>();
        var outline = new List<CreatorWorkbenchOutlineItem>();
        var fields = new List<CreatorWorkbenchStructuredField>();
        var definitions = new List<CreatorWorkbenchNodeDefinition>
        {
            new() { PathKey = string.Empty, Kind = CreatorWorkbenchNodeKind.Object, Depth = 0 }
        };

        string currentSection = string.Empty;
        var definedSections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lines = SplitLines(content);
        for (int index = 0; index < lines.Length; index++)
        {
            var rawLine = lines[index];
            var trimmed = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith(';') || trimmed.StartsWith('#'))
            {
                continue;
            }

            if (trimmed.StartsWith('[') && trimmed.EndsWith(']') && trimmed.Length > 2)
            {
                currentSection = trimmed[1..^1].Trim();
                if (definedSections.Add(currentSection))
                {
                    var sectionPath = new List<PathToken> { PathToken.Property(currentSection) };
                    definitions.Add(new CreatorWorkbenchNodeDefinition
                    {
                        PathKey = EncodePath(sectionPath),
                        Kind = CreatorWorkbenchNodeKind.Section,
                        Depth = 1
                    });
                    outline.Add(CreateOutlineItem(sectionPath, CreatorWorkbenchNodeKind.Section, "Sekce", $"radek {index + 1}"));
                }

                continue;
            }

            var separatorIndex = rawLine.IndexOf('=');
            if (separatorIndex < 0)
            {
                separatorIndex = rawLine.IndexOf(':');
            }

            if (separatorIndex <= 0)
            {
                issues.Add(CreateIssue(CreatorWorkbenchIssueSeverity.Warning, "INI", $"Radek {index + 1} nema rozpoznany key/value tvar."));
                continue;
            }

            var key = rawLine[..separatorIndex].Trim();
            var value = rawLine[(separatorIndex + 1)..].Trim();
            var pathTokens = new List<PathToken>();
            if (!string.IsNullOrWhiteSpace(currentSection))
            {
                pathTokens.Add(PathToken.Property(currentSection));
            }

            pathTokens.Add(PathToken.Property(key));
            fields.Add(CreateField(pathTokens, value, CreatorWorkbenchValueKind.String));
            outline.Add(CreateOutlineItem(pathTokens, CreatorWorkbenchNodeKind.Scalar, "Key", $"radek {index + 1}"));
        }

        return BuildStructuredAnalysis(
            CreatorWorkbenchDocumentKind.Ini,
            "INI / CFG",
            "Section parser",
            fields,
            outline,
            definitions,
            issues,
            structuredSaveIsLossy: true,
            structuredWarning: "Structured save znovu sestavi INI a zahodi puvodni komentare i poradi." );
    }

    private CreatorWorkbenchDocumentAnalysis AnalyzeProperties(string content)
    {
        var issues = new List<CreatorWorkbenchValidationIssue>();
        var outline = new List<CreatorWorkbenchOutlineItem>();
        var fields = new List<CreatorWorkbenchStructuredField>();
        var definitions = new List<CreatorWorkbenchNodeDefinition>
        {
            new() { PathKey = string.Empty, Kind = CreatorWorkbenchNodeKind.Object, Depth = 0 }
        };

        var lines = SplitLines(content);
        for (int index = 0; index < lines.Length; index++)
        {
            var rawLine = lines[index];
            var trimmed = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#') || trimmed.StartsWith('!'))
            {
                continue;
            }

            var separatorIndex = rawLine.IndexOf('=');
            if (separatorIndex < 0)
            {
                separatorIndex = rawLine.IndexOf(':');
            }

            if (separatorIndex <= 0)
            {
                issues.Add(CreateIssue(CreatorWorkbenchIssueSeverity.Warning, "Properties", $"Radek {index + 1} nema rozpoznany key/value tvar."));
                continue;
            }

            var key = rawLine[..separatorIndex].Trim();
            var value = rawLine[(separatorIndex + 1)..].Trim();
            var pathTokens = new List<PathToken> { PathToken.Property(key) };
            fields.Add(CreateField(pathTokens, value, CreatorWorkbenchValueKind.String));
            outline.Add(CreateOutlineItem(pathTokens, CreatorWorkbenchNodeKind.Scalar, "Property", $"radek {index + 1}"));
        }

        return BuildStructuredAnalysis(
            CreatorWorkbenchDocumentKind.Properties,
            "Properties",
            "Properties parser",
            fields,
            outline,
            definitions,
            issues,
            structuredSaveIsLossy: true,
            structuredWarning: "Structured save znovu sestavi properties soubor a zahodi puvodni komentare i poradi.");
    }

    private CreatorWorkbenchDocumentAnalysis AnalyzeMarkdown(string content)
    {
        var outline = new List<CreatorWorkbenchOutlineItem>();
        var lines = SplitLines(content);
        for (int index = 0; index < lines.Length; index++)
        {
            var match = MarkdownHeadingRegex.Match(lines[index]);
            if (!match.Success)
            {
                continue;
            }

            var depth = Math.Max(0, match.Groups[1].Value.Length - 1);
            var title = match.Groups[2].Value.Trim();
            outline.Add(new CreatorWorkbenchOutlineItem
            {
                PathKey = $"md:{index + 1}",
                DisplayPath = title,
                Label = title,
                KindLabel = $"H{depth + 1}",
                Summary = $"radek {index + 1}",
                Depth = depth
            });
        }

        return new CreatorWorkbenchDocumentAnalysis
        {
            Kind = CreatorWorkbenchDocumentKind.Markdown,
            KindLabel = "Markdown",
            ParserLabel = "Heading outline",
            Summary = outline.Count == 0 ? "Markdown bez rozpoznanych nadpisu." : $"Markdown outline: {outline.Count} nadpisu.",
            RecommendedModeId = ModeRaw,
            Outline = outline
        };
    }

    private CreatorWorkbenchDocumentAnalysis AnalyzeScript(CreatorWorkbenchFile file, string content)
    {
        var outline = new List<CreatorWorkbenchOutlineItem>();
        var lines = SplitLines(content);
        for (int index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            var functionMatch = FunctionRegex.Match(line);
            if (functionMatch.Success)
            {
                var name = functionMatch.Groups[1].Value;
                outline.Add(new CreatorWorkbenchOutlineItem
                {
                    PathKey = $"script:function:{index + 1}:{name}",
                    DisplayPath = name,
                    Label = name,
                    KindLabel = "Function",
                    Summary = $"radek {index + 1}",
                    Depth = 0
                });
                continue;
            }

            var kubeEventMatch = KubeEventRegex.Match(line);
            if (kubeEventMatch.Success)
            {
                var name = kubeEventMatch.Groups[1].Value;
                outline.Add(new CreatorWorkbenchOutlineItem
                {
                    PathKey = $"script:event:{index + 1}:{name}",
                    DisplayPath = name,
                    Label = name,
                    KindLabel = "Event",
                    Summary = $"radek {index + 1}",
                    Depth = 0
                });
                continue;
            }

            var zsEventMatch = ZsEventRegex.Match(line);
            if (zsEventMatch.Success)
            {
                var name = zsEventMatch.Groups[1].Value;
                outline.Add(new CreatorWorkbenchOutlineItem
                {
                    PathKey = $"script:hook:{index + 1}:{name}",
                    DisplayPath = name,
                    Label = name,
                    KindLabel = "Hook",
                    Summary = $"radek {index + 1}",
                    Depth = 0
                });
            }
        }

        var issues = new List<CreatorWorkbenchValidationIssue>
        {
            CreateIssue(CreatorWorkbenchIssueSeverity.Info, "Script", $"{Path.GetExtension(file.RelativePath).ToLowerInvariant()} soubor zustava v raw rezimu, ale outline rychle najde eventy a funkce.")
        };

        return new CreatorWorkbenchDocumentAnalysis
        {
            Kind = CreatorWorkbenchDocumentKind.Script,
            KindLabel = "Script",
            ParserLabel = "Function outline",
            Summary = outline.Count == 0 ? "Script bez rozpoznanych eventu nebo funkci." : $"Script outline: {outline.Count} bodu.",
            RecommendedModeId = ModeRaw,
            Outline = outline,
            Issues = issues
        };
    }

    private CreatorWorkbenchDocumentAnalysis AnalyzeCsv(string content)
    {
        var outline = new List<CreatorWorkbenchOutlineItem>();
        var lines = SplitLines(content);
        if (lines.Length > 0)
        {
            var headers = lines[0].Split(',');
            for (int index = 0; index < headers.Length; index++)
            {
                outline.Add(new CreatorWorkbenchOutlineItem
                {
                    PathKey = $"csv:{index}",
                    DisplayPath = headers[index].Trim(),
                    Label = headers[index].Trim(),
                    KindLabel = "Column",
                    Summary = $"sloupec {index + 1}",
                    Depth = 0
                });
            }
        }

        return new CreatorWorkbenchDocumentAnalysis
        {
            Kind = CreatorWorkbenchDocumentKind.Csv,
            KindLabel = "CSV",
            ParserLabel = "Header outline",
            Summary = outline.Count == 0 ? "CSV bez hlavnicky." : $"CSV obsahuje {outline.Count} sloupcu.",
            RecommendedModeId = ModeRaw,
            Outline = outline
        };
    }

    private static CreatorWorkbenchDocumentAnalysis BuildPlainTextAnalysis(
        CreatorWorkbenchDocumentKind kind,
        string kindLabel,
        string parserLabel,
        string content,
        string summary)
    {
        return new CreatorWorkbenchDocumentAnalysis
        {
            Kind = kind,
            KindLabel = kindLabel,
            ParserLabel = parserLabel,
            Summary = string.IsNullOrWhiteSpace(content) ? "Soubor je prazdny." : summary,
            RecommendedModeId = ModeRaw
        };
    }

    private CreatorWorkbenchDocumentAnalysis BuildGenericStructuredAnalysis(
        CreatorWorkbenchDocumentKind kind,
        string kindLabel,
        string parserLabel,
        object? model,
        List<CreatorWorkbenchValidationIssue> issues,
        string structuredWarning)
    {
        var definitions = new List<CreatorWorkbenchNodeDefinition>();
        var outline = new List<CreatorWorkbenchOutlineItem>();
        var fields = new List<CreatorWorkbenchStructuredField>();
        CollectGenericStructure(model, new List<PathToken>(), definitions, outline, fields);

        return BuildStructuredAnalysis(
            kind,
            kindLabel,
            parserLabel,
            fields,
            outline,
            definitions,
            issues,
            structuredSaveIsLossy: true,
            structuredWarning: structuredWarning);
    }

    private static CreatorWorkbenchDocumentAnalysis BuildStructuredAnalysis(
        CreatorWorkbenchDocumentKind kind,
        string kindLabel,
        string parserLabel,
        List<CreatorWorkbenchStructuredField> fields,
        List<CreatorWorkbenchOutlineItem> outline,
        List<CreatorWorkbenchNodeDefinition> definitions,
        List<CreatorWorkbenchValidationIssue> issues,
        bool structuredSaveIsLossy,
        string structuredWarning)
    {
        var supportsStructuredEditing = fields.Count > 0 && !issues.Any(issue => issue.IsError);
        return new CreatorWorkbenchDocumentAnalysis
        {
            Kind = kind,
            KindLabel = kindLabel,
            ParserLabel = parserLabel,
            Summary = supportsStructuredEditing
                ? $"{fields.Count} editovatelnych hodnot, {outline.Count} orientacnich bodu."
                : (issues.Any(issue => issue.IsError)
                    ? "Parser nasel chyby, structured rezim je proto vypnuty."
                    : "Soubor nema scalar pole pro structured editor."),
            SupportsStructuredEditing = supportsStructuredEditing,
            StructuredSaveIsLossy = structuredSaveIsLossy,
            StructuredSaveWarning = supportsStructuredEditing ? structuredWarning : string.Empty,
            RecommendedModeId = supportsStructuredEditing ? ModeSplit : ModeRaw,
            Fields = fields,
            Outline = outline,
            Issues = issues,
            NodeDefinitions = definitions,
            RootNodeKind = definitions.FirstOrDefault()?.Kind ?? CreatorWorkbenchNodeKind.Object
        };
    }

    private static CreatorWorkbenchDocumentKind DetectKind(CreatorWorkbenchFile file, string content)
    {
        var extension = Path.GetExtension(file.RelativePath).ToLowerInvariant();
        return extension switch
        {
            ".json" => CreatorWorkbenchDocumentKind.Json,
            ".json5" => CreatorWorkbenchDocumentKind.Json5,
            ".toml" => CreatorWorkbenchDocumentKind.Toml,
            ".yaml" or ".yml" => CreatorWorkbenchDocumentKind.Yaml,
            ".ini" => CreatorWorkbenchDocumentKind.Ini,
            ".properties" => CreatorWorkbenchDocumentKind.Properties,
            ".cfg" => LooksLikeIni(content) ? CreatorWorkbenchDocumentKind.Ini : CreatorWorkbenchDocumentKind.Text,
            ".md" => CreatorWorkbenchDocumentKind.Markdown,
            ".js" or ".zs" => CreatorWorkbenchDocumentKind.Script,
            ".csv" => CreatorWorkbenchDocumentKind.Csv,
            ".snbt" => CreatorWorkbenchDocumentKind.Snbt,
            _ when file.RelativePath.Contains("kubejs", StringComparison.OrdinalIgnoreCase) || file.RelativePath.Contains("scripts", StringComparison.OrdinalIgnoreCase) => CreatorWorkbenchDocumentKind.Script,
            _ => CreatorWorkbenchDocumentKind.Text
        };
    }

    private static bool LooksLikeIni(string content)
    {
        var lines = SplitLines(content);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#') || trimmed.StartsWith(';'))
            {
                continue;
            }

            return trimmed.StartsWith('[') || trimmed.Contains('=') || trimmed.Contains(':');
        }

        return false;
    }

    private static bool TryParseJson(string content, bool allowJson5, out JToken? token, out List<CreatorWorkbenchValidationIssue> issues)
    {
        issues = new List<CreatorWorkbenchValidationIssue>();
        if (TryParseJsonToken(content, out token, out var errorMessage))
        {
            return true;
        }

        if (allowJson5)
        {
            var normalized = NormalizeJson5Content(content);
            if (TryParseJsonToken(normalized, out token, out errorMessage))
            {
                return true;
            }
        }

        issues.Add(CreateIssue(CreatorWorkbenchIssueSeverity.Error, allowJson5 ? "JSON5" : "JSON", errorMessage ?? "Neznama chyba parseru."));
        return false;
    }

    private static bool TryParseJsonToken(string content, out JToken? token, out string? errorMessage)
    {
        try
        {
            using var stringReader = new StringReader(content);
            using var jsonReader = new JsonTextReader(stringReader)
            {
                DateParseHandling = DateParseHandling.None,
                FloatParseHandling = FloatParseHandling.Decimal
            };

            token = JToken.ReadFrom(jsonReader, new JsonLoadSettings
            {
                CommentHandling = CommentHandling.Ignore,
                DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Replace,
                LineInfoHandling = LineInfoHandling.Load
            });
            errorMessage = null;
            return true;
        }
        catch (Exception ex)
        {
            token = null;
            errorMessage = ex.Message;
            return false;
        }
    }

    private static string NormalizeJson5Content(string content)
    {
        var withoutComments = StripJsonComments(content);
        var normalizedStrings = NormalizeSingleQuotedStrings(withoutComments);
        var normalizedKeys = QuoteJson5ObjectKeys(normalizedStrings);
        return RemoveTrailingJsonCommas(normalizedKeys);
    }

    private static string StripJsonComments(string input)
    {
        var result = new StringBuilder(input.Length);
        bool inString = false;
        char stringDelimiter = '\0';
        bool escaping = false;

        for (int index = 0; index < input.Length; index++)
        {
            var current = input[index];
            var next = index + 1 < input.Length ? input[index + 1] : '\0';

            if (inString)
            {
                result.Append(current);
                if (escaping)
                {
                    escaping = false;
                }
                else if (current == '\\')
                {
                    escaping = true;
                }
                else if (current == stringDelimiter)
                {
                    inString = false;
                }

                continue;
            }

            if ((current == '"' || current == '\'') && !inString)
            {
                inString = true;
                stringDelimiter = current;
                result.Append(current);
                continue;
            }

            if (current == '/' && next == '/')
            {
                while (index < input.Length && input[index] != '\n')
                {
                    index++;
                }

                if (index < input.Length)
                {
                    result.Append(input[index]);
                }

                continue;
            }

            if (current == '/' && next == '*')
            {
                index += 2;
                while (index + 1 < input.Length && !(input[index] == '*' && input[index + 1] == '/'))
                {
                    index++;
                }

                index++;
                continue;
            }

            result.Append(current);
        }

        return result.ToString();
    }

    private static string NormalizeSingleQuotedStrings(string input)
    {
        var result = new StringBuilder(input.Length);
        bool inDoubleString = false;
        bool escapingDouble = false;

        for (int index = 0; index < input.Length; index++)
        {
            var current = input[index];

            if (inDoubleString)
            {
                result.Append(current);
                if (escapingDouble)
                {
                    escapingDouble = false;
                }
                else if (current == '\\')
                {
                    escapingDouble = true;
                }
                else if (current == '"')
                {
                    inDoubleString = false;
                }

                continue;
            }

            if (current == '"')
            {
                inDoubleString = true;
                result.Append(current);
                continue;
            }

            if (current != '\'')
            {
                result.Append(current);
                continue;
            }

            var literal = new StringBuilder();
            bool escaping = false;
            index++;

            for (; index < input.Length; index++)
            {
                current = input[index];
                if (escaping)
                {
                    literal.Append(current switch
                    {
                        '\'' => '\'',
                        '"' => '"',
                        '\\' => '\\',
                        'n' => '\n',
                        'r' => '\r',
                        't' => '\t',
                        _ => current
                    });
                    escaping = false;
                    continue;
                }

                if (current == '\\')
                {
                    escaping = true;
                    continue;
                }

                if (current == '\'')
                {
                    break;
                }

                literal.Append(current);
            }

            result.Append(JsonConvert.ToString(literal.ToString()));
        }

        return result.ToString();
    }

    private static string QuoteJson5ObjectKeys(string input)
    {
        var result = new StringBuilder(input.Length);
        var stack = new Stack<char>();
        bool inString = false;
        bool escaping = false;
        bool expectingKey = false;

        for (int index = 0; index < input.Length; index++)
        {
            var current = input[index];

            if (inString)
            {
                result.Append(current);
                if (escaping)
                {
                    escaping = false;
                }
                else if (current == '\\')
                {
                    escaping = true;
                }
                else if (current == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (current == '"')
            {
                inString = true;
                if (stack.Count > 0 && stack.Peek() == '{' && expectingKey)
                {
                    expectingKey = false;
                }

                result.Append(current);
                continue;
            }

            if (current == '{')
            {
                stack.Push('{');
                expectingKey = true;
                result.Append(current);
                continue;
            }

            if (current == '[')
            {
                stack.Push('[');
                result.Append(current);
                continue;
            }

            if (current == '}' || current == ']')
            {
                if (stack.Count > 0)
                {
                    stack.Pop();
                }

                expectingKey = false;
                result.Append(current);
                continue;
            }

            if (current == ',')
            {
                expectingKey = stack.Count > 0 && stack.Peek() == '{';
                result.Append(current);
                continue;
            }

            if (char.IsWhiteSpace(current))
            {
                result.Append(current);
                continue;
            }

            if (expectingKey && stack.Count > 0 && stack.Peek() == '{' && IsJsonIdentifierStart(current))
            {
                int end = index + 1;
                while (end < input.Length && IsJsonIdentifierPart(input[end]))
                {
                    end++;
                }

                int lookAhead = end;
                while (lookAhead < input.Length && char.IsWhiteSpace(input[lookAhead]))
                {
                    lookAhead++;
                }

                if (lookAhead < input.Length && input[lookAhead] == ':')
                {
                    var key = input[index..end];
                    result.Append('"').Append(EscapeJsonString(key)).Append('"');
                    index = end - 1;
                    expectingKey = false;
                    continue;
                }
            }

            if (current == ':')
            {
                result.Append(current);
                continue;
            }

            result.Append(current);
        }

        return result.ToString();
    }

    private static string RemoveTrailingJsonCommas(string input)
    {
        var result = new StringBuilder(input.Length);
        bool inString = false;
        bool escaping = false;

        for (int index = 0; index < input.Length; index++)
        {
            var current = input[index];

            if (inString)
            {
                result.Append(current);
                if (escaping)
                {
                    escaping = false;
                }
                else if (current == '\\')
                {
                    escaping = true;
                }
                else if (current == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (current == '"')
            {
                inString = true;
                result.Append(current);
                continue;
            }

            if (current == ',')
            {
                int lookAhead = index + 1;
                while (lookAhead < input.Length && char.IsWhiteSpace(input[lookAhead]))
                {
                    lookAhead++;
                }

                if (lookAhead < input.Length && (input[lookAhead] == '}' || input[lookAhead] == ']'))
                {
                    continue;
                }
            }

            result.Append(current);
        }

        return result.ToString();
    }

    private static bool IsJsonIdentifierStart(char character) => character == '_' || character == '$' || char.IsLetter(character);

    private static bool IsJsonIdentifierPart(char character) => character == '_' || character == '$' || character == '-' || char.IsLetterOrDigit(character);

    private static string EscapeJsonString(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static void CollectJsonToken(
        JToken token,
        List<PathToken> pathTokens,
        List<CreatorWorkbenchNodeDefinition> definitions,
        List<CreatorWorkbenchOutlineItem> outline,
        List<CreatorWorkbenchStructuredField> fields)
    {
        switch (token)
        {
            case JObject jsonObject:
                AddNodeDefinition(definitions, pathTokens, CreatorWorkbenchNodeKind.Object);
                if (pathTokens.Count > 0 || jsonObject.Count > 0)
                {
                    outline.Add(CreateOutlineItem(pathTokens, CreatorWorkbenchNodeKind.Object, "Object", $"{jsonObject.Count} polozek"));
                }

                foreach (var property in jsonObject.Properties())
                {
                    var childPath = CopyPath(pathTokens);
                    childPath.Add(PathToken.Property(property.Name));
                    CollectJsonToken(property.Value, childPath, definitions, outline, fields);
                }
                break;

            case JArray jsonArray:
                AddNodeDefinition(definitions, pathTokens, CreatorWorkbenchNodeKind.Array);
                if (pathTokens.Count > 0 || jsonArray.Count > 0)
                {
                    outline.Add(CreateOutlineItem(pathTokens, CreatorWorkbenchNodeKind.Array, "Array", $"{jsonArray.Count} polozek"));
                }

                for (int index = 0; index < jsonArray.Count; index++)
                {
                    var childPath = CopyPath(pathTokens);
                    childPath.Add(PathToken.ArrayIndex(index));
                    CollectJsonToken(jsonArray[index]!, childPath, definitions, outline, fields);
                }
                break;

            default:
                var scalar = ExtractScalarValue(token);
                var field = CreateField(pathTokens, scalar.DisplayValue, scalar.Kind);
                fields.Add(field);
                outline.Add(CreateOutlineItem(pathTokens, CreatorWorkbenchNodeKind.Scalar, field.ValueTypeLabel, Truncate(field.EditableValue, 64)));
                break;
        }
    }

    private static void CollectGenericStructure(
        object? value,
        List<PathToken> pathTokens,
        List<CreatorWorkbenchNodeDefinition> definitions,
        List<CreatorWorkbenchOutlineItem> outline,
        List<CreatorWorkbenchStructuredField> fields)
    {
        switch (value)
        {
            case IDictionary<string, object?> dictionary:
                AddNodeDefinition(definitions, pathTokens, CreatorWorkbenchNodeKind.Object);
                if (pathTokens.Count > 0 || dictionary.Count > 0)
                {
                    outline.Add(CreateOutlineItem(pathTokens, CreatorWorkbenchNodeKind.Object, "Object", $"{dictionary.Count} polozek"));
                }

                foreach (var pair in dictionary)
                {
                    var childPath = CopyPath(pathTokens);
                    childPath.Add(PathToken.Property(pair.Key));
                    CollectGenericStructure(pair.Value, childPath, definitions, outline, fields);
                }
                break;

            case IList<object?> list:
                AddNodeDefinition(definitions, pathTokens, CreatorWorkbenchNodeKind.Array);
                if (pathTokens.Count > 0 || list.Count > 0)
                {
                    outline.Add(CreateOutlineItem(pathTokens, CreatorWorkbenchNodeKind.Array, "Array", $"{list.Count} polozek"));
                }

                for (int index = 0; index < list.Count; index++)
                {
                    var childPath = CopyPath(pathTokens);
                    childPath.Add(PathToken.ArrayIndex(index));
                    CollectGenericStructure(list[index], childPath, definitions, outline, fields);
                }
                break;

            default:
                var kind = DetermineValueKind(value);
                fields.Add(CreateField(pathTokens, FormatScalar(value, kind), kind));
                outline.Add(CreateOutlineItem(pathTokens, CreatorWorkbenchNodeKind.Scalar, BuildValueTypeLabel(kind), Truncate(FormatScalar(value, kind), 64)));
                break;
        }
    }

    private static CreatorWorkbenchStructuredField CreateField(List<PathToken> pathTokens, string value, CreatorWorkbenchValueKind kind)
    {
        return new CreatorWorkbenchStructuredField
        {
            PathKey = EncodePath(pathTokens),
            DisplayPath = BuildDisplayPath(pathTokens),
            SectionLabel = pathTokens.Count > 1 ? BuildDisplayPath(pathTokens[..^1]) : "root",
            ValueTypeLabel = BuildValueTypeLabel(kind),
            ValueKind = kind,
            Depth = Math.Max(pathTokens.Count - 1, 0),
            OriginalValue = value,
            EditableValue = value
        };
    }

    private static CreatorWorkbenchOutlineItem CreateOutlineItem(List<PathToken> pathTokens, CreatorWorkbenchNodeKind kind, string kindLabel, string summary)
    {
        var displayPath = BuildDisplayPath(pathTokens);
        return new CreatorWorkbenchOutlineItem
        {
            PathKey = EncodePath(pathTokens),
            DisplayPath = displayPath,
            Label = pathTokens.Count == 0
                ? "root"
                : (pathTokens[^1].IsIndex ? $"[{pathTokens[^1].Index}]" : pathTokens[^1].Name),
            KindLabel = kindLabel,
            Summary = summary,
            Depth = kind == CreatorWorkbenchNodeKind.Scalar ? Math.Max(pathTokens.Count - 1, 0) : pathTokens.Count
        };
    }

    private static void AddNodeDefinition(List<CreatorWorkbenchNodeDefinition> definitions, List<PathToken> pathTokens, CreatorWorkbenchNodeKind kind)
    {
        definitions.Add(new CreatorWorkbenchNodeDefinition
        {
            PathKey = EncodePath(pathTokens),
            Kind = kind,
            Depth = pathTokens.Count
        });
    }

    private static (CreatorWorkbenchValueKind Kind, string DisplayValue) ExtractScalarValue(JToken token)
    {
        if (token.Type == JTokenType.Null)
        {
            return (CreatorWorkbenchValueKind.Null, "null");
        }

        return token.Type switch
        {
            JTokenType.Integer => (CreatorWorkbenchValueKind.Integer, Convert.ToString(token.Value<long>(), CultureInfo.InvariantCulture) ?? "0"),
            JTokenType.Float => (CreatorWorkbenchValueKind.Decimal, Convert.ToString(token.Value<decimal>(), CultureInfo.InvariantCulture) ?? "0"),
            JTokenType.Boolean => (CreatorWorkbenchValueKind.Boolean, (token.Value<bool>() ? "true" : "false")),
            _ => (CreatorWorkbenchValueKind.String, token.Value<string>() ?? token.ToString(Formatting.None))
        };
    }

    private string RenderJsonLike(
        CreatorWorkbenchDocumentAnalysis analysis,
        List<CreatorWorkbenchStructuredField> fields,
        List<CreatorWorkbenchValidationIssue> renderIssues)
    {
        var root = BuildStructuredRoot(analysis, fields, renderIssues);
        var token = ToJsonToken(root);
        return token.ToString(Formatting.Indented) + Environment.NewLine;
    }

    private string RenderToml(
        CreatorWorkbenchDocumentAnalysis analysis,
        List<CreatorWorkbenchStructuredField> fields,
        List<CreatorWorkbenchValidationIssue> renderIssues)
    {
        var root = BuildStructuredRoot(analysis, fields, renderIssues);
        if (root is not IDictionary<string, object?> dictionary)
        {
            renderIssues.Add(CreateIssue(CreatorWorkbenchIssueSeverity.Error, "TOML", "TOML render ocekava objekt v rootu."));
            return string.Empty;
        }

        var rendered = TomlSerializer.Serialize(dictionary);
        return EnsureTrailingNewLine(rendered);
    }

    private string RenderYaml(
        CreatorWorkbenchDocumentAnalysis analysis,
        List<CreatorWorkbenchStructuredField> fields,
        List<CreatorWorkbenchValidationIssue> renderIssues)
    {
        var root = BuildStructuredRoot(analysis, fields, renderIssues);
        var rendered = _yamlSerializer.Serialize(root);
        return EnsureTrailingNewLine(rendered);
    }

    private static string RenderIni(IEnumerable<CreatorWorkbenchStructuredField> fields)
    {
        var builder = new StringBuilder();
        var allFields = fields.ToList();

        foreach (var field in allFields.Where(field => DecodePath(field.PathKey).Count == 1))
        {
            var key = DecodePath(field.PathKey)[0].Name;
            builder.AppendLine($"{key} = {field.EditableValue}");
        }

        var sections = allFields
            .Where(field => DecodePath(field.PathKey).Count > 1)
            .GroupBy(field => DecodePath(field.PathKey)[0].Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (builder.Length > 0 && sections.Count > 0)
        {
            builder.AppendLine();
        }

        foreach (var section in sections)
        {
            builder.AppendLine($"[{section.Key}]");
            foreach (var field in section)
            {
                var path = DecodePath(field.PathKey);
                var key = path[^1].Name;
                builder.AppendLine($"{key} = {field.EditableValue}");
            }

            builder.AppendLine();
        }

        return builder.ToString().TrimEnd() + Environment.NewLine;
    }

    private static string RenderProperties(IEnumerable<CreatorWorkbenchStructuredField> fields)
    {
        var builder = new StringBuilder();
        foreach (var field in fields)
        {
            var path = DecodePath(field.PathKey);
            var key = path.Count == 0 ? field.DisplayPath : path[^1].Name;
            builder.AppendLine($"{key}={field.EditableValue}");
        }

        return builder.ToString().TrimEnd() + Environment.NewLine;
    }

    private static object BuildStructuredRoot(
        CreatorWorkbenchDocumentAnalysis analysis,
        IReadOnlyCollection<CreatorWorkbenchStructuredField> fields,
        List<CreatorWorkbenchValidationIssue> renderIssues)
    {
        object root = analysis.RootNodeKind == CreatorWorkbenchNodeKind.Array
            ? new List<object?>()
            : new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var definition in analysis.NodeDefinitions.OrderBy(definition => definition.Depth))
        {
            if (string.IsNullOrWhiteSpace(definition.PathKey))
            {
                continue;
            }

            EnsureContainer(root, DecodePath(definition.PathKey), definition.Kind);
        }

        foreach (var field in fields)
        {
            SetScalarValue(root, DecodePath(field.PathKey), ConvertFieldValue(field, renderIssues));
        }

        return root;
    }

    private static object? ConvertFieldValue(CreatorWorkbenchStructuredField field, List<CreatorWorkbenchValidationIssue> renderIssues)
    {
        var raw = field.EditableValue ?? string.Empty;
        var trimmed = raw.Trim();
        switch (field.ValueKind)
        {
            case CreatorWorkbenchValueKind.Integer:
                if (long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integerValue))
                {
                    return integerValue;
                }

                renderIssues.Add(CreateIssue(CreatorWorkbenchIssueSeverity.Warning, field.DisplayPath, "Ocekavano cele cislo, hodnota bude ulozena jako text."));
                return raw;

            case CreatorWorkbenchValueKind.Decimal:
                if (decimal.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var decimalValue))
                {
                    return decimalValue;
                }

                renderIssues.Add(CreateIssue(CreatorWorkbenchIssueSeverity.Warning, field.DisplayPath, "Ocekavano desetinne cislo, hodnota bude ulozena jako text."));
                return raw;

            case CreatorWorkbenchValueKind.Boolean:
                if (bool.TryParse(trimmed, out var boolValue))
                {
                    return boolValue;
                }

                renderIssues.Add(CreateIssue(CreatorWorkbenchIssueSeverity.Warning, field.DisplayPath, "Ocekavano true/false, hodnota bude ulozena jako text."));
                return raw;

            case CreatorWorkbenchValueKind.Null:
                if (string.IsNullOrWhiteSpace(trimmed) || string.Equals(trimmed, "null", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                renderIssues.Add(CreateIssue(CreatorWorkbenchIssueSeverity.Warning, field.DisplayPath, "Puvodni null byl zmenen na textovou hodnotu."));
                return raw;

            default:
                return raw;
        }
    }

    private static void EnsureContainer(object root, IReadOnlyList<PathToken> path, CreatorWorkbenchNodeKind kind)
    {
        if (path.Count == 0)
        {
            return;
        }

        object current = root;
        for (int index = 0; index < path.Count; index++)
        {
            var token = path[index];
            var isLast = index == path.Count - 1;

            if (token.IsIndex)
            {
                if (current is not List<object?> list)
                {
                    return;
                }

                EnsureListSize(list, token.Index + 1);
                if (isLast)
                {
                    list[token.Index] ??= kind == CreatorWorkbenchNodeKind.Array
                        ? new List<object?>()
                        : new Dictionary<string, object?>(StringComparer.Ordinal);
                    return;
                }

                list[token.Index] ??= path[index + 1].IsIndex
                    ? new List<object?>()
                    : new Dictionary<string, object?>(StringComparer.Ordinal);
                current = list[token.Index]!;
                continue;
            }

            if (current is not Dictionary<string, object?> dictionary)
            {
                return;
            }

            if (isLast)
            {
                dictionary[token.Name] ??= kind == CreatorWorkbenchNodeKind.Array
                    ? new List<object?>()
                    : new Dictionary<string, object?>(StringComparer.Ordinal);
                return;
            }

            dictionary[token.Name] ??= path[index + 1].IsIndex
                ? new List<object?>()
                : new Dictionary<string, object?>(StringComparer.Ordinal);
            current = dictionary[token.Name]!;
        }
    }

    private static void SetScalarValue(object root, IReadOnlyList<PathToken> path, object? value)
    {
        if (path.Count == 0)
        {
            return;
        }

        object current = root;
        for (int index = 0; index < path.Count - 1; index++)
        {
            var token = path[index];
            if (token.IsIndex)
            {
                var list = (List<object?>)current;
                EnsureListSize(list, token.Index + 1);
                list[token.Index] ??= path[index + 1].IsIndex
                    ? new List<object?>()
                    : new Dictionary<string, object?>(StringComparer.Ordinal);
                current = list[token.Index]!;
                continue;
            }

            var dictionary = (Dictionary<string, object?>)current;
            dictionary[token.Name] ??= path[index + 1].IsIndex
                ? new List<object?>()
                : new Dictionary<string, object?>(StringComparer.Ordinal);
            current = dictionary[token.Name]!;
        }

        var last = path[^1];
        if (last.IsIndex)
        {
            var list = (List<object?>)current;
            EnsureListSize(list, last.Index + 1);
            list[last.Index] = value;
            return;
        }

        var dict = (Dictionary<string, object?>)current;
        dict[last.Name] = value;
    }

    private static void EnsureListSize(List<object?> list, int desiredSize)
    {
        while (list.Count < desiredSize)
        {
            list.Add(null);
        }
    }

    private static JToken ToJsonToken(object? value)
    {
        return value switch
        {
            null => JValue.CreateNull(),
            Dictionary<string, object?> dictionary => new JObject(dictionary.Select(pair => new JProperty(pair.Key, ToJsonToken(pair.Value)))),
            List<object?> list => new JArray(list.Select(ToJsonToken)),
            _ => JToken.FromObject(value)
        };
    }

    private string? TryResolveLatestSnapshotFile(string? workspaceId, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            return null;
        }

        var backupRoot = Path.Combine(_launcherService.BackupsPath, SanitizeWorkspaceDirectoryName(workspaceId));
        if (!Directory.Exists(backupRoot))
        {
            return null;
        }

        try
        {
            var latestSnapshot = new DirectoryInfo(backupRoot)
                .EnumerateDirectories()
                .OrderByDescending(directory => directory.LastWriteTimeUtc)
                .FirstOrDefault();

            if (latestSnapshot == null)
            {
                return null;
            }

            var candidate = Path.Combine(latestSnapshot.FullName, relativePath);
            return File.Exists(candidate) ? candidate : null;
        }
        catch
        {
            return null;
        }
    }

    private static (string ArchivePath, string EntryPath)? TryResolveLatestExportArchive(string workspacePath, string relativePath)
    {
        var exportsPath = Path.Combine(workspacePath, "exports");
        if (!Directory.Exists(exportsPath))
        {
            return null;
        }

        try
        {
            var normalizedRelativePath = relativePath.Replace('\\', '/');
            foreach (var exportFile in new DirectoryInfo(exportsPath).EnumerateFiles("*.voidpack").OrderByDescending(file => file.LastWriteTimeUtc))
            {
                using var archive = ZipFile.OpenRead(exportFile.FullName);
                var entry = archive.GetEntry(normalizedRelativePath);
                if (entry != null)
                {
                    return (exportFile.FullName, normalizedRelativePath);
                }
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static string? TryResolveDefaultCounterpart(string workspacePath, string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/');
        if (normalized.StartsWith("config/", StringComparison.OrdinalIgnoreCase))
        {
            var counterpart = Path.Combine(workspacePath, "defaultconfigs", normalized["config/".Length..]);
            return File.Exists(counterpart) ? counterpart : null;
        }

        if (normalized.StartsWith("defaultconfigs/", StringComparison.OrdinalIgnoreCase))
        {
            var counterpart = Path.Combine(workspacePath, "config", normalized["defaultconfigs/".Length..]);
            return File.Exists(counterpart) ? counterpart : null;
        }

        return null;
    }

    private static CreatorWorkbenchValidationIssue CreateIssue(CreatorWorkbenchIssueSeverity severity, string scope, string message)
    {
        return new CreatorWorkbenchValidationIssue
        {
            Severity = severity,
            Scope = scope,
            Message = message
        };
    }

    private static object? NormalizeStructuredValue(object? value)
    {
        if (value == null)
        {
            return null;
        }

        if (value is string || value is bool || value is byte || value is sbyte || value is short || value is ushort ||
            value is int || value is uint || value is long || value is ulong || value is float || value is double || value is decimal)
        {
            return value;
        }

        if (value is DateTime or DateTimeOffset or TimeSpan)
        {
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        if (value is IDictionary<string, object?> stringDictionary)
        {
            return stringDictionary.ToDictionary(pair => pair.Key, pair => NormalizeStructuredValue(pair.Value), StringComparer.Ordinal);
        }

        if (value is IDictionary dictionary)
        {
            var normalizedDictionary = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (DictionaryEntry entry in dictionary)
            {
                normalizedDictionary[Convert.ToString(entry.Key, CultureInfo.InvariantCulture) ?? string.Empty] = NormalizeStructuredValue(entry.Value);
            }

            return normalizedDictionary;
        }

        if (value is IEnumerable enumerable and not string)
        {
            var list = new List<object?>();
            foreach (var item in enumerable)
            {
                list.Add(NormalizeStructuredValue(item));
            }

            return list;
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    private static CreatorWorkbenchValueKind DetermineValueKind(object? value)
    {
        return value switch
        {
            null => CreatorWorkbenchValueKind.Null,
            bool => CreatorWorkbenchValueKind.Boolean,
            byte or sbyte or short or ushort or int or uint or long or ulong => CreatorWorkbenchValueKind.Integer,
            float or double or decimal => CreatorWorkbenchValueKind.Decimal,
            _ => CreatorWorkbenchValueKind.String
        };
    }

    private static string FormatScalar(object? value, CreatorWorkbenchValueKind kind)
    {
        return kind switch
        {
            CreatorWorkbenchValueKind.Null => "null",
            CreatorWorkbenchValueKind.Boolean => value is bool boolValue && boolValue ? "true" : "false",
            CreatorWorkbenchValueKind.Integer or CreatorWorkbenchValueKind.Decimal => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };
    }

    private static string BuildValueTypeLabel(CreatorWorkbenchValueKind kind)
    {
        return kind switch
        {
            CreatorWorkbenchValueKind.Integer => "Int",
            CreatorWorkbenchValueKind.Decimal => "Decimal",
            CreatorWorkbenchValueKind.Boolean => "Bool",
            CreatorWorkbenchValueKind.Null => "Null",
            _ => "String"
        };
    }

    private static string EnsureTrailingNewLine(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.EndsWith(Environment.NewLine, StringComparison.Ordinal)
            ? value
            : value + Environment.NewLine;
    }

    private static string[] SplitLines(string content)
    {
        return (content ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..Math.Max(0, maxLength - 1)] + "...";
    }

    private static List<PathToken> CopyPath(IReadOnlyList<PathToken> pathTokens)
    {
        return pathTokens.Select(token => token.IsIndex ? PathToken.ArrayIndex(token.Index) : PathToken.Property(token.Name)).ToList();
    }

    private static string BuildDisplayPath(IReadOnlyList<PathToken> pathTokens)
    {
        if (pathTokens.Count == 0)
        {
            return "root";
        }

        var builder = new StringBuilder();
        foreach (var token in pathTokens)
        {
            if (token.IsIndex)
            {
                builder.Append('[').Append(token.Index).Append(']');
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append('.');
            }

            builder.Append(token.Name);
        }

        return builder.ToString();
    }

    private static string EncodePath(IReadOnlyList<PathToken> pathTokens)
    {
        return string.Join('/', pathTokens.Select(token => token.IsIndex
            ? $"i:{token.Index}"
            : $"p:{Uri.EscapeDataString(token.Name)}"));
    }

    private static List<PathToken> DecodePath(string pathKey)
    {
        var tokens = new List<PathToken>();
        if (string.IsNullOrWhiteSpace(pathKey))
        {
            return tokens;
        }

        foreach (var part in pathKey.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.StartsWith("i:", StringComparison.Ordinal) && int.TryParse(part[2..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
            {
                tokens.Add(PathToken.ArrayIndex(index));
            }
            else if (part.StartsWith("p:", StringComparison.Ordinal))
            {
                tokens.Add(PathToken.Property(Uri.UnescapeDataString(part[2..])));
            }
        }

        return tokens;
    }

    private static string SanitizeWorkspaceDirectoryName(string workspaceId)
    {
        var source = string.IsNullOrWhiteSpace(workspaceId) ? "voidcraft-instance" : workspaceId.Trim();
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitizedChars = source
            .Select(character => invalidChars.Contains(character) ? '-' : character)
            .ToArray();

        var sanitized = new string(sanitizedChars)
            .Replace(' ', '-')
            .Trim('.', ' ', '-', '_');

        return string.IsNullOrWhiteSpace(sanitized) ? "voidcraft-instance" : sanitized;
    }

    private readonly record struct PathToken(bool IsIndex, string Name, int Index)
    {
        public static PathToken Property(string name) => new(false, name, -1);

        public static PathToken ArrayIndex(int index) => new(true, string.Empty, index);
    }
}