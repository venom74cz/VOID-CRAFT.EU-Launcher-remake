using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using VoidCraftLauncher.Models;
using VoidCraftLauncher.Models.CreatorStudio;

namespace VoidCraftLauncher.ViewModels;

public partial class MainViewModel
{
    [RelayCommand]
    private void SwitchCreatorTab(string? tabId)
    {
        var targetTab = tabId?.Trim().ToLowerInvariant() switch
        {
            "overview" => CreatorShellTab.Overview,
            "identity" => CreatorShellTab.Identity,
            "mods" => CreatorShellTab.Mods,
            "files" => CreatorShellTab.Files,
            "notes" => CreatorShellTab.Notes,
            "git" => CreatorShellTab.Git,
            "release" => CreatorShellTab.Release,
            _ => CreatorShellTab.Overview
        };

        SetCreatorSelectedTab(targetTab);
    }

    [RelayCommand]
    private async Task SaveActiveCreatorSurface()
    {
        if (CreatorShellState.SelectedTab == CreatorShellTab.Files && CanSaveCreatorWorkbenchFile)
        {
            await SaveCreatorWorkbenchFile();
            return;
        }

        if (CreatorShellState.SelectedTab == CreatorShellTab.Identity && IsCreatorMetadataDirty && CanSaveCreatorMetadata)
        {
            await SaveCreatorMetadata();
            return;
        }

        if (CreatorShellState.SelectedTab == CreatorShellTab.Notes && CanSaveCreatorNote)
        {
            await SaveActiveCreatorNotesSurface();
            return;
        }

        if (CreatorShellState.SelectedTab == CreatorShellTab.Notes && CanSaveCreatorCanvasGraph)
        {
            await SaveActiveCreatorNotesSurface();
            return;
        }

        if (CanSaveCreatorWorkbenchFile)
        {
            await SaveCreatorWorkbenchFile();
            return;
        }

        if (IsCreatorMetadataDirty && CanSaveCreatorMetadata)
        {
            await SaveCreatorMetadata();
            return;
        }

        if (CanSaveCreatorNote)
        {
            await SaveCreatorNote();
            return;
        }

        if (CanSaveCreatorCanvasGraph)
        {
            await SaveCreatorCanvasGraph();
            return;
        }

        ShowToast("Creator Studio", "Aktivni plocha nema co ulozit.", ToastSeverity.Info, 1800);
    }

    [RelayCommand]
    private async Task SaveAllCreatorSurface()
    {
        var savedAnything = false;

        if (IsCreatorMetadataDirty && CanSaveCreatorMetadata)
        {
            await SaveCreatorMetadata();
            savedAnything = true;
        }

        if (CanSaveCreatorWorkbenchFile)
        {
            await SaveCreatorWorkbenchFile();
            savedAnything = true;
        }

        if (CanSaveCreatorNote)
        {
            await SaveCreatorNote();
            savedAnything = true;
        }

        if (CanSaveCreatorCanvasGraph)
        {
            await SaveCreatorCanvasGraph();
            savedAnything = true;
        }

        if (!savedAnything)
        {
            ShowToast("Creator Studio", "Workspace nema zadne lokalni zmeny k ulozeni.", ToastSeverity.Info, 1800);
        }
    }

    [RelayCommand]
    private async Task QuickCreatorExport()
    {
        await CreatorExportVoidpack();
    }

    [RelayCommand]
    private void OpenCreatorWorkbenchDiffMode()
    {
        if (CreatorShellState.SelectedTab != CreatorShellTab.Files)
        {
            SetCreatorSelectedTab(CreatorShellTab.Files);
        }

        if (!HasSelectedCreatorWorkbenchFile)
        {
            ShowToast("Creator Studio", "Diff rezim potrebuje otevreny soubor v Files tabu.", ToastSeverity.Warning, 2200);
            return;
        }

        SetCreatorWorkbenchModeById(CreatorWorkbenchModeDiffId);
        TrackCreatorActivity("Zapnut diff rezim Creator Workbench.");
    }

    [RelayCommand]
    private void FocusCreatorCopilotDesk()
    {
        UseCreatorCopilotDesk();
    }

    [RelayCommand]
    private void HandleCreatorEscape()
    {
        if (IsCreatorGlobalSearchOpen)
        {
            ToggleCreatorGlobalSearch();
            return;
        }

        if (IsCreatorNotesDrawerOpen)
        {
            CloseCreatorNotesDrawer();
        }
    }
}