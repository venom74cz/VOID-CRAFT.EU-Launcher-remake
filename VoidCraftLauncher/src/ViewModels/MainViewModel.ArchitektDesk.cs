using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VoidCraftLauncher.Models;
using VoidCraftLauncher.Services;
using Avalonia.Platform.Storage;

namespace VoidCraftLauncher.ViewModels;

/// <summary>
/// Creator Copilot Desk — AI chat assistant powered by Anthropic Claude.
/// Injects current workspace context (manifest, active file, git state) into the system prompt
/// so Claude has full creator context without the user having to paste it manually.
/// </summary>
public partial class MainViewModel
{
    // ── State ────────────────────────────────────────────────────────────────

    public ObservableCollection<ArchitektMessage> ArchitektHistory { get; } = new();

    [ObservableProperty]
    private string _architektInput = string.Empty;

    [ObservableProperty]
    private bool _isArchitektStreaming;

    [ObservableProperty]
    private string _architektStatus = string.Empty;
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CreatorActiveScopeSummary))]
    [NotifyPropertyChangedFor(nameof(CreatorDockSubtitle))]
    private bool _isArchitektAgentMode = true;

    private CancellationTokenSource? _architektStreamCts;

    // ── Computed ─────────────────────────────────────────────────────────────

    public bool HasArchitektHistory => ArchitektHistory.Count > 0;

    public bool CanSendArchitektMessage =>
        SelectedArchitektProfile != null &&
        !IsArchitektStreaming &&
        !string.IsNullOrWhiteSpace(ArchitektInput);

    public string ArchitektPlaceholderText => SelectedArchitektProfile != null
        ? "Zeptej se (lokální / cloud AI)..."
        : "Nejdřív vyber nebo nastav AI v Nastavení";

    public string ArchitektSendLabel => IsArchitektStreaming ? "Přemýšlím..." : "Odeslat";

    // ── Init ─────────────────────────────────────────────────────────────

    // ── Property change wiring ────────────────────────────────────────────────

    partial void OnArchitektInputChanged(string value)
    {
        OnPropertyChanged(nameof(CanSendArchitektMessage));
    }

    partial void OnIsArchitektStreamingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanSendArchitektMessage));
        OnPropertyChanged(nameof(ArchitektSendLabel));
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task AttachFileToArchitekt()
    {
        if (MainWindow == null || !HasCreatorWorkspaceContext) return;

        try
        {
            var topLevel = Avalonia.Controls.TopLevel.GetTopLevel(MainWindow);
            if (topLevel == null) return;

            var workspacePath = CreatorWorkspaceContext.WorkspacePath;
            if (string.IsNullOrWhiteSpace(workspacePath) || !Directory.Exists(workspacePath)) return;

            var results = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Vyber soubor pro kontext AI",
                AllowMultiple = false,
                SuggestedStartLocation = await topLevel.StorageProvider.TryGetFolderFromPathAsync(new Uri(workspacePath))
            });

            if (results.Count > 0)
            {
                var file = results[0];
                var fullPath = file.Path.LocalPath;
                
                // Vytvoříme modelový objekt souboru pro workbench
                var workbenchFile = new CreatorWorkbenchFile
                {
                    FullPath = fullPath,
                    RelativePath = Path.GetRelativePath(workspacePath, fullPath),
                    Category = "Attached",
                    SizeBytes = new FileInfo(fullPath).Length
                };

                // Nastavením této property dojde k automatickému načtení obsahu v partial metodě OnSelectedCreatorWorkbenchFileChanged (MainViewModel.Streaming.cs)
                SelectedCreatorWorkbenchFile = workbenchFile;
                
                ArchitektStatus = $"Připojen soubor: {workbenchFile.RelativePath}";
                ShowToast("ARCHITEKT", $"Soubor {workbenchFile.RelativePath} byl přidán do kontextu.", VoidCraftLauncher.Models.ToastSeverity.Success);
            }
        }
        catch (Exception ex)
        {
            LogService.Error("Failed to attach file to Architekt", ex);
            ShowToast("ARCHITEKT", "Nepodařilo se vybrat soubor.", VoidCraftLauncher.Models.ToastSeverity.Error);
        }
    }

    [RelayCommand]
    private async Task SendArchitektMessage()
    {
        var input = ArchitektInput?.Trim();
        if (string.IsNullOrWhiteSpace(input) || IsArchitektStreaming || SelectedArchitektProfile == null)
            return;

        ArchitektInput = string.Empty;

        // Add user message
        var userMsg = new ArchitektMessage { Role = "user", Content = input };
        ArchitektHistory.Add(userMsg);

        // Add empty assistant placeholder
        var assistantMsg = new ArchitektMessage { Role = "assistant", Content = string.Empty };
        ArchitektHistory.Add(assistantMsg);

        IsArchitektStreaming = true;
        ArchitektStatus = "Přemýšlím...";

        // Pokud se právě načítá soubor (např. po kliknutí na +), počkáme chvíli na dokončení, 
        // aby prompt obsahoval aktuální obsah souboru.
        int waitAttempts = 0;
        while (IsCreatorWorkbenchLoading && waitAttempts < 20)
        {
            await Task.Delay(100);
            waitAttempts++;
            ArchitektStatus = $"Načítám kontext ({waitAttempts * 5}%)...";
        }
        
        ArchitektStatus = "Přemýšlím...";

        _architektStreamCts?.Cancel();
        _architektStreamCts?.Dispose();
        _architektStreamCts = new CancellationTokenSource();
        var cts = _architektStreamCts;

        string? systemPrompt = null;
        try
        {
            systemPrompt = BuildArchitektSystemPrompt();

            // --- ULTRA-STABILNÍ HISTORIE ---
            // 1. Vyčistíme statusy a zajistíme neprázdný obsah (placeholder)
            var fullHistoryCleaned = ArchitektHistory
                .Take(ArchitektHistory.Count - 1)
                .Select(m => 
                {
                    string cleaned = string.Join("\n", (m.Content ?? "").Split('\n')
                        .Where(line => !line.Trim().StartsWith("> *"))).Trim();
                        
                    return new ArchitektMessage 
                    { 
                        Role = m.Role, 
                        Content = (m.Role == "assistant" && string.IsNullOrWhiteSpace(cleaned)) 
                                   ? "(AI analyzovala projekt pomocí nástrojů)" 
                                   : cleaned
                    };
                })
                .Where(m => !string.IsNullOrWhiteSpace(m.Content))
                .ToList();

            // 2. Vynutíme střídání User-Assistant (Chat Protocol Safety)
            // A omezíme historii na posledních 4 zpráv (2 turns) pro Token Safety
            var alternatingHistory = new List<ArchitektMessage>();
            string lastRole = "";
            foreach (var m in fullHistoryCleaned.AsEnumerable().Reverse().Take(4).Reverse())
            {
                if (m.Role != lastRole)
                {
                    alternatingHistory.Add(m);
                    lastRole = m.Role;
                }
            }
            var historyForApi = alternatingHistory;

            LogService.Log($"ARCHITEKT: History stabilized. Used={historyForApi.Count}/{fullHistoryCleaned.Count} turns.");

            LogService.Log($"ARCHITEKT: History built for API. Turns={historyForApi.Count}");

            string workspacePath = "";
            if (HasCreatorWorkspaceContext && CreatorWorkspaceContext != null)
            {
                if (!string.IsNullOrWhiteSpace(CreatorWorkspaceContext.WorkspacePath))
                {
                    workspacePath = CreatorWorkspaceContext.WorkspacePath;
                }
            }

            // V režimu Chat se o nástroje ani nepokoušíme, abychom nenarazili na chybu 400 u limitovaných modelů
            bool useTools = IsArchitektAgentMode;
            bool retry = true;
            bool hasTriedPrunedHistory = false;

            while (retry)
            {
                retry = false;
                try
                {
                    var resultStream = _architektDeskService.StreamChatAsync(
                        systemPrompt, historyForApi, SelectedArchitektProfile, workspacePath, useTools, IsArchitektAgentMode, cts.Token);

                    await foreach (var update in resultStream)
                    {
                        if (cts.Token.IsCancellationRequested)
                            break;

                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            if (update.IsStatus)
                            {
                                ArchitektStatus = update.Text ?? "";
                                
                                // Přidáme status do těla zprávy v blockquote, aby uživatel viděl činnost v reálném čase
                                if (!string.IsNullOrEmpty(update.Text) && update.Text.StartsWith("Nástroj:"))
                                {
                                    assistantMsg.Content += $"\n> *{update.Text}*\n";
                                }
                            }
                            else
                            {
                                assistantMsg.Content += update.Text;
                                ArchitektStatus = "Píše odpověď...";
                            }

                            // Vyvolat scroll dolů
                            OnPropertyChanged("ScrollArchitektRequested");
                        });
                    }
                }
                catch (Exception ex) when (useTools && !cts.Token.IsCancellationRequested)
                {
                    // Tiché přepnutí bez tools - uživatel nechce vidět technické hlášky
                    LogService.Log($"ARCHITEKT: Fallback to plain text. Error: {ex.Message}");
                    useTools = false;
                    retry = true;
                    // Nemažeme stávající obsah, ale nepřidáváme technické hlášky
                    ArchitektStatus = "Zpracovávám (text)...";
                    continue;
                }

                // Hail Mary: Pokud model vrátil prázdnou odpověď, zkusíme to naposledy bez historie (jen poslední User + System)
                if (string.IsNullOrWhiteSpace(assistantMsg.Content) && !hasTriedPrunedHistory && !cts.Token.IsCancellationRequested)
                {
                    LogService.Log("ARCHITEKT: Empty response detected. Retrying with PRUNED history (last turn only).");
                    hasTriedPrunedHistory = true;
                    retry = true;
                    // Použijeme jen poslední user zprávu
                    var lastUser = alternatingHistory.LastOrDefault(m => m.IsUser);
                    if (lastUser != null)
                    {
                        historyForApi = new List<ArchitektMessage> { lastUser };
                        continue;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(assistantMsg.Content))
            {
                assistantMsg.Content += "(Žádná odpověď.)";
            }

            ArchitektStatus = string.Empty;
        }
        catch (OperationCanceledException)
        {
            if (string.IsNullOrWhiteSpace(assistantMsg.Content))
                assistantMsg.Content += "(Požadavek byl zrušen.)";
            ArchitektStatus = string.Empty;
        }
        catch (Exception ex)
        {
            int promptLen = systemPrompt?.Length ?? 0;
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                string trace = ex.StackTrace ?? "";
                if (trace.Length > 200) trace = trace.Substring(0, 200);
                
                assistantMsg.Content += $"\n\n⚠ Chyba: {ex.Message} (Prompt: {promptLen} ch)\n\nLog:\n{trace}...";
            });
            ArchitektStatus = "Požadavek selhal.";
            LogService.Error($"ARCHITEKT stream failed (Prompt: {promptLen})", ex);
        }
        finally
        {
            IsArchitektStreaming = false;
            OnPropertyChanged(nameof(HasArchitektHistory));
        }
    }

    [RelayCommand]
    private void StopArchitektStream()
    {
        _architektStreamCts?.Cancel();
    }

    [RelayCommand]
    private void ClearArchitektHistory()
    {
        _architektStreamCts?.Cancel();
        ArchitektHistory.Clear();
        ArchitektStatus = string.Empty;
        OnPropertyChanged(nameof(HasArchitektHistory));
    }

    // ── System prompt builder ─────────────────────────────────────────────────

    private string BuildArchitektSystemPrompt()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Jsi ARCHITEKT, autonomní AI vývojářský asistent integrovaný ve VOID-CRAFT Launcheru.");
        if (IsArchitektAgentMode)
        {
            sb.AppendLine("Máš k dispozici tyto nástroje:");
            sb.AppendLine("- ListWorkspaceDirectory, ReadWorkspaceFile, WriteWorkspaceFile, SearchWeb, GetSystemInfo.");
            sb.AppendLine();
            sb.AppendLine("ODPOVÍDEJ ČESKY, STRUČNĚ A PŘESNĚ. Pokud neznáš soubory, použij ListWorkspaceDirectory.");
        }
        else
        {
            sb.AppendLine("ODPOVÍDEJ ČESKY, STRUČNĚ A PŘESNĚ.");
        }
        
        if (HasCreatorWorkspaceContext)
        {
            sb.AppendLine("## Kontext projektu");
            // Všechny metriky ořezáváme na max 200-500 znaků, aby nezabíraly tokeny
            sb.AppendLine($"- Název: {(CreatorWorkspaceContext.WorkspaceLabel?.Length > 100 ? CreatorWorkspaceContext.WorkspaceLabel[..100] : CreatorWorkspaceContext.WorkspaceLabel)}");
            sb.AppendLine($"- MC: {CreatorWorkspaceContext.MinecraftVersion}");
            
            sb.AppendLine();
        }

        if (IsArchitektAgentMode && HasSelectedCreatorWorkbenchFile && !string.IsNullOrWhiteSpace(CreatorWorkbenchContent))
        {
            sb.AppendLine($"## Aktivní soubor: {SelectedCreatorWorkbenchFile?.RelativePath}");
            sb.AppendLine("```");
            var content = CreatorWorkbenchContent;
            // Zvýšeno na 8000 znaků pro moderní modely (využijí kód lépe)
            const int maxFileChars = 8000;
            sb.AppendLine(content.Length > maxFileChars
                ? content.Substring(0, maxFileChars) + "\n... (oříznuto)"
                : content);
            sb.AppendLine("```");
            LogService.Log($"ARCHITEKT: Prompt built with file {SelectedCreatorWorkbenchFile?.RelativePath} (Safe-limit 600 chars)");
        }
        else if (!IsArchitektAgentMode && HasSelectedCreatorWorkbenchFile)
        {
            sb.AppendLine($"(Poznámka: Soubor {SelectedCreatorWorkbenchFile?.RelativePath} je přiložen, ale v režimu CHAT jeho obsah nevidíš.)");
            LogService.Log("ARCHITEKT: Skip file content (Chat Mode)");
        }
        else
        {
            LogService.Log($"ARCHITEKT: Prompt built WITHOUT active file context");
        }

        if (CurrentModpackCreatorManifest != null)
        {
            var manifest = CurrentModpackCreatorManifest;
            if (!string.IsNullOrWhiteSpace(manifest.PackName))
                sb.AppendLine($"## Pack: {manifest.PackName} ({manifest.Slug}) v{manifest.Version} [{manifest.ReleaseChannel}]");
        }

        return sb.ToString().TrimEnd();
    }
}
