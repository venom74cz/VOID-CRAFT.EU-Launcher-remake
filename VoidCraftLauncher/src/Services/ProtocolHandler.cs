using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

#pragma warning disable CA1416

namespace VoidCraftLauncher.Services;

public sealed class ProtocolLaunchRequest
{
    public string? AuthCode { get; init; }
    public ProtocolInstallRequest? InstallRequest { get; init; }
}

public sealed class ProtocolInstallRequest
{
    public string Source { get; init; } = "registry";
    public string Slug { get; init; } = "";
    public string Version { get; init; } = "";
    public string DownloadUrl { get; init; } = "";
    public string ProjectName { get; init; } = "";
}

public static class ProtocolHandler
{
    private const string ProtocolScheme = "voidcraft";
    private const string ProtocolName = "VoidCraft Launcher Protocol";

    /// <summary>
    /// Registers the voidcraft:// protocol handler in Windows Registry (current user)
    /// </summary>
    public static void RegisterProtocol()
    {
        try
        {
            var exePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exePath)) return;

            // Use HKEY_CURRENT_USER to avoid admin rights
            using var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProtocolScheme}");
            if (key == null) return;

            key.SetValue("", $"URL:{ProtocolName}");
            key.SetValue("URL Protocol", "");

            using var iconKey = key.CreateSubKey("DefaultIcon");
            iconKey?.SetValue("", $"\"{exePath}\",1");

            using var shellKey = key.CreateSubKey(@"shell\open\command");
            shellKey?.SetValue("", $"\"{exePath}\" \"%1\"");

            Debug.WriteLine($"Protocol {ProtocolScheme}:// registered successfully");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to register protocol: {ex.Message}");
        }
    }

    public static ProtocolLaunchRequest? ParseLaunchRequest(string[] args)
    {
        if (args.Length == 0) return null;

        var uriText = args[0];
        if (!uriText.StartsWith($"{ProtocolScheme}://", StringComparison.OrdinalIgnoreCase))
            return null;

        try
        {
            var parsedUri = new Uri(uriText);
            var query = ParseQuery(parsedUri.Query);

            if (query.TryGetValue("code", out var code) && !string.IsNullOrWhiteSpace(code))
            {
                return new ProtocolLaunchRequest
                {
                    AuthCode = code.Trim()
                };
            }

            var action = parsedUri.Host;
            if (string.IsNullOrWhiteSpace(action))
            {
                action = parsedUri.AbsolutePath.Trim('/');
            }

            if (string.Equals(action, "install", StringComparison.OrdinalIgnoreCase))
            {
                if (!query.TryGetValue("url", out var downloadUrl) || string.IsNullOrWhiteSpace(downloadUrl))
                {
                    return null;
                }

                return new ProtocolLaunchRequest
                {
                    InstallRequest = new ProtocolInstallRequest
                    {
                        Source = GetQueryValue(query, "source", "registry"),
                        Slug = GetQueryValue(query, "slug"),
                        Version = GetQueryValue(query, "version"),
                        DownloadUrl = downloadUrl.Trim(),
                        ProjectName = GetQueryValue(query, "name")
                    }
                };
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to parse protocol URI: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Checks if the app was launched via protocol and extracts auth code
    /// </summary>
    public static string? ExtractAuthCode(string[] args)
    {
        return ParseLaunchRequest(args)?.AuthCode;
    }

    /// <summary>
    /// Writes auth code to a temp file for the running instance to pick up
    /// </summary>
    public static void WriteAuthCodeToFile(string code)
    {
        var path = GetAuthCodeFilePath();
        File.WriteAllText(path, code);
    }

    /// <summary>
    /// Reads auth code from temp file if exists
    /// </summary>
    public static string? ReadAuthCodeFromFile()
    {
        var path = GetAuthCodeFilePath();
        if (!File.Exists(path)) return null;

        try
        {
            var code = File.ReadAllText(path);
            File.Delete(path); // Clean up after reading
            return string.IsNullOrWhiteSpace(code) ? null : code;
        }
        catch
        {
            return null;
        }
    }

    public static void WriteInstallRequestToFile(ProtocolInstallRequest request)
    {
        var path = GetInstallRequestFilePath();
        File.WriteAllText(path, JsonSerializer.Serialize(request));
    }

    public static ProtocolInstallRequest? ReadInstallRequestFromFile()
    {
        var path = GetInstallRequestFilePath();
        if (!File.Exists(path)) return null;

        try
        {
            var json = File.ReadAllText(path);
            File.Delete(path);
            if (string.IsNullOrWhiteSpace(json)) return null;

            return JsonSerializer.Deserialize<ProtocolInstallRequest>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return null;
        }
    }

    private static string GetAuthCodeFilePath()
    {
        var dir = GetProtocolWorkingDirectory();
        return Path.Combine(dir, "pending_auth_code.txt");
    }

    private static string GetInstallRequestFilePath()
    {
        var dir = GetProtocolWorkingDirectory();
        return Path.Combine(dir, "pending_install_request.json");
    }

    private static string GetProtocolWorkingDirectory()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(appData, "VoidCraftLauncher");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(query))
        {
            return result;
        }

        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separatorIndex = pair.IndexOf('=');
            if (separatorIndex < 0)
            {
                result[DecodeQueryValue(pair)] = string.Empty;
                continue;
            }

            var key = DecodeQueryValue(pair[..separatorIndex]);
            var value = DecodeQueryValue(pair[(separatorIndex + 1)..]);
            result[key] = value;
        }

        return result;
    }

    private static string GetQueryValue(IReadOnlyDictionary<string, string> query, string key, string fallback = "")
    {
        return query.TryGetValue(key, out var value) ? value : fallback;
    }

    private static string DecodeQueryValue(string value)
    {
        return Uri.UnescapeDataString(value.Replace("+", " "));
    }
}
