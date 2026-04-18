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

    partial void OnContentChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayContent));
    }

    public string DisplayContent => Content
        .Replace("<think>", "> 🧠 **Úvaha AI:**\n> _")
        .Replace("</think>", "_\n\n");

    /// <summary>"user" or "assistant"</summary>
    public string Role { get; set; } = "user";

    public bool IsUser => Role == "user";
    public bool IsAssistant => Role == "assistant";
}
