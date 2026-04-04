using System;
using System.Collections.Concurrent;
using System.Net.Http;
using VoidCraftLauncher.Services.CreatorStudio;

namespace VoidCraftLauncher.Services;

/// <summary>
/// Lightweight service locator / DI container for the launcher.
/// Registers all services at startup and provides typed resolution.
/// No external NuGet required — keeps the dependency footprint small.
/// </summary>
public sealed class ServiceLocator
{
    private static ServiceLocator? _instance;
    public static ServiceLocator Current => _instance ?? throw new InvalidOperationException("ServiceLocator not initialized. Call Initialize() first.");

    private readonly ConcurrentDictionary<Type, object> _singletons = new();
    private readonly ConcurrentDictionary<Type, Func<object>> _factories = new();

    private ServiceLocator() { }

    /// <summary>Initialize the global service locator and register all services.</summary>
    public static ServiceLocator Initialize()
    {
        _instance = new ServiceLocator();
        _instance.RegisterServices();
        return _instance;
    }

    /// <summary>Register a pre-created singleton instance.</summary>
    public void Register<T>(T instance) where T : class
    {
        _singletons[typeof(T)] = instance;
    }

    /// <summary>Register a factory for lazy/transient creation.</summary>
    public void RegisterFactory<T>(Func<T> factory) where T : class
    {
        _factories[typeof(T)] = () => factory();
    }

    /// <summary>Resolve a registered service. Throws if not found.</summary>
    public T Resolve<T>() where T : class
    {
        if (_singletons.TryGetValue(typeof(T), out var singleton))
            return (T)singleton;

        if (_factories.TryGetValue(typeof(T), out var factory))
        {
            var instance = (T)factory();
            // Promote to singleton on first resolve
            _singletons[typeof(T)] = instance;
            return instance;
        }

        throw new InvalidOperationException($"Service {typeof(T).Name} is not registered.");
    }

    /// <summary>Try to resolve; returns null if not registered.</summary>
    public T? TryResolve<T>() where T : class
    {
        if (_singletons.TryGetValue(typeof(T), out var singleton))
            return (T)singleton;

        if (_factories.TryGetValue(typeof(T), out var factory))
        {
            var instance = (T)factory();
            _singletons[typeof(T)] = instance;
            return (T)instance;
        }

        return null;
    }

    // ──────────────────────────────────────────────
    //  Central registration — single source of truth
    // ──────────────────────────────────────────────
    private void RegisterServices()
    {
        // Shared HttpClient (pooled, long-lived)
        var httpClient = new HttpClient();
        var launcherVersion = typeof(ServiceLocator).Assembly.GetName().Version?.ToString(3) ?? "3.1.8";
        httpClient.DefaultRequestHeaders.Add("User-Agent", $"VoidCraftLauncher/{launcherVersion}");
        Register(httpClient);

        // Observability
        Register(new ObservabilityService());
        Register(new LocalizationService());

        // Core services
        Register(new NavigationService());
        var secureStorage = new SecureStorageService();
        Register(secureStorage);
        Register(new AuthService(secureStorage));
        Register(new LauncherService());
        Register(new CurseForgeApi());
        Register(new ModrinthApi());
        Register(new DiscordRpcService());

        // Services with dependencies
        RegisterFactory(() => new ModpackInstaller(Resolve<CurseForgeApi>()));
        RegisterFactory(() => new ServerStatusService(Resolve<HttpClient>(), Resolve<ObservabilityService>()));
        RegisterFactory(() => new SkinStudioService(Resolve<HttpClient>()));
        RegisterFactory(() => new SocialFeedService(Resolve<HttpClient>(), Resolve<LauncherService>(), Resolve<ObservabilityService>()));
        RegisterFactory(() => new CreatorWorkbenchService());
        RegisterFactory(() => new CreatorWorkbenchEditorService(Resolve<LauncherService>()));
        RegisterFactory(() => new CreatorWorkspaceService(Resolve<LauncherService>()));
        RegisterFactory(() => new CreatorAssetsService());
        RegisterFactory(() => new CreatorManifestService(Resolve<CreatorWorkspaceService>(), Resolve<CreatorAssetsService>()));
        RegisterFactory(() => new CreatorGitService());
        RegisterFactory(() => new CreatorNotesService());
        RegisterFactory(() => new CreatorReleaseService(Resolve<CreatorManifestService>(), Resolve<LauncherService>()));
        RegisterFactory(() => new AchievementHubService(Resolve<HttpClient>(), Resolve<LauncherService>(), Resolve<ObservabilityService>()));
        RegisterFactory(() => new ServerDiscoveryService(Resolve<LauncherService>(), Resolve<ObservabilityService>()));
        RegisterFactory(() => new InstanceExportService(Resolve<CurseForgeApi>(), Resolve<ModrinthApi>()));
    }
}
