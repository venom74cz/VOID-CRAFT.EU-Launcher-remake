using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VoidCraftLauncher.Services;

/// <summary>
/// Structured logging extension for the launcher.
/// Wraps existing LogService with structured JSON event logging
/// for observability, diagnostics and telemetry export.
/// </summary>
public static class StructuredLog
{
    private static string? _structuredLogPath;
    private static readonly object _lock = new();
    private static readonly ConcurrentQueue<LogEvent> _recentEvents = new();
    private const int MaxRecentEvents = 200;

    /// <summary>Initialize structured logging alongside the existing flat logger.</summary>
    public static void Initialize(string basePath)
    {
        _structuredLogPath = Path.Combine(basePath, "launcher_structured.jsonl");

        // Rotate if > 2 MB
        if (File.Exists(_structuredLogPath) && new FileInfo(_structuredLogPath).Length > 2 * 1024 * 1024)
        {
            var backup = Path.Combine(basePath, "launcher_structured_prev.jsonl");
            try
            {
                if (File.Exists(backup)) File.Delete(backup);
                File.Move(_structuredLogPath, backup);
            }
            catch { /* best effort */ }
        }
    }

    /// <summary>Log a structured event with category, message, and optional data fields.</summary>
    public static void Event(string category, string message, object? data = null, string level = "INFO")
    {
        var logEvent = new LogEvent
        {
            Timestamp = DateTime.UtcNow,
            Level = level,
            Category = category,
            Message = message,
            Data = data
        };

        // Keep in-memory ring buffer
        _recentEvents.Enqueue(logEvent);
        while (_recentEvents.Count > MaxRecentEvents)
            _recentEvents.TryDequeue(out _);

        // Write to JSONL file
        if (_structuredLogPath != null)
        {
            try
            {
                var json = JsonSerializer.Serialize(logEvent, LogEventContext.Default.LogEvent);
                lock (_lock)
                {
                    File.AppendAllText(_structuredLogPath, json + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch { /* non-critical */ }
        }

        // Also forward to existing flat logger
        var flat = data != null ? $"[{category}] {message} | {JsonSerializer.Serialize(data)}" : $"[{category}] {message}";
        LogService.Log(flat, level);
    }

    /// <summary>Log an error event with exception details.</summary>
    public static void Error(string category, string message, Exception? ex = null)
    {
        Event(category, message, ex != null ? new { Error = ex.Message, Stack = ex.StackTrace?[..Math.Min(ex.StackTrace?.Length ?? 0, 500)] } : null, "ERROR");
    }

    /// <summary>Get recent events for diagnostics panel display.</summary>
    public static LogEvent[] GetRecentEvents() => _recentEvents.ToArray();
}

public sealed class LogEvent
{
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = "INFO";
    public string Category { get; set; } = "";
    public string Message { get; set; } = "";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Data { get; set; }
}

[JsonSerializable(typeof(LogEvent))]
internal partial class LogEventContext : JsonSerializerContext { }
