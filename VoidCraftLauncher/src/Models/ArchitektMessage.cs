using CommunityToolkit.Mvvm.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;

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

    private static readonly Regex ThinkBlockRegex = new Regex(
        @"<think>(.*?)</think>",
        RegexOptions.Singleline | RegexOptions.Compiled);

    public string DisplayContent
    {
        get
        {
            var text = Content;
            if (string.IsNullOrEmpty(text)) return text;

            // 1) Completed <think>...</think> blocks → blockquote
            text = ThinkBlockRegex.Replace(text, m =>
            {
                var inner = m.Groups[1].Value.Trim();
                if (string.IsNullOrWhiteSpace(inner)) return "";
                // Convert to blockquote lines
                var lines = inner.Split('\n');
                var quoted = string.Join("\n", lines.Select(l => $"> {l}"));
                return $"> 🧠 **Úvaha AI:**\n{quoted}\n\n";
            });

            // 2) Unclosed <think> (still streaming thinking) → show as faded blockquote
            var openIdx = text.LastIndexOf("<think>");
            if (openIdx >= 0 && !text.Substring(openIdx).Contains("</think>"))
            {
                var before = text.Substring(0, openIdx);
                var thinkContent = text.Substring(openIdx + 7).Trim();
                if (!string.IsNullOrWhiteSpace(thinkContent))
                {
                    var lines = thinkContent.Split('\n');
                    var quoted = string.Join("\n", lines.Select(l => $"> {l}"));
                    text = before + $"> 🧠 *Přemýšlím...*\n{quoted}";
                }
                else
                {
                    text = before + "> 🧠 *Přemýšlím...*";
                }
            }

            // 3) Orphan </think> tag cleanup
            text = text.Replace("</think>", "");

            return text.TrimStart();
        }
    }

    /// <summary>"user" or "assistant"</summary>
    public string Role { get; set; } = "user";

    public bool IsUser => Role == "user";
    public bool IsAssistant => Role == "assistant";
}
