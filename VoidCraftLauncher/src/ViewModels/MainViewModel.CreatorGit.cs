using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
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

    partial void OnIsCreatorGitOperationRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(CanCreatorGitCommit));
        OnPropertyChanged(nameof(CanCreatorGitStageAll));
        OnPropertyChanged(nameof(CanCreatorGitStageIndividualFiles));
        OnPropertyChanged(nameof(CanCreatorGitGenerateVoidpack));
        OnPropertyChanged(nameof(CanCreatorGitUploadPublishPayload));
    }

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
        CreatorGitStagedCount > 0;

    public bool CanCreatorGitStageAll =>
        HasCreatorGitRepository &&
        HasCreatorGitChanges &&
        !IsCreatorGitOperationRunning;

    public bool CanCreatorGitGenerateVoidpack =>
        !IsCreatorGitOperationRunning &&
        HasCreatorWorkspaceContext;

    public bool CanCreatorGitUploadPublishPayload =>
        HasCreatorGitRepository &&
        CreatorGitState.HasRemote &&
        !IsCreatorGitOperationRunning;

    public bool CanCreatorGitStageIndividualFiles =>
        HasCreatorGitRepository &&
        !IsCreatorGitOperationRunning;

    [RelayCommand]
    private async Task RefreshCreatorGitStatus()
    {
        var workspacePath = SkinStudioSelectedInstancePath;
        if (string.IsNullOrWhiteSpace(workspacePath)) return;

        var status = await GetCreatorPublishGitStatusAsync(workspacePath);
        var branches = status.IsRepository
            ? await _creatorGitService.GetBranchesAsync(workspacePath)
            : new System.Collections.Generic.List<string>();

        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            CreatorGitState = status;
            CreatorGitBranches.Clear();
            if (status.IsRepository)
            {
                foreach (var branch in branches)
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

        IsCreatorGitOperationRunning = true;
        try
        {
            var publishTrackedPaths = await GetCreatorPublishTrackedPathsAsync(workspacePath);
            var ok = await _creatorGitService.StagePathsAsync(workspacePath, publishTrackedPaths);
            if (!ok)
            {
                ShowToast("Git", "Stage publish payload selhal.", ToastSeverity.Error, 2500);
            }

            await RefreshCreatorGitStatus();
        }
        finally
        {
            IsCreatorGitOperationRunning = false;
        }
    }

    [RelayCommand]
    private async Task CreatorGitStageFile(CreatorGitChange? change)
    {
        if (change == null) return;
        var workspacePath = SkinStudioSelectedInstancePath;
        if (string.IsNullOrWhiteSpace(workspacePath)) return;

        IsCreatorGitOperationRunning = true;
        try
        {
            var ok = change.IsStaged
                ? await _creatorGitService.UnstageFileAsync(workspacePath, change.FilePath)
                : await _creatorGitService.StageFileAsync(workspacePath, change.FilePath);

            if (!ok)
            {
                ShowToast("Git", $"Operace nad {change.FilePath} selhala.", ToastSeverity.Error, 2600);
            }

            await RefreshCreatorGitStatus();
        }
        finally
        {
            IsCreatorGitOperationRunning = false;
        }
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
            var commitMessage = ResolveCreatorGitCommitMessage();
            var stagedPublishPaths = GetStagedCreatorGitPaths();
            var ok = await _creatorGitService.CommitAsync(workspacePath, commitMessage, stagedPublishPaths);
            if (ok)
            {
                ShowToast("Git", "Commit odeslan.", ToastSeverity.Success, 2500);
                TrackCreatorActivity($"Commit: \"{commitMessage}\"");
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
        var hadStagedChanges = CreatorGitStagedCount > 0;
        await CreatorGitCommit();
        if (hadStagedChanges && CreatorGitState.HasRemote)
            await CreatorGitPush();
    }

    [RelayCommand]
    private async Task CreatorGitUploadPublishPayload()
    {
        var workspacePath = SkinStudioSelectedInstancePath;
        if (string.IsNullOrWhiteSpace(workspacePath)) return;

        IsCreatorGitOperationRunning = true;
        try
        {
            var manifest = await EnsureCreatorManifestForPublishAsync(workspacePath, showToast: true);
            if (manifest == null)
            {
                return;
            }

            if (!_gitHubReleaseService.HasWorkflowFiles(workspacePath))
            {
                await BootstrapCreatorPublishWorkflow();
            }

            CreatorPublishStatus = "Staguji publish obsah pro GitHub...";
            var publishTrackedPaths = await GetCreatorPublishTrackedPathsAsync(workspacePath);
            if (!await _creatorGitService.StagePathsAsync(workspacePath, publishTrackedPaths))
            {
                ShowToast("Git", "Upload publish obsahu selhal při stage.", ToastSeverity.Error, 3200);
                return;
            }

            var gitStatus = await GetCreatorPublishGitStatusAsync(workspacePath);
            var stagedPublishPaths = gitStatus.Changes
                .Where(change => change.IsStaged)
                .Select(change => change.FilePath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var createdCommit = false;
            if (stagedPublishPaths.Count == 0)
            {
                CreatorPublishStatus = "Publish obsah je beze změn. Spustím workflow nad aktuálním stavem repa.";
            }

            if (stagedPublishPaths.Count > 0)
            {
                var commitMessage = ResolveCreatorGitCommitMessage();
                CreatorPublishStatus = $"Commituji publish obsah: {commitMessage}";
                var commitOk = await _creatorGitService.CommitAsync(workspacePath, commitMessage, stagedPublishPaths);
                if (!commitOk)
                {
                    ShowToast("Git", "Commit publish obsahu selhal.", ToastSeverity.Error, 3200);
                    return;
                }

                createdCommit = true;
                CreatorGitCommitMessage = string.Empty;
            }

            CreatorPublishStatus = createdCommit
                ? "Pushuji publish obsah na GitHub..."
                : "Kontroluji GitHub repo před spuštěním workflow...";
            var pushOk = await _creatorGitService.PushAsync(workspacePath, CreatorGitHubSession?.AccessToken);
            if (!pushOk)
            {
                ShowToast("Git", "Push publish obsahu selhal.", ToastSeverity.Error, 3200);
                return;
            }

            var tagName = _gitHubReleaseService.BuildTagName(manifest.Version);
            CreatorPublishStatus = $"Vytvářím workflow tag {tagName}...";
            var tagOk = await _creatorGitService.CreateOrUpdateTagAsync(workspacePath, tagName, $"Release {manifest.PackName} {tagName}");
            if (!tagOk)
            {
                ShowToast("Git", "Vytvoření workflow tagu selhalo.", ToastSeverity.Error, 3200);
                return;
            }

            CreatorPublishStatus = $"Pushuji workflow tag {tagName} na GitHub...";
            var pushTagOk = await _creatorGitService.PushTagAsync(workspacePath, tagName, CreatorGitHubSession?.AccessToken);
            if (!pushTagOk)
            {
                ShowToast("Git", "Push workflow tagu selhal.", ToastSeverity.Error, 3200);
                return;
            }

            CreatorPublishStatus = $"Publish obsah je na GitHubu a workflow {tagName} bylo spuštěné.";
            ShowToast(
                "Git",
                createdCommit
                    ? $"Publish obsah byl nahraný a workflow {tagName} spuštěné."
                    : $"Repo už bylo synchronizované. Workflow {tagName} bylo spuštěné znovu.",
                ToastSeverity.Success,
                3600);
            TrackCreatorActivity($"Publish obsah + workflow {tagName}: {manifest.Slug} v{manifest.Version}", "Git");

            await RefreshCreatorGitStatus();
            await RefreshCreatorReleasePipeline();
        }
        finally
        {
            IsCreatorGitOperationRunning = false;
            NotifyCreatorGitStateChanged();
        }
    }

    [RelayCommand]
    private async Task CreatorGitPull()
    {
        var workspacePath = SkinStudioSelectedInstancePath;
        if (string.IsNullOrWhiteSpace(workspacePath)) return;

        IsCreatorGitOperationRunning = true;
        try
        {
            var ok = await _creatorGitService.PullAsync(workspacePath, CreatorGitHubSession?.AccessToken);
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
            var ok = await _creatorGitService.PushAsync(workspacePath, CreatorGitHubSession?.AccessToken);
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

        IsCreatorGitOperationRunning = true;
        try
        {
            var ok = await _creatorGitService.RevertFileAsync(workspacePath, change.FilePath);
            if (ok)
            {
                ShowToast("Git", $"Soubor {change.FilePath} vracen.", ToastSeverity.Success, 2000);
            }

            await RefreshCreatorGitStatus();
        }
        finally
        {
            IsCreatorGitOperationRunning = false;
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
        OnPropertyChanged(nameof(CanCreatorGitStageAll));
        OnPropertyChanged(nameof(CanCreatorGitGenerateVoidpack));
        OnPropertyChanged(nameof(CanCreatorGitStageIndividualFiles));
        OnPropertyChanged(nameof(CanCreatorGitUploadPublishPayload));
        OnPropertyChanged(nameof(CreatorGitIsAvailable));
        NotifyCreatorGitHubStateChanged();
    }

    private async Task<CreatorGitStatus> GetCreatorPublishGitStatusAsync(string workspacePath)
    {
        var statusScopePaths = _gitHubReleaseService.GetPublishStatusScopePaths(workspacePath);
        var status = await _creatorGitService.GetStatusAsync(workspacePath, statusScopePaths);
        status.Changes = status.Changes
            .Where(change => _gitHubReleaseService.IsPublishTrackedPath(change.FilePath))
            .ToList();
        return status;
    }

    private IReadOnlyList<string> GetStagedCreatorGitPaths()
    {
        return CreatorGitState.Changes
            .Where(change => change.IsStaged)
            .Select(change => change.FilePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private string ResolveCreatorGitCommitMessage()
    {
        if (!string.IsNullOrWhiteSpace(CreatorGitCommitMessage))
        {
            return CreatorGitCommitMessage.Trim();
        }

        var manifest = CurrentModpackCreatorManifest;
        if (manifest != null)
        {
            var label = !string.IsNullOrWhiteSpace(manifest.Slug) ? manifest.Slug : manifest.PackName;
            var versionSuffix = !string.IsNullOrWhiteSpace(manifest.Version) ? $" v{manifest.Version}" : string.Empty;
            if (!string.IsNullOrWhiteSpace(label))
            {
                return $"publish: {label}{versionSuffix}";
            }
        }

        return "publish: update voidpack sources";
    }
}
