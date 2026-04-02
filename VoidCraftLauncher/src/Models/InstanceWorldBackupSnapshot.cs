using System;

namespace VoidCraftLauncher.Models;

public sealed class InstanceWorldBackupSnapshot
{
    public string WorldName { get; set; } = "";

    public string Name { get; set; } = "";

    public string FullPath { get; set; } = "";

    public DateTime CreatedAt { get; set; }

    public int FileCount { get; set; }

    public string Summary { get; set; } = "";
}