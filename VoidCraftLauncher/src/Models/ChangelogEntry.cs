namespace VoidCraftLauncher.Models;

using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public partial class ChangelogEntry : ObservableObject
{
    public string Version { get; set; } = "";
    public string Date { get; set; } = "";
    public string Title { get; set; } = "";
    public List<string> Items { get; set; } = new();

    [ObservableProperty]
    private bool _isExpanded;

    public string Summary => Items.Count > 0
        ? string.Join("\n", Items.Take(3)) + (Items.Count > 3 ? $"\n… a dalších {Items.Count - 3}" : "")
        : Title;

    public string FullText => Items.Count > 0
        ? string.Join("\n", Items)
        : Title;

    [RelayCommand]
    private void ToggleExpand() => IsExpanded = !IsExpanded;
}
