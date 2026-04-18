using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text.RegularExpressions;
using VoidCraftLauncher.Services;

namespace VoidCraftLauncher.Agent;

public static class ArchitektAgentTools
{
    private static readonly HttpClient _httpClient = new();

    public static string ReadWorkspaceFile(string relativePath, string modpackPath, int maxContextChars = 8000)
    {
        try
        {
            var absolutePath = Path.Combine(modpackPath, relativePath);
            if (!File.Exists(absolutePath)) return "Chyba: Soubor neexistuje.";
            
            var content = File.ReadAllText(absolutePath);
            if (content.Length > maxContextChars) return content.Substring(0, maxContextChars) + "\n...[oříznuto]";
            return content;
        }
        catch (Exception ex)
        {
            return $"Chyba při čtení: {ex.Message}";
        }
    }

    public static string ListWorkspaceDirectory(string relativePath, string modpackPath)
    {
        try
        {
            var absolutePath = Path.Combine(modpackPath, relativePath ?? "");
            if (!Directory.Exists(absolutePath))
                return $"Chyba: Složka '{relativePath}' neexistuje.";

            var entries = new System.Text.StringBuilder();
            entries.AppendLine($"Obsah složky: {relativePath ?? "/"}");
            entries.AppendLine("---");

            var allDirs = Directory.GetDirectories(absolutePath);
            var allFiles = Directory.GetFiles(absolutePath);
            var totalCount = allDirs.Length + allFiles.Length;
            
            int count = 0;
            const int maxItems = 100;

            foreach (var dir in allDirs)
            {
                if (count++ >= maxItems) break;
                entries.AppendLine($"[DIR]  {Path.GetFileName(dir)}/");
            }

            foreach (var file in allFiles)
            {
                if (count++ >= maxItems) break;
                entries.AppendLine($"[FILE] {Path.GetFileName(file)}");
            }

            if (totalCount > maxItems)
            {
                entries.AppendLine($"---");
                entries.AppendLine($"... a dalších {totalCount - maxItems} souborů/složek.");
                entries.AppendLine($"DOPORUČENÍ: Složka je příliš velká. Pokud hledáš něco konkrétního, zkus podadresář nebo se zeptej uživatele na název.");
            }

            var result = entries.ToString();
            return result;
        }
        catch (Exception ex)
        {
            return $"Chyba při výpisu: {ex.Message}";
        }
    }

    public static string WriteWorkspaceFile(string relativePath, string content, string modpackPath)
    {
        try
        {
            var absolutePath = Path.Combine(modpackPath, relativePath);
            var dir = Path.GetDirectoryName(absolutePath);
            if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            
            File.WriteAllText(absolutePath, content);
            return "Soubor úspěšně zapsán/upraven.";
        }
        catch (Exception ex)
        {
            return $"Chyba při zápisu: {ex.Message}";
        }
    }

    public static string GetSystemInfo()
    {
        return $"OS: {Environment.OSVersion}\n" +
               $"64Bit: {Environment.Is64BitOperatingSystem}\n" +
               $"Cores: {Environment.ProcessorCount}\n" +
               $"Username: {Environment.UserName}\n" +
               $".NET: {Environment.Version}";
    }

    public static async Task<string> SearchWebAsync(string query)
    {
        // Simple DuckDuckGo HTML scraping fallback for MVP, or DuckDuckGo Lite
        try
        {
            var url = $"https://html.duckduckgo.com/html/?q={Uri.EscapeDataString(query)}";
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
            var response = await _httpClient.GetStringAsync(url);
            
            // Extract a snippets
            var regex = new Regex("<a class=\"result__snippet[^>]*>(.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            var matches = regex.Matches(response);
            
            var result = "Web Search Results:\\n";
            int count = 0;
            foreach (Match m in matches)
            {
                if (count++ > 5) break; // top 5
                var rawText = Regex.Replace(m.Groups[1].Value, "<.*?>", "");
                result += $"- {System.Net.WebUtility.HtmlDecode(rawText)}\n";
            }
            if (count == 0) return "No results found.";
            return result;
        }
        catch (Exception ex)
        {
            return $"Hledání selhalo: {ex.Message}";
        }
    }
}
