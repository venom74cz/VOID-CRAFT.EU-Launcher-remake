using System;
using System.Collections.Generic;
using VoidCraftLauncher.ViewModels;

namespace VoidCraftLauncher.Services;

/// <summary>
/// Centralized navigation service with history stack and view routing.
/// Replaces scattered GoToXxx commands with a single Navigate(view) call.
/// </summary>
public sealed class NavigationService
{
    private readonly Stack<MainViewType> _history = new();
    private MainViewType _currentView = MainViewType.Dashboard;

    /// <summary>Fired when the active view changes. Subscribers update UI bindings.</summary>
    public event Action<MainViewType>? ViewChanged;

    /// <summary>Fired with (fromView, toView) for telemetry/observability.</summary>
    public event Action<MainViewType, MainViewType>? NavigationOccurred;

    public MainViewType CurrentView => _currentView;

    public bool CanGoBack => _history.Count > 0;

    /// <summary>Navigate to a new view, pushing the current one onto the history stack.</summary>
    public void Navigate(MainViewType target)
    {
        if (target == _currentView) return;

        var from = _currentView;
        _history.Push(_currentView);
        _currentView = target;

        ViewChanged?.Invoke(_currentView);
        NavigationOccurred?.Invoke(from, target);

        LogService.Log($"Navigate: {from} → {target} (depth={_history.Count})", "NAV");
    }

    /// <summary>Go back to the previous view. Returns false if history is empty.</summary>
    public bool GoBack()
    {
        if (_history.Count == 0) return false;

        var from = _currentView;
        _currentView = _history.Pop();

        ViewChanged?.Invoke(_currentView);
        NavigationOccurred?.Invoke(from, _currentView);

        LogService.Log($"GoBack: {from} → {_currentView} (depth={_history.Count})", "NAV");
        return true;
    }

    /// <summary>Clear history and jump to a root view (e.g. on app reset).</summary>
    public void NavigateRoot(MainViewType target)
    {
        var from = _currentView;
        _history.Clear();
        _currentView = target;

        ViewChanged?.Invoke(_currentView);
        NavigationOccurred?.Invoke(from, target);

        LogService.Log($"NavigateRoot: → {target}", "NAV");
    }
}
