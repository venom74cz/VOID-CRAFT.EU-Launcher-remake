using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VoidCraftLauncher.Models;

namespace VoidCraftLauncher.ViewModels;

public partial class MainViewModel
{
    private const int MaxCrashHistoryEntries = 40;
    private const int MaxCrashLogTailCharacters = 6000;

    public ObservableCollection<CrashHistoryEntry> CurrentModpackCrashHistory { get; } = new();

    public bool HasCurrentModpackCrashHistory => CurrentModpackCrashHistory.Count > 0;

    public string CurrentModpackCrashHistoryEmptyTitle => "Zatím bez pádů";

    public string CurrentModpackCrashHistoryEmptySubtitle => Config.CrashHistory?.Count > 0
        ? "Pro tuhle instanci zatím launcher neeviduje žádný pád. Nové záznamy se uloží automaticky po pádu hry."
        : "Po prvním pádu zaznamenaném launcherem se tady uloží exit code, délka běhu a výpis logu.";

    private IReadOnlyList<CrashHistoryEntry> GetCurrentModpackCrashHistoryEntries()
    {
        if (CurrentModpack == null || string.IsNullOrWhiteSpace(CurrentModpack.Name))
        {
            return Array.Empty<CrashHistoryEntry>();
        }

        Config.CrashHistory ??= new List<CrashHistoryEntry>();

        return Config.CrashHistory
            .Where(entry => !string.IsNullOrWhiteSpace(entry.ModpackName) && ArePackNamesEquivalent(entry.ModpackName, CurrentModpack.Name))
            .OrderByDescending(entry => entry.OccurredAtUtc)
            .ToList();
    }

    private void RefreshCurrentModpackCrashHistoryCollection(IReadOnlyList<CrashHistoryEntry>? entries = null)
    {
        entries ??= GetCurrentModpackCrashHistoryEntries();

        CurrentModpackCrashHistory.Clear();
        foreach (var entry in entries)
        {
            CurrentModpackCrashHistory.Add(entry);
        }

        OnPropertyChanged(nameof(HasCurrentModpackCrashHistory));
        OnPropertyChanged(nameof(CurrentModpackCrashHistoryEmptyTitle));
        OnPropertyChanged(nameof(CurrentModpackCrashHistoryEmptySubtitle));
    }

    private void RecordCrashHistoryEntry(string modpackName, int exitCode, TimeSpan runtime, string? logTail, string? logPath)
    {
        if (string.IsNullOrWhiteSpace(modpackName))
        {
            return;
        }

        Config.CrashHistory ??= new List<CrashHistoryEntry>();

        var occurredAtUtc = DateTime.UtcNow;
        Config.CrashHistory.Insert(0, new CrashHistoryEntry
        {
            ModpackName = modpackName,
            OccurredAtUtc = occurredAtUtc,
            ExitCode = exitCode,
            RuntimeSeconds = Math.Max(0, (int)Math.Round(runtime.TotalSeconds)),
            Summary = BuildCrashSummary(exitCode),
            LogTail = TrimCrashLogTail(logTail),
            LogPath = string.IsNullOrWhiteSpace(logPath) ? null : logPath,
            CrashReportPath = FindRecentCrashReportPath(modpackName, occurredAtUtc)
        });

        Config.CrashHistory = Config.CrashHistory
            .OrderByDescending(entry => entry.OccurredAtUtc)
            .Take(MaxCrashHistoryEntries)
            .ToList();

        _launcherService.SaveConfig(Config);

        if (CurrentModpack != null && ArePackNamesEquivalent(CurrentModpack.Name, modpackName))
        {
            RefreshCurrentModpackCrashHistoryCollection();
        }
    }

    private string? FindRecentCrashReportPath(string modpackName, DateTime occurredAtUtc)
    {
        var crashReportsPath = Path.Combine(_launcherService.GetModpackPath(modpackName), "crash-reports");
        if (!Directory.Exists(crashReportsPath))
        {
            return null;
        }

        return Directory.EnumerateFiles(crashReportsPath, "*.txt", SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .Where(info => info.Exists && info.LastWriteTimeUtc >= occurredAtUtc.AddMinutes(-10))
            .OrderByDescending(info => info.LastWriteTimeUtc)
            .FirstOrDefault()
            ?.FullName;
    }

    private static string TrimCrashLogTail(string? logTail)
    {
        if (string.IsNullOrWhiteSpace(logTail))
        {
            return string.Empty;
        }

        var normalized = logTail.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        return normalized.Length <= MaxCrashLogTailCharacters
            ? normalized
            : normalized[^MaxCrashLogTailCharacters..];
    }

    private static string BuildCrashSummary(int exitCode) => exitCode switch
    {
        -1 => "Hra byla násilně ukončena (out of memory nebo kill).",
        1 => "Hra skončila s obecnou chybou. Zkontrolujte log.",
        _ => $"Hra skončila s kódem {exitCode}."
    };

    [RelayCommand]
    private void RefreshCrashHistory()
    {
        RefreshCurrentModpackCrashHistoryCollection();
    }

    [RelayCommand]
    private void OpenCrashHistoryArtifact(CrashHistoryEntry? entry)
    {
        var path = entry?.PreferredArtifactPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    [RelayCommand]
    private async Task CopyCrashHistoryLog(CrashHistoryEntry? entry)
    {
        if (entry == null || string.IsNullOrWhiteSpace(entry.LogTail))
        {
            return;
        }

        var clipboard = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow?.Clipboard
            : null;

        if (clipboard != null)
        {
            await clipboard.SetTextAsync(entry.LogTail);
        }
    }
}