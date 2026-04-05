using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using VoidCraftLauncher.Models;
using VoidCraftLauncher.Models.CreatorStudio;
using VoidCraftLauncher.Services;

namespace VoidCraftLauncher.ViewModels;

public partial class MainViewModel
{
    private bool _isHydratingCreatorGitHubState;

    [ObservableProperty]
    private GitHubSession? _creatorGitHubSession;

    [ObservableProperty]
    private bool _isCreatorGitHubLoginInProgress;

    [ObservableProperty]
    private string _creatorGitHubLoginStatus = "GitHub není připojený.";

    [ObservableProperty]
    private string _creatorGitHubLoginUrl = string.Empty;

    [ObservableProperty]
    private string _creatorGitHubRepositoryOwner = string.Empty;

    [ObservableProperty]
    private string _creatorGitHubRepositoryName = string.Empty;

    [ObservableProperty]
    private string _creatorGitHubRepositoryDescription = string.Empty;

    [ObservableProperty]
    private bool _creatorGitHubRepositoryPrivate;

    [ObservableProperty]
    private bool _isCreatorGitHubRepoOperationRunning;

    [ObservableProperty]
    private bool _isCreatorGitHubRepositoriesLoading;

    [ObservableProperty]
    private string _creatorGitHubRepositoryStatus = "GitHub repo zatím není vytvořené z launcheru.";

    [ObservableProperty]
    private string _creatorGitHubRepositoriesStatus = "GitHub repozitáře zatím nejsou načtené.";

    public ObservableCollection<GitHubRepositoryInfo> CreatorGitHubRepositories { get; } = new();

    public bool HasCreatorGitHubSession => CreatorGitHubSession?.IsAuthenticated == true;

    public string CreatorGitHubDisplayName => CreatorGitHubSession?.Profile?.DisplayName ?? "Nepřihlášen";

    public string CreatorGitHubAccountLabel => HasCreatorGitHubSession
        ? string.IsNullOrWhiteSpace(CreatorGitHubSession?.Scope)
            ? CreatorGitHubSession?.Profile?.Login ?? "Nepřihlášen"
            : $"{CreatorGitHubSession?.Profile?.Login} • {CreatorGitHubSession?.Scope}"
        : "GitHub session není aktivní";

    public bool HasCreatorGitHubLoginUrl => !string.IsNullOrWhiteSpace(CreatorGitHubLoginUrl);

    public bool CanCreatorGitHubLogin =>
        !IsCreatorGitHubLoginInProgress;

    public bool CanCreatorCreateGitHubRepository =>
        HasCreatorGitHubSession &&
        !IsCreatorGitHubRepoOperationRunning &&
        !string.IsNullOrWhiteSpace(ResolveCreatorGitHubRepositoryOwner()) &&
        !string.IsNullOrWhiteSpace(ResolveCreatorGitHubRepositoryName());

    public bool CanRefreshCreatorGitHubRepositories =>
        HasCreatorGitHubSession &&
        !IsCreatorGitHubRepositoriesLoading;

    public bool HasCreatorGitHubRepositories => CreatorGitHubRepositories.Count > 0;

    public bool CanUseCreatorGitHubRepository =>
        HasCreatorGitHubSession &&
        !IsCreatorGitHubRepoOperationRunning;

    public string CreatorGitHubCreateRepositoryLabel => IsCreatorGitHubRepoOperationRunning
        ? "Vytvářím repo..."
        : "Vytvořit repo + nastavit origin";

    public string CreatorGitHubRefreshRepositoriesLabel => IsCreatorGitHubRepositoriesLoading
        ? "Načítám repa..."
        : "Načíst repa";

    public string CreatorGitHubSuggestedRepositoryName => FirstNonEmpty(
        CurrentModpackCreatorManifest?.Slug,
        CreatorMetadataSlug,
        CurrentModpack?.Name);

    public string CreatorGitHubSuggestedDescription => FirstNonEmpty(
        CurrentModpackCreatorManifest?.Summary,
        CreatorMetadataSummary,
        CurrentModpack?.Description);

    public string CreatorGitHubRepositoryOwnerEffective => ResolveCreatorGitHubRepositoryOwner();

    public string CreatorGitHubRepositoryNameEffective => ResolveCreatorGitHubRepositoryName();

    public string CreatorGitHubRepositoryPreviewUrl =>
        string.IsNullOrWhiteSpace(CreatorGitHubRepositoryOwnerEffective) || string.IsNullOrWhiteSpace(CreatorGitHubRepositoryNameEffective)
            ? string.Empty
            : $"https://github.com/{CreatorGitHubRepositoryOwnerEffective}/{CreatorGitHubRepositoryNameEffective}";

    public bool HasCreatorGitHubRepositoryPreviewUrl => !string.IsNullOrWhiteSpace(CreatorGitHubRepositoryPreviewUrl);

    public string CreatorGitHubNewRepositoryUrl
    {
        get
        {
            if (string.IsNullOrWhiteSpace(CreatorGitHubRepositoryOwnerEffective) || string.IsNullOrWhiteSpace(CreatorGitHubRepositoryNameEffective))
            {
                return string.Empty;
            }

            var builder = new System.Text.StringBuilder("https://github.com/new");
            builder.Append("?owner=").Append(Uri.EscapeDataString(CreatorGitHubRepositoryOwnerEffective));
            builder.Append("&name=").Append(Uri.EscapeDataString(CreatorGitHubRepositoryNameEffective));

            var description = FirstNonEmpty(CreatorGitHubRepositoryDescription, CreatorGitHubSuggestedDescription);
            if (!string.IsNullOrWhiteSpace(description))
            {
                builder.Append("&description=").Append(Uri.EscapeDataString(description));
            }

            builder.Append(CreatorGitHubRepositoryPrivate ? "&visibility=private" : "&visibility=public");
            return builder.ToString();
        }
    }

    public bool HasCreatorGitHubNewRepositoryUrl => !string.IsNullOrWhiteSpace(CreatorGitHubNewRepositoryUrl);

    partial void OnCreatorGitStateChanged(CreatorGitStatus value)
    {
        NotifyCreatorGitHubStateChanged();
    }

    partial void OnCreatorGitHubSessionChanged(GitHubSession? value)
    {
        if (value?.IsAuthenticated == true && string.IsNullOrWhiteSpace(CreatorGitHubRepositoryOwner))
        {
            CreatorGitHubRepositoryOwner = value.Profile?.Login ?? string.Empty;
        }

        if (value?.IsAuthenticated == true)
        {
            CreatorGitHubLoginUrl = string.Empty;
        }

        NotifyCreatorGitHubStateChanged();
    }

    partial void OnIsCreatorGitHubLoginInProgressChanged(bool value) => NotifyCreatorGitHubStateChanged();

    partial void OnIsCreatorGitHubRepoOperationRunningChanged(bool value) => NotifyCreatorGitHubStateChanged();

    partial void OnCreatorGitHubRepositoryOwnerChanged(string value)
    {
        if (_isHydratingCreatorGitHubState)
        {
            NotifyCreatorGitHubStateChanged();
            return;
        }

        CreatorPreferences.GitHubLastRepositoryOwner = value?.Trim() ?? string.Empty;
        PersistCreatorPreferences();
        NotifyCreatorGitHubStateChanged();
    }

    partial void OnCreatorGitHubRepositoryNameChanged(string value) => NotifyCreatorGitHubStateChanged();

    partial void OnCreatorGitHubRepositoryDescriptionChanged(string value) => NotifyCreatorGitHubStateChanged();

    partial void OnCreatorGitHubRepositoryPrivateChanged(bool value)
    {
        if (_isHydratingCreatorGitHubState)
        {
            NotifyCreatorGitHubStateChanged();
            return;
        }

        CreatorPreferences.GitHubDefaultPrivateRepository = value;
        PersistCreatorPreferences();
        NotifyCreatorGitHubStateChanged();
    }

    partial void OnIsCreatorGitHubRepositoriesLoadingChanged(bool value) => NotifyCreatorGitHubStateChanged();

    private async Task InitializeGitHubSessionAsync()
    {
        try
        {
            var session = await _gitHubAuthService.LoadCachedSessionAsync();
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                _isHydratingCreatorGitHubState = true;
                try
                {
                    CreatorGitHubRepositoryOwner = CreatorPreferences.GitHubLastRepositoryOwner ?? string.Empty;
                    CreatorGitHubRepositoryPrivate = CreatorPreferences.GitHubDefaultPrivateRepository;
                    CreatorGitHubSession = session;
                    CreatorGitHubLoginStatus = session?.IsAuthenticated == true
                        ? $"GitHub aktivní: {session.Profile?.Login}"
                        : "GitHub není připojený.";

                    if (CreatorGitHubSession?.IsAuthenticated == true && string.IsNullOrWhiteSpace(CreatorGitHubRepositoryOwner))
                    {
                        CreatorGitHubRepositoryOwner = CreatorGitHubSession.Profile?.Login ?? string.Empty;
                    }
                }
                finally
                {
                    _isHydratingCreatorGitHubState = false;
                }

                NotifyCreatorGitHubStateChanged();
            });

            if (session?.IsAuthenticated == true)
            {
                await RefreshGitHubRepositoriesCoreAsync(showToastOnSuccess: false);
            }
        }
        catch (Exception ex)
        {
            LogService.Error("GitHub restore failed", ex);
        }
    }

    [RelayCommand]
    private async Task LoginGitHub()
    {
        if (IsCreatorGitHubLoginInProgress)
        {
            return;
        }

        IsCreatorGitHubLoginInProgress = true;
        CreatorGitHubLoginUrl = string.Empty;

        try
        {
            CreatorGitHubLoginStatus = "Spouštím GitHub OAuth přes VOID-CRAFT.EU API...";

            var loginAttempt = await _gitHubAuthService.StartOAuthLoginAsync();
            CreatorGitHubLoginUrl = loginAttempt.AuthorizeUrl;
            CreatorGitHubLoginStatus = "Otevírám GitHub autorizaci v prohlížeči...";

            try
            {
                OpenUrl(CreatorGitHubLoginUrl);
            }
            catch (Exception openBrowserEx)
            {
                LogService.Error("GitHub OAuth browser open failed", openBrowserEx);
                CreatorGitHubLoginStatus = "Prohlížeč se neotevřel automaticky. Použij tlačítko Otevřít znovu.";
            }

            var session = await _gitHubAuthService.CompleteOAuthLoginAsync(loginAttempt, message =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    CreatorGitHubLoginStatus = message;
                });
            });

            CreatorGitHubSession = session;
            CreatorGitHubLoginUrl = string.Empty;
            CreatorGitHubLoginStatus = $"GitHub přihlášení hotovo: {session.Profile?.Login}";
            if (string.IsNullOrWhiteSpace(CreatorGitHubRepositoryOwner))
            {
                CreatorGitHubRepositoryOwner = session.Profile?.Login ?? string.Empty;
            }

            await RefreshGitHubRepositoriesCoreAsync(showToastOnSuccess: false);

            ShowToast("GitHub", $"Přihlášen {session.Profile?.Login}", ToastSeverity.Success, 2600);
            TrackCreatorActivity($"GitHub přihlášení: {session.Profile?.Login}", "Git");
        }
        catch (Exception ex)
        {
            CreatorGitHubLoginStatus = $"GitHub login selhal: {ex.Message}";
            ShowToast("GitHub", ex.Message, ToastSeverity.Error, 4200);
        }
        finally
        {
            IsCreatorGitHubLoginInProgress = false;
            NotifyCreatorGitHubStateChanged();
        }
    }

    [RelayCommand]
    private async Task LogoutGitHub()
    {
        await _gitHubAuthService.LogoutAsync();
        CreatorGitHubSession = null;
        CreatorGitHubLoginStatus = "GitHub byl odhlášen.";
        CreatorGitHubLoginUrl = string.Empty;
        ReplaceCollectionItems(CreatorGitHubRepositories, Array.Empty<GitHubRepositoryInfo>());
        CreatorGitHubRepositoriesStatus = "GitHub repozitáře zatím nejsou načtené.";
        ShowToast("GitHub", "GitHub logout dokončen.", ToastSeverity.Success, 2200);
        NotifyCreatorGitHubStateChanged();
    }

    [RelayCommand]
    private async Task CreateGitHubRepository()
    {
        if (!CanCreatorCreateGitHubRepository)
        {
            return;
        }

        var workspacePath = SkinStudioSelectedInstancePath;
        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            ShowToast("GitHub", "Nejdřív vyber Creator workspace.", ToastSeverity.Warning, 3200);
            return;
        }

        var accessToken = CreatorGitHubSession?.AccessToken;
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            ShowToast("GitHub", "GitHub session není aktivní.", ToastSeverity.Warning, 3200);
            return;
        }

        IsCreatorGitHubRepoOperationRunning = true;
        try
        {
            if (!HasCreatorGitRepository)
            {
                var initialized = await _creatorGitService.InitRepositoryAsync(workspacePath);
                if (!initialized)
                {
                    throw new InvalidOperationException("Nepodařilo se inicializovat lokální git repository.");
                }
            }

            await RefreshCreatorGitStatus();

            var repository = await _gitHubAuthService.CreateRepositoryAsync(accessToken, new GitHubRepositoryCreationRequest
            {
                Owner = ResolveCreatorGitHubRepositoryOwner(),
                Name = ResolveCreatorGitHubRepositoryName(),
                Description = FirstNonEmpty(CreatorGitHubRepositoryDescription, CreatorGitHubSuggestedDescription),
                Homepage = CreatorBrandWebsite,
                IsPrivate = CreatorGitHubRepositoryPrivate,
                AutoInitializeWithReadme = false
            });

            if (CreatorGitState.HasRemote &&
                !string.IsNullOrWhiteSpace(CreatorGitState.RemoteUrl) &&
                !string.Equals(CreatorGitState.RemoteUrl.Trim(), repository.CloneUrl, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Origin remote už existuje a míří jinam. Přepiš ho ručně nebo nejdřív remote odpoj.");
            }

            var remoteSet = await _creatorGitService.SetRemoteOriginAsync(workspacePath, repository.CloneUrl);
            if (!remoteSet)
            {
                throw new InvalidOperationException("Repo bylo vytvořené, ale nepodařilo se nastavit origin remote.");
            }

            await PersistGitHubRepositoryMetadataAsync(workspacePath, repository);
            await RefreshGitHubRepositoriesCoreAsync(showToastOnSuccess: false, preferredRepositoryFullName: repository.FullName);

            CreatorGitHubRepositoryStatus = $"Repo připravené: {repository.FullName}";
            ShowToast("GitHub", $"Repo {repository.FullName} vytvořené a origin nastavený.", ToastSeverity.Success, 4200);
            TrackCreatorActivity($"GitHub repo vytvořené: {repository.FullName}", "Git");

            await RefreshCreatorGitStatus();
            await RefreshCreatorReleasePipeline();
        }
        catch (Exception ex)
        {
            CreatorGitHubRepositoryStatus = $"Create repo selhal: {ex.Message}";
            ShowToast("GitHub", ex.Message, ToastSeverity.Error, 5200);
        }
        finally
        {
            IsCreatorGitHubRepoOperationRunning = false;
            NotifyCreatorGitHubStateChanged();
        }
    }

    [RelayCommand]
    private async Task RefreshGitHubRepositories()
    {
        await RefreshGitHubRepositoriesCoreAsync(showToastOnSuccess: true);
    }

    [RelayCommand]
    private async Task UseGitHubRepository(GitHubRepositoryInfo? repository)
    {
        if (repository == null || IsCreatorGitHubRepoOperationRunning)
        {
            return;
        }

        var workspacePath = SkinStudioSelectedInstancePath;
        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            ShowToast("GitHub", "Nejdřív vyber Creator workspace.", ToastSeverity.Warning, 3200);
            return;
        }

        IsCreatorGitHubRepoOperationRunning = true;
        try
        {
            if (!HasCreatorGitRepository)
            {
                var initialized = await _creatorGitService.InitRepositoryAsync(workspacePath);
                if (!initialized)
                {
                    throw new InvalidOperationException("Nepodařilo se inicializovat lokální git repository.");
                }
            }

            await RefreshCreatorGitStatus();

            var remoteSet = await _creatorGitService.SetRemoteOriginAsync(workspacePath, repository.CloneUrl);
            if (!remoteSet)
            {
                throw new InvalidOperationException("Nepodařilo se nastavit origin remote na vybrané GitHub repo.");
            }

            ApplyGitHubRepositoryDraft(repository);
            await PersistGitHubRepositoryMetadataAsync(workspacePath, repository);

            CreatorGitHubRepositoryStatus = $"Origin nastavený na {repository.FullName}";
            ShowToast("GitHub", $"Origin přepnutý na {repository.FullName}.", ToastSeverity.Success, 3200);
            TrackCreatorActivity($"GitHub repo napojené: {repository.FullName}", "Git");

            await RefreshCreatorGitStatus();
            await RefreshCreatorReleasePipeline();
        }
        catch (Exception ex)
        {
            CreatorGitHubRepositoryStatus = $"Napojení repa selhalo: {ex.Message}";
            ShowToast("GitHub", ex.Message, ToastSeverity.Error, 5200);
        }
        finally
        {
            IsCreatorGitHubRepoOperationRunning = false;
            NotifyCreatorGitHubStateChanged();
        }
    }

    private async Task PersistGitHubRepositoryMetadataAsync(string workspacePath, GitHubRepositoryInfo repository)
    {
        var manifest = CurrentModpackCreatorManifest ?? _creatorManifestService.LoadManifest(workspacePath);

        CreatorBrandGitHub = repository.HtmlUrl;
        CreatorGitHubRepositoryOwner = repository.Owner;
        CreatorGitHubRepositoryName = repository.Name;

        if (manifest == null)
        {
            IsCreatorMetadataDirty = true;
            return;
        }

        manifest.RepositoryUrl = repository.HtmlUrl;
        manifest.BrandProfile ??= BuildCurrentBrandProfile();
        manifest.BrandProfile.GitHub = repository.HtmlUrl;
        var savedManifest = await _creatorManifestService.SaveManifestAsync(workspacePath, manifest);
        ApplyCreatorMetadata(savedManifest);
        RefreshCurrentModpackCreatorManifest();
    }

    private async Task RefreshGitHubRepositoriesCoreAsync(bool showToastOnSuccess, string? preferredRepositoryFullName = null)
    {
        var accessToken = CreatorGitHubSession?.AccessToken;
        if (string.IsNullOrWhiteSpace(accessToken) || IsCreatorGitHubRepositoriesLoading)
        {
            return;
        }

        IsCreatorGitHubRepositoriesLoading = true;
        try
        {
            var repositories = await _gitHubAuthService.ListAccessibleRepositoriesAsync(accessToken);
            repositories.Sort((left, right) => Nullable.Compare(right.UpdatedAtUtc, left.UpdatedAtUtc));

            ReplaceCollectionItems(CreatorGitHubRepositories, repositories);
            CreatorGitHubRepositoriesStatus = repositories.Count == 0
                ? "Účet zatím nemá žádná dostupná repa."
                : $"Načteno {repositories.Count} GitHub repozitářů.";

            var preferredRepository = !string.IsNullOrWhiteSpace(preferredRepositoryFullName)
                ? repositories.Find(repo => string.Equals(repo.FullName, preferredRepositoryFullName, StringComparison.OrdinalIgnoreCase))
                : null;

            if (preferredRepository != null)
            {
                ApplyGitHubRepositoryDraft(preferredRepository);
            }

            if (showToastOnSuccess)
            {
                ShowToast("GitHub", CreatorGitHubRepositoriesStatus, ToastSeverity.Success, 2400);
            }
        }
        catch (Exception ex)
        {
            CreatorGitHubRepositoriesStatus = $"Načtení rep selhalo: {ex.Message}";
            if (showToastOnSuccess)
            {
                ShowToast("GitHub", ex.Message, ToastSeverity.Error, 4200);
            }
        }
        finally
        {
            IsCreatorGitHubRepositoriesLoading = false;
            NotifyCreatorGitHubStateChanged();
        }
    }

    private void ApplyGitHubRepositoryDraft(GitHubRepositoryInfo repository)
    {
        CreatorGitHubRepositoryOwner = repository.Owner;
        CreatorGitHubRepositoryName = repository.Name;
        CreatorGitHubRepositoryDescription = repository.Description;
        CreatorGitHubRepositoryPrivate = repository.IsPrivate;
    }

    private string ResolveCreatorGitHubRepositoryOwner()
    {
        var manualOwner = CreatorGitHubRepositoryOwner?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(manualOwner))
        {
            return manualOwner;
        }

        if (_gitHubReleaseService.TryParseRepository(CreatorGitState.RemoteUrl, out var remoteRepository))
        {
            return remoteRepository.Owner;
        }

        if (_gitHubReleaseService.TryParseRepository(CurrentModpackCreatorManifest?.RepositoryUrl, out var manifestRepository))
        {
            return manifestRepository.Owner;
        }

        if (_gitHubReleaseService.TryParseRepository(CreatorBrandGitHub, out var brandRepository))
        {
            return brandRepository.Owner;
        }

        return CreatorGitHubSession?.Profile?.Login ?? string.Empty;
    }

    private string ResolveCreatorGitHubRepositoryName()
    {
        var manualName = CreatorGitHubRepositoryName?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(manualName))
        {
            return manualName;
        }

        if (_gitHubReleaseService.TryParseRepository(CreatorGitState.RemoteUrl, out var remoteRepository))
        {
            return remoteRepository.Repository;
        }

        if (_gitHubReleaseService.TryParseRepository(CurrentModpackCreatorManifest?.RepositoryUrl, out var manifestRepository))
        {
            return manifestRepository.Repository;
        }

        if (_gitHubReleaseService.TryParseRepository(CreatorBrandGitHub, out var brandRepository))
        {
            return brandRepository.Repository;
        }

        return CreatorGitHubSuggestedRepositoryName?.Trim() ?? string.Empty;
    }

    private void NotifyCreatorGitHubStateChanged()
    {
        OnPropertyChanged(nameof(HasCreatorGitHubSession));
        OnPropertyChanged(nameof(CreatorGitHubDisplayName));
        OnPropertyChanged(nameof(CreatorGitHubAccountLabel));
        OnPropertyChanged(nameof(HasCreatorGitHubLoginUrl));
        OnPropertyChanged(nameof(CanCreatorGitHubLogin));
        OnPropertyChanged(nameof(CanCreatorCreateGitHubRepository));
        OnPropertyChanged(nameof(CanRefreshCreatorGitHubRepositories));
        OnPropertyChanged(nameof(HasCreatorGitHubRepositories));
        OnPropertyChanged(nameof(CanUseCreatorGitHubRepository));
        OnPropertyChanged(nameof(CreatorGitHubCreateRepositoryLabel));
        OnPropertyChanged(nameof(CreatorGitHubRefreshRepositoriesLabel));
        OnPropertyChanged(nameof(CreatorGitHubSuggestedRepositoryName));
        OnPropertyChanged(nameof(CreatorGitHubSuggestedDescription));
        OnPropertyChanged(nameof(CreatorGitHubRepositoryOwnerEffective));
        OnPropertyChanged(nameof(CreatorGitHubRepositoryNameEffective));
        OnPropertyChanged(nameof(CreatorGitHubRepositoryPreviewUrl));
        OnPropertyChanged(nameof(HasCreatorGitHubRepositoryPreviewUrl));
        OnPropertyChanged(nameof(CreatorGitHubNewRepositoryUrl));
        OnPropertyChanged(nameof(HasCreatorGitHubNewRepositoryUrl));
    }
}