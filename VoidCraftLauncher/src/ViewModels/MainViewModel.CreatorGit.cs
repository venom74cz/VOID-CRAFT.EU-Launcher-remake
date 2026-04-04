using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using VoidCraftLauncher.Models;
using VoidCraftLauncher.Models.CreatorStudio;

namespace VoidCraftLauncher.ViewModels;

public partial class MainViewModel
{
    // ──── Git State ────
    [ObservableProperty]
    private CreatorGitStatus _creatorGitState = new();

    [ObservableProperty]
    private string _creatorGitCommitMessage = string.Empty;

    [ObservableProperty]
    private bool _isCreatorGitOperationRunning;

    public ObservableCollection<string> CreatorGitBranches { get; } = new();

    public bool HasCreatorGitRepository => CreatorGitState.IsRepository;

    public bool HasCreatorGitChanges => CreatorGitState.HasChanges;

    public int CreatorGitChangesCount => CreatorGitState.Changes.Count;

    public int CreatorGitStagedCount => CreatorGitState.Changes.Count(c => c.IsStaged);

    public string CreatorGitBranchSummary => CreatorGitState.BranchSummary;

    public string CreatorGitLastCommitLabel => CreatorGitState.LastCommitLabel;

    public string CreatorGitRemoteLabel => CreatorGitState.HasRemote
        ? CreatorGitState.RemoteUrl
        : "Zadny remote";

    public bool CreatorGitIsAvailable => _creatorGitService.IsGitAvailable();

    public bool CanCreatorGitCommit =>
        HasCreatorGitRepository &&
        !IsCreatorGitOperationRunning &&
        !string.IsNullOrWhiteSpace(CreatorGitCommitMessage) &&
        CreatorGitStagedCount > 0;

    [RelayCommand]
    private async Task RefreshCreatorGitStatus()
    {
        var workspacePath = SkinStudioSelectedInstancePath;
        if (string.IsNullOrWhiteSpace(workspacePath)) return;

        var status = await _creatorGitService.GetStatusAsync(workspacePath);

        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            CreatorGitState = status;
            CreatorGitBranches.Clear();
            if (status.IsRepository)
            {
                foreach (var branch in _creatorGitService.GetBranches(workspacePath))
                    CreatorGitBranches.Add(branch);
            }
            NotifyCreatorGitStateChanged();
        });
    }

    [RelayCommand]
    private async Task CreatorGitInit()
    {
        var workspacePath = SkinStudioSelectedInstancePath;
        if (string.IsNullOrWhiteSpace(workspacePath)) return;

        IsCreatorGitOperationRunning = true;
        try
        {
            var ok = await _creatorGitService.InitRepositoryAsync(workspacePath);
            if (ok)
            {
                ShowToast("Git", "Repository inicializovan.", ToastSeverity.Success, 2500);
                TrackCreatorActivity("Git repository inicializovan.");
                await RefreshCreatorGitStatus();
            }
            else
            {
                ShowToast("Git", "Inicializace se nezdarila.", ToastSeverity.Error);
            }
        }
        finally
        {
            IsCreatorGitOperationRunning = false;
        }
    }

    [RelayCommand]
    private async Task CreatorGitStageAll()
    {
        var workspacePath = SkinStudioSelectedInstancePath;
        if (string.IsNullOrWhiteSpace(workspacePath)) return;

        await _creatorGitService.StageAllAsync(workspacePath);
        await RefreshCreatorGitStatus();
    }

    [RelayCommand]
    private async Task CreatorGitStageFile(CreatorGitChange? change)
    {
        if (change == null) return;
        var workspacePath = SkinStudioSelectedInstancePath;
        if (string.IsNullOrWhiteSpace(workspacePath)) return;

        if (change.IsStaged)
            await _creatorGitService.UnstageFileAsync(workspacePath, change.FilePath);
        else
            await _creatorGitService.StageFileAsync(workspacePath, change.FilePath);

        await RefreshCreatorGitStatus();
    }

    [RelayCommand]
    private async Task CreatorGitCommit()
    {
        if (!CanCreatorGitCommit) return;

        var workspacePath = SkinStudioSelectedInstancePath;
        if (string.IsNullOrWhiteSpace(workspacePath)) return;

        IsCreatorGitOperationRunning = true;
        try
        {
            var ok = await _creatorGitService.CommitAsync(workspacePath, CreatorGitCommitMessage);
            if (ok)
            {
                ShowToast("Git", "Commit odeslan.", ToastSeverity.Success, 2500);
                TrackCreatorActivity($"Commit: \"{CreatorGitCommitMessage}\"");
                CreatorGitCommitMessage = string.Empty;
                await RefreshCreatorGitStatus();
            }
            else
            {
                ShowToast("Git", "Commit selhal.", ToastSeverity.Error);
            }
        }
        finally
        {
            IsCreatorGitOperationRunning = false;
        }
    }

    [RelayCommand]
    private async Task CreatorGitCommitAndPush()
    {
        await CreatorGitCommit();
        if (CreatorGitState.HasRemote)
            await CreatorGitPush();
    }

    [RelayCommand]
    private async Task CreatorGitPull()
    {
        var workspacePath = SkinStudioSelectedInstancePath;
        if (string.IsNullOrWhiteSpace(workspacePath)) return;

        IsCreatorGitOperationRunning = true;
        try
        {
            var ok = await _creatorGitService.PullAsync(workspacePath);
            ShowToast("Git", ok ? "Pull dokoncen." : "Pull selhal.", ok ? ToastSeverity.Success : ToastSeverity.Error, 2500);
            if (ok) TrackCreatorActivity("Git pull dokoncen.");
            await RefreshCreatorGitStatus();
        }
        finally
        {
            IsCreatorGitOperationRunning = false;
        }
    }

    [RelayCommand]
    private async Task CreatorGitPush()
    {
        var workspacePath = SkinStudioSelectedInstancePath;
        if (string.IsNullOrWhiteSpace(workspacePath)) return;

        IsCreatorGitOperationRunning = true;
        try
        {
            var ok = await _creatorGitService.PushAsync(workspacePath);
            ShowToast("Git", ok ? "Push dokoncen." : "Push selhal.", ok ? ToastSeverity.Success : ToastSeverity.Error, 2500);
            if (ok) TrackCreatorActivity("Git push dokoncen.");
            await RefreshCreatorGitStatus();
        }
        finally
        {
            IsCreatorGitOperationRunning = false;
        }
    }

    [RelayCommand]
    private async Task CreatorGitRevertFile(CreatorGitChange? change)
    {
        if (change == null) return;
        var workspacePath = SkinStudioSelectedInstancePath;
        if (string.IsNullOrWhiteSpace(workspacePath)) return;

        var ok = await _creatorGitService.RevertFileAsync(workspacePath, change.FilePath);
        if (ok)
        {
            ShowToast("Git", $"Soubor {change.FilePath} vracen.", ToastSeverity.Success, 2000);
            await RefreshCreatorGitStatus();
        }
    }

    private void NotifyCreatorGitStateChanged()
    {
        OnPropertyChanged(nameof(HasCreatorGitRepository));
        OnPropertyChanged(nameof(HasCreatorGitChanges));
        OnPropertyChanged(nameof(CreatorGitChangesCount));
        OnPropertyChanged(nameof(CreatorGitStagedCount));
        OnPropertyChanged(nameof(CreatorGitBranchSummary));
        OnPropertyChanged(nameof(CreatorGitLastCommitLabel));
        OnPropertyChanged(nameof(CreatorGitRemoteLabel));
        OnPropertyChanged(nameof(CanCreatorGitCommit));
        OnPropertyChanged(nameof(CreatorGitIsAvailable));
    }
}
