using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using VoidCraftLauncher.Models.CreatorStudio;

namespace VoidCraftLauncher.ViewModels;

public partial class MainViewModel
{
    // ──── Global Creator Search ────
    [ObservableProperty]
    private string _creatorGlobalSearchQuery = string.Empty;

    [ObservableProperty]
    private bool _isCreatorGlobalSearchOpen;

    public ObservableCollection<CreatorGlobalSearchResult> CreatorGlobalSearchResults { get; } = new();

    public bool HasCreatorGlobalSearchResults => CreatorGlobalSearchResults.Count > 0;

    [RelayCommand]
    private void ToggleCreatorGlobalSearch()
    {
        IsCreatorGlobalSearchOpen = !IsCreatorGlobalSearchOpen;
        if (!IsCreatorGlobalSearchOpen)
        {
            CreatorGlobalSearchQuery = string.Empty;
            CreatorGlobalSearchResults.Clear();
        }
    }

    [RelayCommand]
    private void ExecuteCreatorGlobalSearch()
    {
        var query = CreatorGlobalSearchQuery?.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            CreatorGlobalSearchResults.Clear();
            OnPropertyChanged(nameof(HasCreatorGlobalSearchResults));
            return;
        }

        CreatorGlobalSearchResults.Clear();

        // Search files
        foreach (var file in CreatorWorkbenchFiles.Where(f =>
            f.RelativePath.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            f.Category.Contains(query, StringComparison.OrdinalIgnoreCase)))
        {
            CreatorGlobalSearchResults.Add(new CreatorGlobalSearchResult
            {
                Label = file.RelativePath,
                Subtitle = $"{file.Category} · {file.SizeLabel}",
                Category = "Soubory",
                TargetTab = "Files",
                TargetPath = file.FullPath
            });
        }

        // Search installed mods
        foreach (var mod in InstalledMods.Where(m =>
            (m.Name?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (m.Author?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)))
        {
            CreatorGlobalSearchResults.Add(new CreatorGlobalSearchResult
            {
                Label = mod.Name ?? mod.InstalledFileName ?? "???",
                Subtitle = $"{mod.Source} · {mod.Author}",
                Category = "Mody",
                TargetTab = "Mods"
            });
        }

        // Search notes
        foreach (var doc in CreatorNoteDocuments.Concat(CreatorWikiDocuments).Where(d =>
            d.Title.Contains(query, StringComparison.OrdinalIgnoreCase)))
        {
            CreatorGlobalSearchResults.Add(new CreatorGlobalSearchResult
            {
                Label = doc.Title,
                Subtitle = $"{(doc.IsWikiPage ? "Wiki" : "Docs")} · upraveno {doc.ModifiedLabel}",
                Category = "Poznamky",
                TargetTab = "Notes",
                TargetPath = doc.FullPath
            });
        }

        // Search git commits
        foreach (var commit in CreatorGitState.RecentCommits.Where(c =>
            c.Message.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            c.Hash.Contains(query, StringComparison.OrdinalIgnoreCase)))
        {
            CreatorGlobalSearchResults.Add(new CreatorGlobalSearchResult
            {
                Label = commit.Message,
                Subtitle = $"{commit.ShortHash} · {commit.Author} · {commit.TimeAgoLabel}",
                Category = "Git",
                TargetTab = "Git"
            });
        }

        // Search manifest fields
        var manifest = CurrentModpackCreatorManifest;
        if (manifest != null)
        {
            if (manifest.PackName?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
                CreatorGlobalSearchResults.Add(new CreatorGlobalSearchResult { Label = manifest.PackName, Subtitle = "Pack name", Category = "Manifest", TargetTab = "Identity" });
            if (manifest.PrimaryServer?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
                CreatorGlobalSearchResults.Add(new CreatorGlobalSearchResult { Label = manifest.PrimaryServer, Subtitle = "Primary server", Category = "Manifest", TargetTab = "Identity" });
        }

        OnPropertyChanged(nameof(HasCreatorGlobalSearchResults));
    }

    [RelayCommand]
    private void NavigateCreatorSearchResult(CreatorGlobalSearchResult? result)
    {
        if (result == null) return;

        IsCreatorGlobalSearchOpen = false;

        var tab = result.TargetTab switch
        {
            "Overview" => CreatorShellTab.Overview,
            "Identity" => CreatorShellTab.Identity,
            "Mods" => CreatorShellTab.Mods,
            "Files" => CreatorShellTab.Files,
            "Notes" => CreatorShellTab.Notes,
            "Git" => CreatorShellTab.Git,
            "Release" => CreatorShellTab.Release,
            _ => CreatorShellTab.Overview
        };

        SetCreatorSelectedTab(tab);

        if (result.TargetTab == "Files" && !string.IsNullOrWhiteSpace(result.TargetPath))
        {
            var targetFile = FilteredCreatorWorkbenchFiles.FirstOrDefault(f =>
                string.Equals(f.FullPath, result.TargetPath, StringComparison.OrdinalIgnoreCase));
            if (targetFile != null)
                SelectedCreatorWorkbenchFile = targetFile;
        }
        else if (result.TargetTab == "Notes" && !string.IsNullOrWhiteSpace(result.TargetPath))
        {
            var targetDoc = CreatorNoteDocuments.Concat(CreatorWikiDocuments).FirstOrDefault(d =>
                string.Equals(d.FullPath, result.TargetPath, StringComparison.OrdinalIgnoreCase));
            if (targetDoc != null)
            {
                SetCreatorNotesMode(targetDoc.IsWikiPage ? "wiki" : "docs");
                SelectedCreatorNoteDocument = targetDoc;
            }
        }
    }

    partial void OnCreatorGlobalSearchQueryChanged(string value)
    {
        if (IsCreatorGlobalSearchOpen)
            ExecuteCreatorGlobalSearch();
    }
}
