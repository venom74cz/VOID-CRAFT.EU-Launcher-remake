using System;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace VoidCraftLauncher.Services;

/// <summary>
/// Lightweight observability service for tracking operation timing,
/// fallback scenarios, and feed loading health across the launcher.
/// </summary>
public sealed class ObservabilityService
{
    private readonly ConcurrentDictionary<string, OperationMetrics> _metrics = new();

    /// <summary>Start a timed operation scope. Dispose the result to record duration.</summary>
    public OperationScope BeginOperation(string name)
    {
        return new OperationScope(this, name);
    }

    /// <summary>Record a successful operation completion with timing.</summary>
    public void RecordSuccess(string operation, TimeSpan duration)
    {
        var m = _metrics.GetOrAdd(operation, _ => new OperationMetrics());
        m.SuccessCount++;
        m.LastDuration = duration;
        m.LastSuccess = DateTime.UtcNow;

        StructuredLog.Event("Observability", $"{operation} succeeded", new
        {
            DurationMs = (int)duration.TotalMilliseconds,
            Total = m.SuccessCount
        });
    }

    /// <summary>Record a failed operation.</summary>
    public void RecordFailure(string operation, string reason, TimeSpan duration)
    {
        var m = _metrics.GetOrAdd(operation, _ => new OperationMetrics());
        m.FailureCount++;
        m.LastFailure = DateTime.UtcNow;
        m.LastFailureReason = reason;

        StructuredLog.Event("Observability", $"{operation} failed: {reason}", new
        {
            DurationMs = (int)duration.TotalMilliseconds,
            Failures = m.FailureCount
        }, "WARN");
    }

    /// <summary>Record a fallback scenario (e.g. WebSocket → polling, API → cache).</summary>
    public void RecordFallback(string operation, string primaryMethod, string fallbackMethod, string reason)
    {
        var m = _metrics.GetOrAdd(operation, _ => new OperationMetrics());
        m.FallbackCount++;

        StructuredLog.Event("Fallback", $"{operation}: {primaryMethod} → {fallbackMethod}", new
        {
            Reason = reason,
            FallbackTotal = m.FallbackCount
        }, "WARN");
    }

    /// <summary>Get metrics summary for a named operation.</summary>
    public OperationMetrics? GetMetrics(string operation)
    {
        return _metrics.TryGetValue(operation, out var m) ? m : null;
    }

    /// <summary>Get all tracked operation names and their metrics.</summary>
    public ConcurrentDictionary<string, OperationMetrics> GetAllMetrics() => _metrics;
}

public sealed class OperationMetrics
{
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public int FallbackCount { get; set; }
    public TimeSpan LastDuration { get; set; }
    public DateTime? LastSuccess { get; set; }
    public DateTime? LastFailure { get; set; }
    public string? LastFailureReason { get; set; }
}

/// <summary>Disposable scope for timing operations via using blocks.</summary>
public sealed class OperationScope : IDisposable
{
    private readonly ObservabilityService _service;
    private readonly string _name;
    private readonly Stopwatch _sw;
    private bool _completed;

    internal OperationScope(ObservabilityService service, string name)
    {
        _service = service;
        _name = name;
        _sw = Stopwatch.StartNew();
    }

    /// <summary>Mark the operation as successful before dispose.</summary>
    public void Complete()
    {
        _sw.Stop();
        _completed = true;
        _service.RecordSuccess(_name, _sw.Elapsed);
    }

    /// <summary>Mark as failed with a reason.</summary>
    public void Fail(string reason)
    {
        _sw.Stop();
        _completed = true;
        _service.RecordFailure(_name, reason, _sw.Elapsed);
    }

    public void Dispose()
    {
        if (!_completed)
        {
            _sw.Stop();
            _service.RecordFailure(_name, "Scope disposed without completion", _sw.Elapsed);
        }
    }
}
