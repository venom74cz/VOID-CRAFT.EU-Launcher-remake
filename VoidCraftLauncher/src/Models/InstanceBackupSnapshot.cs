using System;

namespace VoidCraftLauncher.Models;

public class InstanceBackupSnapshot
{
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public int FileCount { get; set; }
    public string Summary { get; set; } = "";
}