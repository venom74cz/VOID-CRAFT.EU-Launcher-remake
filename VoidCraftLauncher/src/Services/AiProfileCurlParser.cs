using System;
using System.Text.RegularExpressions;
using VoidCraftLauncher.Models;

namespace VoidCraftLauncher.Services;

public static class AiProfileCurlParser
{
    public static void ParseAndUpdateProfile(AiProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.RawCurlCommand))
            return;

        var cmd = profile.RawCurlCommand;

        // Extract URL
        var urlMatch = Regex.Match(cmd, @"curl\s+(?:-X\s+\w+\s+)?(?:""|')?(https?://[^\s""']+)(?:""|')?", RegexOptions.IgnoreCase);
        if (urlMatch.Success)
        {
            var rawUrl = urlMatch.Groups[1].Value;
            
            // Check for Gemini native endpoint translation
            if (rawUrl.Contains("generativelanguage.googleapis.com", StringComparison.OrdinalIgnoreCase))
            {
                profile.BaseUrl = "https://generativelanguage.googleapis.com/v1beta/openai/v1";
            }
            else
            {
                // Normalize URL to not include /chat/completions
                if (rawUrl.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
                    profile.BaseUrl = rawUrl.Substring(0, rawUrl.Length - "/chat/completions".Length);
                else
                    profile.BaseUrl = rawUrl;
            }
        }

        // Extract API Key
        // Bearer token
        var bearerMatch = Regex.Match(cmd, @"-H\s+(?:""|')?Authorization:\s+Bearer\s+([^""'\s]+)(?:""|')?", RegexOptions.IgnoreCase);
        if (bearerMatch.Success)
        {
            profile.ApiKey = bearerMatch.Groups[1].Value;
        }
        else
        {
            // Gemini X-goog-api-key
            var googMatch = Regex.Match(cmd, @"-H\s+(?:""|')?x-goog-api-key:\s+([^""'\s]+)(?:""|')?", RegexOptions.IgnoreCase);
            if (googMatch.Success)
            {
                profile.ApiKey = googMatch.Groups[1].Value;
            }
            else
            {
                // Generic api-key header
                var genericKeyMatch = Regex.Match(cmd, @"-H\s+(?:""|')?api-key:\s+([^""'\s]+)(?:""|')?", RegexOptions.IgnoreCase);
                if (genericKeyMatch.Success)
                    profile.ApiKey = genericKeyMatch.Groups[1].Value;
            }
        }

        // Clean up common copy-paste errors for keys (e.g. replacing variable content but keeping the ${...} wrapper)
        if (!string.IsNullOrEmpty(profile.ApiKey) && profile.ApiKey.StartsWith("${") && profile.ApiKey.EndsWith("}"))
        {
            profile.ApiKey = profile.ApiKey.Substring(2, profile.ApiKey.Length - 3);
        }

        // Extract Model
        // Match: "model": "gpt-4"
        var modelMatch = Regex.Match(cmd, @"""model""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
        if (modelMatch.Success)
        {
            profile.ModelName = modelMatch.Groups[1].Value;
        }
        else if (urlMatch.Success && urlMatch.Groups[1].Value.Contains("models/"))
        {
            // Gemini models logic: models/gemini-flash-latest:generateContent
            var urlPortion = urlMatch.Groups[1].Value;
            var mdMatch = Regex.Match(urlPortion, @"models/([^:]+)");
            if (mdMatch.Success)
                profile.ModelName = mdMatch.Groups[1].Value;
        }
    }
}
