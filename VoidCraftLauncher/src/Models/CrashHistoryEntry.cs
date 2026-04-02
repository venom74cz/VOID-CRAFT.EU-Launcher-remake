using System;
using System.Globalization;
using System.IO;
using System.Text.Json.Serialization;

namespace VoidCraftLauncher.Models;

public sealed class CrashHistoryEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);

    public string ModpackName { get; set; } = string.Empty;

    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

    public int ExitCode { get; set; }

    public int RuntimeSeconds { get; set; }

    public string Summary { get; set; } = string.Empty;

    public string LogTail { get; set; } = string.Empty;

    public string? LogPath { get; set; }

    public string? CrashReportPath { get; set; }

    [JsonIgnore]
    public string OccurredAtLabel => OccurredAtUtc == default
        ? string.Empty
        : OccurredAtUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm", CultureInfo.CurrentCulture);

    [JsonIgnore]
    public string ExitCodeLabel => ExitCode.ToString(CultureInfo.InvariantCulture);

    [JsonIgnore]
    public string RuntimeLabel
    {
        get
        {
            var runtime = TimeSpan.FromSeconds(Math.Max(0, RuntimeSeconds));
            return runtime.TotalMinutes < 1
                ? $"{runtime.Seconds}s"
                : $"{(int)runtime.TotalMinutes}m {runtime.Seconds}s";
        }
    }

    [JsonIgnore]
    public bool HasLogTail => !string.IsNullOrWhiteSpace(LogTail);

    [JsonIgnore]
    public string? PreferredArtifactPath
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(CrashReportPath) && File.Exists(CrashReportPath))
            {
                return CrashReportPath;
            }

            if (!string.IsNullOrWhiteSpace(LogPath) && File.Exists(LogPath))
            {
                return LogPath;
            }

            return null;
        }
    }

    [JsonIgnore]
    public bool HasOpenableArtifact => !string.IsNullOrWhiteSpace(PreferredArtifactPath);
}