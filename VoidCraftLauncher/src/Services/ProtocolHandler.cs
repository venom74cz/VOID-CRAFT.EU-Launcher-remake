using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;

#pragma warning disable CA1416

namespace VoidCraftLauncher.Services;

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

    /// <summary>
    /// Checks if the app was launched via protocol and extracts auth code
    /// </summary>
    public static string? ExtractAuthCode(string[] args)
    {
        if (args.Length == 0) return null;

        var uri = args[0];
        if (!uri.StartsWith($"{ProtocolScheme}://", StringComparison.OrdinalIgnoreCase))
            return null;

        try
        {
            var parsedUri = new Uri(uri);
            var query = parsedUri.Query;
            
            if (query.Contains("code="))
            {
                var code = query.Substring(query.IndexOf("code=") + 5);
                if (code.Contains("&")) 
                    code = code.Substring(0, code.IndexOf("&"));
                return code;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to parse auth URI: {ex.Message}");
        }

        return null;
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

    private static string GetAuthCodeFilePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(appData, "VoidCraftLauncher");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "pending_auth_code.txt");
    }
}
