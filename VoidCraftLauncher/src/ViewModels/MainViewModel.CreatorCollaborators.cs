using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using VoidCraftLauncher.Models;
using VoidCraftLauncher.Services;

namespace VoidCraftLauncher.ViewModels;

public partial class MainViewModel
{
    public ObservableCollection<VoidRegistryCollaboratorEntry> CreatorRegistryCollaborators { get; } = new();

    public ObservableCollection<VoidRegistryAccountSearchEntry> CreatorRegistryCollaboratorSearchResults { get; } = new();

    [ObservableProperty]
    private string _creatorCollaboratorSearchQuery = string.Empty;

    [ObservableProperty]
    private string _creatorNewCollaboratorRole = "viewer";

    [ObservableProperty]
    private string _creatorCollaboratorStatus = "Správa spolupracovníků čeká na VOID ID a slug projektu.";

    [ObservableProperty]
    private bool _isCreatorCollaboratorsLoading;

    [ObservableProperty]
    private bool _isCreatorCollaboratorSearchLoading;

    [ObservableProperty]
    private bool _isCreatorCollaboratorMutationRunning;

    [ObservableProperty]
    private bool _creatorCollaboratorsCanManage;

    public bool HasCreatorRegistryCollaborators => CreatorRegistryCollaborators.Count > 0;

    public bool HasCreatorCollaboratorSearchResults => CreatorRegistryCollaboratorSearchResults.Count > 0;

    public bool CanRefreshCreatorCollaborators =>
        !IsCreatorCollaboratorsLoading &&
        HasVoidIdSession &&
        !string.IsNullOrWhiteSpace(GetCreatorRegistrySlug());

    public bool CanSearchCreatorCollaborators =>
        !IsCreatorCollaboratorSearchLoading &&
        !IsCreatorCollaboratorMutationRunning &&
        CreatorCollaboratorsCanManage &&
        !string.IsNullOrWhiteSpace(GetCreatorRegistrySlug()) &&
        HasVoidIdSession;

    public string CreatorCollaboratorProjectLabel => string.IsNullOrWhiteSpace(GetCreatorRegistrySlug())
        ? "Slug projektu zatím chybí"
        : $"Projekt: {GetCreatorRegistrySlug()}";

    public string CreatorCollaboratorAccessLabel => CreatorCollaboratorsCanManage
        ? "Plná správa"
        : HasCreatorRegistryCollaborators
            ? "Jen pro čtení"
            : "Přístup se načte z backendu";

    public string CreatorCollaboratorActionLabel => IsCreatorCollaboratorsLoading
        ? "Načítám spolupracovníky..."
        : "Obnovit spolupracovníky";

    public string CreatorCollaboratorSearchActionLabel => IsCreatorCollaboratorSearchLoading
        ? "Hledám účet..."
        : "Najít účet";

    partial void OnIsCreatorCollaboratorsLoadingChanged(bool value) => NotifyCreatorCollaboratorStateChanged();

    partial void OnIsCreatorCollaboratorSearchLoadingChanged(bool value) => NotifyCreatorCollaboratorStateChanged();

    partial void OnIsCreatorCollaboratorMutationRunningChanged(bool value) => NotifyCreatorCollaboratorStateChanged();

    partial void OnCreatorCollaboratorsCanManageChanged(bool value) => NotifyCreatorCollaboratorStateChanged();

    partial void OnCreatorCollaboratorSearchQueryChanged(string value) => NotifyCreatorCollaboratorStateChanged();

    private void NotifyCreatorCollaboratorStateChanged()
    {
        OnPropertyChanged(nameof(HasCreatorRegistryCollaborators));
        OnPropertyChanged(nameof(HasCreatorCollaboratorSearchResults));
        OnPropertyChanged(nameof(CanRefreshCreatorCollaborators));
        OnPropertyChanged(nameof(CanSearchCreatorCollaborators));
        OnPropertyChanged(nameof(CreatorCollaboratorProjectLabel));
        OnPropertyChanged(nameof(CreatorCollaboratorAccessLabel));
        OnPropertyChanged(nameof(CreatorCollaboratorActionLabel));
        OnPropertyChanged(nameof(CreatorCollaboratorSearchActionLabel));
    }

    private string GetCreatorRegistrySlug()
    {
        return (CurrentModpackCreatorManifest?.Slug ?? string.Empty).Trim();
    }

    private async Task<string?> GetCreatorRegistryAccessTokenAsync(bool showToast)
    {
        if (string.IsNullOrWhiteSpace(GetCreatorRegistrySlug()))
        {
            CreatorCollaboratorStatus = "Manifest ještě nemá slug projektu pro VOID Registry.";
            if (showToast)
            {
                ShowToast("Spolupracovníci", "Nejdřív doplň creator manifest se slugem projektu.", ToastSeverity.Warning, 3200);
            }

            return null;
        }

        if (!await EnsureFreshVoidIdSessionAsync(TimeSpan.FromMinutes(2)))
        {
            CreatorCollaboratorStatus = "Správa spolupracovníků vyžaduje aktivní relaci VOID ID.";
            if (showToast)
            {
                ShowToast("Spolupracovníci", "Nejdřív přihlas správce přes VOID ID.", ToastSeverity.Warning, 3200);
            }

            return null;
        }

        return CreatorVoidIdSession?.AccessToken;
    }

    private async Task ClearCreatorCollaboratorSearchResultsAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            CreatorRegistryCollaboratorSearchResults.Clear();
            NotifyCreatorCollaboratorStateChanged();
        });
    }

    private async Task ResetCreatorCollaboratorsStateAsync(string status)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            CreatorRegistryCollaborators.Clear();
            CreatorRegistryCollaboratorSearchResults.Clear();
            CreatorCollaboratorsCanManage = false;
            CreatorCollaboratorStatus = status;
            NotifyCreatorCollaboratorStateChanged();
        });
    }

    private async Task ApplyCollaboratorBundleAsync(VoidRegistryCollaboratorBundle bundle, string status)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            CreatorRegistryCollaborators.Clear();

            if (bundle.Owner != null)
            {
                bundle.Owner.IsOwner = true;
                bundle.Owner.Role = string.IsNullOrWhiteSpace(bundle.Owner.Role) ? "owner" : bundle.Owner.Role;
                CreatorRegistryCollaborators.Add(bundle.Owner);
            }

            foreach (var collaborator in bundle.Data)
            {
                collaborator.IsOwner = false;
                CreatorRegistryCollaborators.Add(collaborator);
            }

            CreatorCollaboratorsCanManage = bundle.Permissions?.CanManageCollaborators == true;
            CreatorCollaboratorStatus = status;
            NotifyCreatorCollaboratorStateChanged();
        });
    }

    private async Task RefreshCreatorCollaboratorsCoreAsync(bool showToast)
    {
        var slug = GetCreatorRegistrySlug();
        var accessToken = await GetCreatorRegistryAccessTokenAsync(showToast);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return;
        }

        IsCreatorCollaboratorsLoading = true;

        try
        {
            var bundle = await _voidRegistryService.GetCollaboratorsAsync(accessToken, slug);
            await ApplyCollaboratorBundleAsync(bundle, $"Spolupracovníci byli načteni pro {slug}.");
        }
        catch (Exception ex)
        {
            LogService.Error("Creator collaborators refresh failed", ex);
            CreatorCollaboratorStatus = $"Načtení spolupracovníků selhalo: {ex.Message}";
            if (showToast)
            {
                ShowToast("Spolupracovníci", ex.Message, ToastSeverity.Error, 4200);
            }
        }
        finally
        {
            IsCreatorCollaboratorsLoading = false;
        }
    }

    private void QueueRefreshCreatorCollaborators(string status)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await ResetCreatorCollaboratorsStateAsync(status);
                if (HasVoidIdSession && !string.IsNullOrWhiteSpace(GetCreatorRegistrySlug()))
                {
                    await RefreshCreatorCollaboratorsCoreAsync(false);
                }
            }
            catch (Exception ex)
            {
                LogService.Error("Creator collaborators queued refresh failed", ex);
            }
        });
    }

    [RelayCommand]
    private async Task RefreshCreatorCollaborators()
    {
        await RefreshCreatorCollaboratorsCoreAsync(true);
    }

    [RelayCommand]
    private async Task SearchCreatorCollaboratorAccounts()
    {
        var slug = GetCreatorRegistrySlug();
        var query = (CreatorCollaboratorSearchQuery ?? string.Empty).Trim();
        if (query.Length < 2)
        {
            ShowToast("Spolupracovníci", "Pro hledání zadej aspoň 2 znaky nebo přesné ID účtu.", ToastSeverity.Warning, 2600);
            return;
        }

        var accessToken = await GetCreatorRegistryAccessTokenAsync(true);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return;
        }

        if (!CreatorCollaboratorsCanManage)
        {
            ShowToast("Spolupracovníci", "Spolupracovníky může měnit jen vlastník projektu.", ToastSeverity.Warning, 3200);
            return;
        }

        IsCreatorCollaboratorSearchLoading = true;

        try
        {
            var existingIds = CreatorRegistryCollaborators.Select(entry => entry.AccountId).ToHashSet();
            var results = await _voidRegistryService.SearchAccountsAsync(accessToken, query);
            var filtered = results.Where(entry => !existingIds.Contains(entry.AccountId)).ToList();

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                CreatorRegistryCollaboratorSearchResults.Clear();
                foreach (var result in filtered)
                {
                    CreatorRegistryCollaboratorSearchResults.Add(result);
                }

                CreatorCollaboratorStatus = filtered.Count > 0
                    ? $"Nalezeno {filtered.Count} kandidátů pro přidání do {slug}."
                    : $"Pro dotaz '{query}' nebyl nalezen žádný nový kandidát.";
                NotifyCreatorCollaboratorStateChanged();
            });
        }
        catch (Exception ex)
        {
            LogService.Error("Creator collaborator account search failed", ex);
            CreatorCollaboratorStatus = $"Hledání účtů selhalo: {ex.Message}";
            ShowToast("Spolupracovníci", ex.Message, ToastSeverity.Error, 4200);
        }
        finally
        {
            IsCreatorCollaboratorSearchLoading = false;
        }
    }

    [RelayCommand]
    private async Task AddCreatorCollaborator(VoidRegistryAccountSearchEntry? account)
    {
        if (account == null)
        {
            return;
        }

        var slug = GetCreatorRegistrySlug();
        var accessToken = await GetCreatorRegistryAccessTokenAsync(true);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return;
        }

        if (!CreatorCollaboratorsCanManage)
        {
            ShowToast("Spolupracovníci", "Spolupracovníky může přidávat jen vlastník projektu.", ToastSeverity.Warning, 3200);
            return;
        }

        IsCreatorCollaboratorMutationRunning = true;

        try
        {
            var bundle = await _voidRegistryService.AddCollaboratorAsync(accessToken, slug, account.AccountId, CreatorNewCollaboratorRole);
            await ApplyCollaboratorBundleAsync(bundle, $"{account.DisplayName} byl přidán jako {CreatorNewCollaboratorRole}.");
            await ClearCreatorCollaboratorSearchResultsAsync();
            CreatorCollaboratorSearchQuery = string.Empty;
            ShowToast("Spolupracovníci", $"{account.DisplayName} byl přidán do projektu.", ToastSeverity.Success, 2800);
            TrackCreatorActivity($"Přidán spolupracovník {account.DisplayName} do {slug} jako {CreatorNewCollaboratorRole}.", "Release");
        }
        catch (Exception ex)
        {
            LogService.Error("Creator add collaborator failed", ex);
            CreatorCollaboratorStatus = $"Přidání spolupracovníka selhalo: {ex.Message}";
            ShowToast("Spolupracovníci", ex.Message, ToastSeverity.Error, 4200);
        }
        finally
        {
            IsCreatorCollaboratorMutationRunning = false;
        }
    }

    [RelayCommand]
    private async Task UpdateCreatorCollaborator(VoidRegistryCollaboratorEntry? collaborator)
    {
        if (collaborator == null || collaborator.IsOwner)
        {
            return;
        }

        var slug = GetCreatorRegistrySlug();
        var accessToken = await GetCreatorRegistryAccessTokenAsync(true);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return;
        }

        if (!CreatorCollaboratorsCanManage)
        {
            ShowToast("Spolupracovníci", "Spolupracovníky může upravovat jen vlastník projektu.", ToastSeverity.Warning, 3200);
            return;
        }

        IsCreatorCollaboratorMutationRunning = true;

        try
        {
            var bundle = await _voidRegistryService.UpdateCollaboratorAsync(accessToken, slug, collaborator.AccountId, collaborator.Role);
            await ApplyCollaboratorBundleAsync(bundle, $"{collaborator.DisplayName} má nově roli {collaborator.Role}.");
            ShowToast("Spolupracovníci", $"Role pro {collaborator.DisplayName} byla uložená.", ToastSeverity.Success, 2600);
            TrackCreatorActivity($"Upravena role spolupracovníka {collaborator.DisplayName} v {slug} na {collaborator.Role}.", "Release");
        }
        catch (Exception ex)
        {
            LogService.Error("Creator update collaborator failed", ex);
            CreatorCollaboratorStatus = $"Úprava spolupracovníka selhala: {ex.Message}";
            ShowToast("Spolupracovníci", ex.Message, ToastSeverity.Error, 4200);
        }
        finally
        {
            IsCreatorCollaboratorMutationRunning = false;
        }
    }

    [RelayCommand]
    private async Task RemoveCreatorCollaborator(VoidRegistryCollaboratorEntry? collaborator)
    {
        if (collaborator == null || collaborator.IsOwner)
        {
            return;
        }

        var slug = GetCreatorRegistrySlug();
        var accessToken = await GetCreatorRegistryAccessTokenAsync(true);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return;
        }

        if (!CreatorCollaboratorsCanManage)
        {
            ShowToast("Spolupracovníci", "Spolupracovníky může odebírat jen vlastník projektu.", ToastSeverity.Warning, 3200);
            return;
        }

        IsCreatorCollaboratorMutationRunning = true;

        try
        {
            await _voidRegistryService.RemoveCollaboratorAsync(accessToken, slug, collaborator.AccountId);
            await RefreshCreatorCollaboratorsCoreAsync(false);
            ShowToast("Spolupracovníci", $"{collaborator.DisplayName} byl odebraný z projektu.", ToastSeverity.Success, 2600);
            TrackCreatorActivity($"Odebrán spolupracovník {collaborator.DisplayName} z {slug}.", "Release");
        }
        catch (Exception ex)
        {
            LogService.Error("Creator remove collaborator failed", ex);
            CreatorCollaboratorStatus = $"Odebrání spolupracovníka selhalo: {ex.Message}";
            ShowToast("Spolupracovníci", ex.Message, ToastSeverity.Error, 4200);
        }
        finally
        {
            IsCreatorCollaboratorMutationRunning = false;
        }
    }
}