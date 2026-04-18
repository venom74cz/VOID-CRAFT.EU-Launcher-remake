using CommunityToolkit.Mvvm.ComponentModel;

namespace VoidCraftLauncher.Models;

/// <summary>
/// Single message in the ARCHITEKT AI chat history.
/// Extends ObservableObject so streaming chunk appends update the UI in real time.
/// </summary>
public partial class ArchitektMessage : ObservableObject
{
    [ObservableProperty]
    private string _content = string.Empty;

    /// <summary>"user" or "assistant"</summary>
    public string Role { get; set; } = "user";

    public bool IsUser => Role == "user";
    public bool IsAssistant => Role == "assistant";
}
