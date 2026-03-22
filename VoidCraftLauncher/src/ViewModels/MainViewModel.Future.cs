using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VoidCraftLauncher.Models;
using VoidCraftLauncher.Services;

namespace VoidCraftLauncher.ViewModels;

public partial class MainViewModel
{
    private const string FutureRoadmapRawUrl = "https://raw.githubusercontent.com/venom74cz/VOID-CRAFT.EU-Launcher-remake/main/future.md";
    private const string FutureRoadmapSourceUrl = "https://github.com/venom74cz/VOID-CRAFT.EU-Launcher-remake/blob/main/future.md";
    private static readonly TimeSpan FutureRoadmapRefreshWindow = TimeSpan.FromMinutes(15);

    public ObservableCollection<FutureRoadmapSignal> FutureRoadmapSignals { get; } = new();

    public ObservableCollection<FutureRoadmapSection> FutureRoadmapSections { get; } = new();

    [ObservableProperty]
    private bool _isFutureRoadmapLoading;

    [ObservableProperty]
    private string _futureRoadmapSourceStatus = "Čeká na načtení z GitHubu";

    [ObservableProperty]
    private string _futureRoadmapLastUpdated = "Ještě nenačteno";

    [ObservableProperty]
    private string _futureRoadmapHeadline = "VOID Future Roadmap";

    [ObservableProperty]
    private string _futureRoadmapIntro = "Kurátorovaná post-release roadmapa z GitHubu. V launcheru se převádí do čitelných produktových bloků, aby bylo na první pohled jasné, co je severka, co jsou guardraily a které směry mají nejvyšší dopad.";

    private DateTime _futureRoadmapFetchedAtUtc = DateTime.MinValue;

    public bool HasFutureRoadmapSignals => FutureRoadmapSignals.Count > 0;

    public bool HasFutureRoadmapSections => FutureRoadmapSections.Count > 0;

    public bool ShouldShowFutureRoadmapEmptyState => !IsFutureRoadmapLoading && !HasFutureRoadmapSections;

    public int FutureRoadmapSectionCount => FutureRoadmapSections.Count;

    public int FutureRoadmapEntryCount => FutureRoadmapSections.Sum(section => section.Entries.Count);

    public string FutureRoadmapStatsLabel => $"{FutureRoadmapSectionCount} bloků · {FutureRoadmapEntryCount} konkrétních návrhů";

    public string FutureRoadmapEmptyTitle => "Future roadmap se nepodařilo načíst";

    public string FutureRoadmapEmptySubtitle => "GitHub zdroj ani lokální cache teď nevrátily použitelný obsah. Zkus obnovit načtení za chvíli.";

    partial void OnIsFutureRoadmapLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(ShouldShowFutureRoadmapEmptyState));
    }

    [RelayCommand]
    private async Task RefreshFutureRoadmap()
    {
        await LoadFutureRoadmapAsync(true);
    }

    [RelayCommand]
    private void OpenFutureRoadmapSource()
    {
        OpenUrl(FutureRoadmapSourceUrl);
    }

    private async Task LoadFutureRoadmapAsync(bool forceRefresh = false)
    {
        if (IsFutureRoadmapLoading)
            return;

        if (!forceRefresh && HasFutureRoadmapSections && DateTime.UtcNow - _futureRoadmapFetchedAtUtc < FutureRoadmapRefreshWindow)
            return;

        IsFutureRoadmapLoading = true;
        try
        {
            var markdown = string.Empty;
            var sourceStatus = string.Empty;
            var fetchedAtUtc = DateTime.UtcNow;

            try
            {
                markdown = await _httpClient.GetStringAsync(FutureRoadmapRawUrl).WaitAsync(TimeSpan.FromSeconds(8));
                sourceStatus = "Načteno živě z GitHubu";
                PersistFutureRoadmapCache(markdown, fetchedAtUtc);
            }
            catch (Exception ex)
            {
                LogService.Error("Future roadmap live fetch failed", ex);
                var cached = LoadFutureRoadmapCache();
                if (cached != null && !string.IsNullOrWhiteSpace(cached.Markdown))
                {
                    markdown = cached.Markdown;
                    fetchedAtUtc = cached.FetchedAtUtc;
                    sourceStatus = "GitHub nedostupný, zobrazuju poslední uloženou cache";
                }
                else
                {
                    var workspaceFallback = TryLoadFutureRoadmapFromWorkspace();
                    if (!string.IsNullOrWhiteSpace(workspaceFallback))
                    {
                        markdown = workspaceFallback;
                        sourceStatus = "GitHub nedostupný, zobrazuju lokální workspace variantu";
                    }
                }
            }

            var parsedDocument = string.IsNullOrWhiteSpace(markdown)
                ? new FutureRoadmapDocumentData()
                : ParseFutureRoadmap(markdown);

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                FutureRoadmapSignals.Clear();
                foreach (var signal in parsedDocument.Signals)
                    FutureRoadmapSignals.Add(signal);

                FutureRoadmapSections.Clear();
                foreach (var section in parsedDocument.Sections)
                    FutureRoadmapSections.Add(section);

                _futureRoadmapFetchedAtUtc = fetchedAtUtc;
                FutureRoadmapSourceStatus = string.IsNullOrWhiteSpace(sourceStatus) ? "Zdroj není dostupný" : sourceStatus;
                FutureRoadmapHeadline = string.IsNullOrWhiteSpace(parsedDocument.Headline)
                    ? "VOID Future Roadmap"
                    : parsedDocument.Headline;
                FutureRoadmapIntro = string.IsNullOrWhiteSpace(parsedDocument.Intro)
                    ? "Kurátorovaná post-release roadmapa z GitHubu. V launcheru se převádí do čitelných produktových bloků, aby bylo na první pohled jasné, co je severka, co jsou guardraily a které směry mají nejvyšší dopad."
                    : parsedDocument.Intro;
                FutureRoadmapLastUpdated = parsedDocument.Sections.Count == 0
                    ? "Bez načteného obsahu"
                    : $"Aktualizováno {fetchedAtUtc.ToLocalTime():dd.MM.yyyy HH:mm}";

                OnPropertyChanged(nameof(HasFutureRoadmapSignals));
                OnPropertyChanged(nameof(HasFutureRoadmapSections));
                OnPropertyChanged(nameof(ShouldShowFutureRoadmapEmptyState));
                OnPropertyChanged(nameof(FutureRoadmapSectionCount));
                OnPropertyChanged(nameof(FutureRoadmapEntryCount));
                OnPropertyChanged(nameof(FutureRoadmapStatsLabel));
            });
        }
        finally
        {
            IsFutureRoadmapLoading = false;
        }
    }

    private FutureRoadmapDocumentData ParseFutureRoadmap(string markdown)
    {
        var document = new FutureRoadmapDocumentData();
        FutureRoadmapSection? currentSection = null;
        FutureRoadmapEntry? currentEntry = null;
        FutureRoadmapSignal? currentSignal = null;
        var introLines = new List<string>();
        var signalLines = new List<string>();
        var sectionBody = new List<string>();
        var descriptionLines = new List<string>();
        var implementationLines = new List<string>();
        var whyItMattersLines = new List<string>();
        var activeEntryField = "description";
        var inCodeBlock = false;
        var beforeFirstSection = true;

        void ResetEntryBuffers()
        {
            descriptionLines.Clear();
            implementationLines.Clear();
            whyItMattersLines.Clear();
            activeEntryField = "description";
        }

        void FinalizeSignal()
        {
            if (currentSignal == null)
                return;

            currentSignal.Description = JoinMarkdownLines(signalLines);
            if (!string.IsNullOrWhiteSpace(currentSignal.Title) || !string.IsNullOrWhiteSpace(currentSignal.Description))
                document.Signals.Add(currentSignal);

            currentSignal = null;
            signalLines.Clear();
        }

        void FinalizeEntry()
        {
            if (currentEntry == null || currentSection == null)
                return;

            currentEntry.Description = JoinMarkdownLines(descriptionLines);
            currentEntry.Implementation = JoinMarkdownLines(implementationLines);
            currentEntry.WhyItMatters = JoinMarkdownLines(whyItMattersLines);
            currentSection.Entries.Add(currentEntry);
            currentEntry = null;
            ResetEntryBuffers();
        }

        void FinalizeSection()
        {
            if (currentSection == null)
                return;

            FinalizeEntry();
            currentSection.Body = JoinMarkdownLines(sectionBody);
            currentSection.HasEntries = currentSection.Entries.Count > 0;
            document.Sections.Add(currentSection);
            currentSection = null;
            sectionBody.Clear();
        }

        foreach (var rawLine in markdown.Split('\n'))
        {
            var line = rawLine.Replace("\r", string.Empty);
            var trimmed = line.Trim();

            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                inCodeBlock = !inCodeBlock;
                continue;
            }

            if (inCodeBlock)
                continue;

            if (beforeFirstSection && trimmed.StartsWith("# ", StringComparison.Ordinal))
            {
                document.Headline = NormalizeMarkdownLine(trimmed[2..].Trim());
                continue;
            }

            if (trimmed.StartsWith("## ", StringComparison.Ordinal))
            {
                FinalizeSignal();
                beforeFirstSection = false;
                FinalizeSection();
                currentSection = CreateFutureRoadmapSection(trimmed[3..].Trim());
                continue;
            }

            if (beforeFirstSection)
            {
                if (trimmed == "---")
                    continue;

                var introContent = StripMarkdownQuote(trimmed);
                if (string.IsNullOrWhiteSpace(introContent))
                {
                    AppendMarkdownLine(currentSignal == null ? introLines : signalLines, string.Empty);
                    continue;
                }

                if (introContent.StartsWith("[!", StringComparison.OrdinalIgnoreCase))
                {
                    FinalizeSignal();
                    currentSignal = CreateFutureRoadmapSignal(introContent);
                    continue;
                }

                if (introContent.StartsWith("- [", StringComparison.Ordinal))
                    continue;

                AppendMarkdownLine(currentSignal == null ? introLines : signalLines, NormalizeMarkdownLine(introContent));
                continue;
            }

            if (currentSection == null)
                continue;

            if (trimmed.StartsWith("### ", StringComparison.Ordinal))
            {
                FinalizeEntry();
                currentEntry = CreateFutureRoadmapEntry(trimmed[4..].Trim(), currentSection);
                continue;
            }

            if (trimmed == "---")
                continue;

            var content = StripMarkdownQuote(trimmed);
            if (string.IsNullOrWhiteSpace(content))
            {
                AppendMarkdownLine(currentEntry == null ? sectionBody : GetTargetBuffer(activeEntryField), string.Empty);
                continue;
            }

            if (content.StartsWith("[!", StringComparison.OrdinalIgnoreCase))
                continue;

            if (content.StartsWith("**Popis**", StringComparison.OrdinalIgnoreCase))
            {
                activeEntryField = "description";
                AppendFieldTail(content, "**Popis**", descriptionLines);
                continue;
            }

            if (content.StartsWith("**Mozna implementace**", StringComparison.OrdinalIgnoreCase))
            {
                activeEntryField = "implementation";
                AppendFieldTail(content, "**Mozna implementace**", implementationLines);
                continue;
            }

            if (content.StartsWith("**Proc to dava smysl**", StringComparison.OrdinalIgnoreCase))
            {
                activeEntryField = "why";
                AppendFieldTail(content, "**Proc to dava smysl**", whyItMattersLines);
                continue;
            }

            AppendMarkdownLine(currentEntry == null ? sectionBody : GetTargetBuffer(activeEntryField), NormalizeMarkdownLine(content));
        }

        FinalizeSignal();
        FinalizeSection();
        document.Intro = JoinMarkdownLines(introLines);
        return document;

        List<string> GetTargetBuffer(string field) => field switch
        {
            "implementation" => implementationLines,
            "why" => whyItMattersLines,
            _ => descriptionLines
        };
    }

    private static FutureRoadmapSection CreateFutureRoadmapSection(string title)
    {
        var normalizedTitle = NormalizeMarkdownLine(title);
        var comparison = NormalizeComparisonValue(normalizedTitle);

        return comparison switch
        {
            var value when value.Contains("priorita a") => new FutureRoadmapSection
            {
                Title = normalizedTitle,
                AccentBrush = "#62D69A",
                SurfaceBrush = "#101B16",
                BorderBrush = "#28513D",
                BadgeLabel = "CORE"
            },
            var value when value.Contains("priorita b") => new FutureRoadmapSection
            {
                Title = normalizedTitle,
                AccentBrush = "#5FA8FF",
                SurfaceBrush = "#111A28",
                BorderBrush = "#27486D",
                BadgeLabel = "SOCIAL"
            },
            var value when value.Contains("priorita c") => new FutureRoadmapSection
            {
                Title = normalizedTitle,
                AccentBrush = "#8FD3FF",
                SurfaceBrush = "#101A22",
                BorderBrush = "#355A73",
                BadgeLabel = "UTILITY"
            },
            var value when value.Contains("priorita d") => new FutureRoadmapSection
            {
                Title = normalizedTitle,
                AccentBrush = "#F5C05D",
                SurfaceBrush = "#241A0F",
                BorderBrush = "#6C5130",
                BadgeLabel = "LAB"
            },
            var value when value.Contains("priorita e") => new FutureRoadmapSection
            {
                Title = normalizedTitle,
                AccentBrush = "#FF8A65",
                SurfaceBrush = "#261714",
                BorderBrush = "#704336",
                BadgeLabel = "QUALITY"
            },
            var value when value.Contains("mantinely") => new FutureRoadmapSection
            {
                Title = normalizedTitle,
                AccentBrush = "#FFB86B",
                SurfaceBrush = "#211A11",
                BorderBrush = "#6E4A28",
                BadgeLabel = "RULES"
            },
            var value when value.Contains("jak roadmapu cist") || value.Contains("jak tenhle dokument cist") => new FutureRoadmapSection
            {
                Title = normalizedTitle,
                AccentBrush = "#7CD9FF",
                SurfaceBrush = "#101C25",
                BorderBrush = "#305C71",
                BadgeLabel = "FLOW"
            },
            var value when value.Contains("co tenhle dokument resi") || value.Contains("proc tenhle dokument existuje") => new FutureRoadmapSection
            {
                Title = normalizedTitle,
                AccentBrush = "#B79CFF",
                SurfaceBrush = "#171427",
                BorderBrush = "#4A4180",
                BadgeLabel = "WHY"
            },
            var value when value.Contains("top set") || value.Contains("prioritizoval") => new FutureRoadmapSection
            {
                Title = normalizedTitle,
                AccentBrush = "#7CE0B0",
                SurfaceBrush = "#111D17",
                BorderBrush = "#355B46",
                BadgeLabel = "TOP"
            },
            var value when value.Contains("nedelat") || value.Contains("moc brzy") || value.Contains("prilis brzy") => new FutureRoadmapSection
            {
                Title = normalizedTitle,
                AccentBrush = "#D8A24B",
                SurfaceBrush = "#241F16",
                BorderBrush = "#6A5531",
                BadgeLabel = "RISK"
            },
            var value when value.Contains("backlog") || value.Contains("format") => new FutureRoadmapSection
            {
                Title = normalizedTitle,
                AccentBrush = "#8BD7C7",
                SurfaceBrush = "#10211F",
                BorderBrush = "#2F5F59",
                BadgeLabel = "FORMAT"
            },
            var value when value.Contains("zaver") => new FutureRoadmapSection
            {
                Title = normalizedTitle,
                AccentBrush = "#D5C6FF",
                SurfaceBrush = "#17152A",
                BorderBrush = "#4F4780",
                BadgeLabel = "VISION"
            },
            _ => new FutureRoadmapSection
            {
                Title = normalizedTitle,
                AccentBrush = "#7C6FFF",
                SurfaceBrush = "#141420",
                BorderBrush = "#3A3A55",
                BadgeLabel = "GUIDE"
            }
        };
    }

    private static FutureRoadmapEntry CreateFutureRoadmapEntry(string title, FutureRoadmapSection section)
    {
        var normalizedTitle = NormalizeMarkdownLine(title);
        var titleMatch = Regex.Match(normalizedTitle, @"^(?<index>\d+[A-Za-z]?)\.\s+(?<title>.+)$");

        return new FutureRoadmapEntry
        {
            IndexLabel = titleMatch.Success ? titleMatch.Groups["index"].Value : string.Empty,
            Title = titleMatch.Success ? titleMatch.Groups["title"].Value : normalizedTitle,
            AccentBrush = section.AccentBrush,
            SurfaceBrush = section.SurfaceBrush,
            BorderBrush = section.BorderBrush
        };
    }

    private static FutureRoadmapSignal CreateFutureRoadmapSignal(string content)
    {
        var match = Regex.Match(content, @"^\[!(?<type>[^\]]+)\]\s*(?<title>.*)$");
        var type = match.Success ? NormalizeComparisonValue(match.Groups["type"].Value) : "note";
        var title = match.Success ? NormalizeMarkdownLine(match.Groups["title"].Value) : NormalizeMarkdownLine(content);

        return type switch
        {
            "important" => new FutureRoadmapSignal
            {
                Label = "NORTH STAR",
                Title = string.IsNullOrWhiteSpace(title) ? "Směr produktu" : title,
                AccentBrush = "#A997FF",
                SurfaceBrush = "#161429",
                BorderBrush = "#4A4183"
            },
            "tip" => new FutureRoadmapSignal
            {
                Label = "PLAYBOOK",
                Title = string.IsNullOrWhiteSpace(title) ? "Jak roadmapu číst" : title,
                AccentBrush = "#78D9FF",
                SurfaceBrush = "#10202A",
                BorderBrush = "#315D74"
            },
            "warning" => new FutureRoadmapSignal
            {
                Label = "GUARDRAIL",
                Title = string.IsNullOrWhiteSpace(title) ? "Implementační mantinel" : title,
                AccentBrush = "#FFB86B",
                SurfaceBrush = "#23190F",
                BorderBrush = "#724D2A"
            },
            "success" => new FutureRoadmapSignal
            {
                Label = "BET",
                Title = string.IsNullOrWhiteSpace(title) ? "Silný směr" : title,
                AccentBrush = "#76E0B0",
                SurfaceBrush = "#112019",
                BorderBrush = "#345E47"
            },
            "quote" => new FutureRoadmapSignal
            {
                Label = "INTENT",
                Title = string.IsNullOrWhiteSpace(title) ? "Produktový záměr" : title,
                AccentBrush = "#D2C4FF",
                SurfaceBrush = "#17152A",
                BorderBrush = "#4C467A"
            },
            _ => new FutureRoadmapSignal
            {
                Label = "CONTEXT",
                Title = string.IsNullOrWhiteSpace(title) ? "Kontext" : title,
                AccentBrush = "#7C6FFF",
                SurfaceBrush = "#141420",
                BorderBrush = "#3A3A55"
            }
        };
    }

    private static string StripMarkdownQuote(string line)
    {
        return line.StartsWith(">", StringComparison.Ordinal)
            ? line.TrimStart('>', ' ')
            : line;
    }

    private static void AppendFieldTail(string content, string fieldLabel, List<string> target)
    {
        var tail = content[fieldLabel.Length..].Trim().TrimStart(':');
        if (!string.IsNullOrWhiteSpace(tail))
            AppendMarkdownLine(target, NormalizeMarkdownLine(tail));
    }

    private static void AppendMarkdownLine(List<string> target, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (target.Count > 0 && !string.IsNullOrWhiteSpace(target[^1]))
                target.Add(string.Empty);
            return;
        }

        target.Add(value);
    }

    private static string JoinMarkdownLines(IEnumerable<string> lines)
    {
        var filteredLines = lines.ToList();
        while (filteredLines.Count > 0 && string.IsNullOrWhiteSpace(filteredLines[0]))
            filteredLines.RemoveAt(0);
        while (filteredLines.Count > 0 && string.IsNullOrWhiteSpace(filteredLines[^1]))
            filteredLines.RemoveAt(filteredLines.Count - 1);

        return string.Join(Environment.NewLine, filteredLines);
    }

    private static string NormalizeMarkdownLine(string value)
    {
        var normalized = value.Trim();
        normalized = Regex.Replace(normalized, @"\[(.*?)\]\((.*?)\)", "$1");
        normalized = normalized.Replace("**", string.Empty).Replace("`", string.Empty);
        normalized = normalized.StartsWith("- ", StringComparison.Ordinal)
            ? $"• {normalized[2..].Trim()}"
            : normalized;
        return Regex.Replace(normalized, @"\s+", " ").Trim();
    }

    private static string NormalizeComparisonValue(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }

    private void PersistFutureRoadmapCache(string markdown, DateTime fetchedAtUtc)
    {
        try
        {
            var payload = new FutureRoadmapCache
            {
                FetchedAtUtc = fetchedAtUtc,
                Markdown = markdown
            };
            File.WriteAllText(GetFutureRoadmapCachePath(), JsonSerializer.Serialize(payload));
        }
        catch (Exception ex)
        {
            LogService.Error("Future roadmap cache persist failed", ex);
        }
    }

    private FutureRoadmapCache? LoadFutureRoadmapCache()
    {
        try
        {
            var cachePath = GetFutureRoadmapCachePath();
            if (!File.Exists(cachePath))
                return null;

            return JsonSerializer.Deserialize<FutureRoadmapCache>(File.ReadAllText(cachePath));
        }
        catch (Exception ex)
        {
            LogService.Error("Future roadmap cache load failed", ex);
            return null;
        }
    }

    private string GetFutureRoadmapCachePath()
    {
        Directory.CreateDirectory(_launcherService.BasePath);
        return Path.Combine(_launcherService.BasePath, "future_roadmap_cache.json");
    }

    private static string? TryLoadFutureRoadmapFromWorkspace()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDirectory, "future.md"),
            Path.Combine(baseDirectory, "..", "future.md"),
            Path.Combine(baseDirectory, "..", "..", "future.md"),
            Path.Combine(baseDirectory, "..", "..", "..", "future.md"),
            Path.Combine(baseDirectory, "..", "..", "..", "..", "future.md")
        };

        foreach (var candidate in candidates)
        {
            try
            {
                var fullPath = Path.GetFullPath(candidate);
                if (File.Exists(fullPath))
                    return File.ReadAllText(fullPath);
            }
            catch
            {
                // Ignore fallback probing failures.
            }
        }

        return null;
    }

    private sealed class FutureRoadmapCache
    {
        public DateTime FetchedAtUtc { get; set; }

        public string Markdown { get; set; } = string.Empty;
    }

    private sealed class FutureRoadmapDocumentData
    {
        public string Headline { get; set; } = string.Empty;

        public string Intro { get; set; } = string.Empty;

        public List<FutureRoadmapSignal> Signals { get; } = new();

        public List<FutureRoadmapSection> Sections { get; } = new();
    }
}