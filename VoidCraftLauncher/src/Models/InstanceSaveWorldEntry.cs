using System;
using System.Collections.Generic;

namespace VoidCraftLauncher.Models;

public sealed class InstanceSaveWorldEntry
{
    public string Name { get; set; } = "";

    public string FullPath { get; set; } = "";

    public DateTime LastModifiedAt { get; set; }

    public int FileCount { get; set; }

    public string Summary { get; set; } = "";

    public List<InstanceWorldBackupSnapshot> Backups { get; set; } = new();

    public bool HasBackups => Backups.Count > 0;

    public string BackupStatusLabel => Backups.Count switch
    {
        0 => "Bez zálohy",
        1 => $"1 záloha • poslední {Backups[0].CreatedAt:dd.MM.yyyy HH:mm}",
        _ => $"{Backups.Count} záloh • poslední {Backups[0].CreatedAt:dd.MM.yyyy HH:mm}"
    };
}