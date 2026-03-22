using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace VoidCraftLauncher.Services;

/// <summary>
/// Provides encrypted storage for sensitive data (tokens, credentials) using DPAPI.
/// Data is encrypted at rest and tied to the current Windows user via <see cref="ProtectedData"/>.
/// Falls back to plaintext on non-Windows platforms with a warning.
/// </summary>
public class SecureStorageService
{
    private readonly string _storagePath;

    public SecureStorageService()
    {
        _storagePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            ".voidcraft", "secure_store.dat");
    }

    /// <summary>
    /// Stores a value encrypted with DPAPI under the given key.
    /// </summary>
    public void Set(string key, string value)
    {
        var store = LoadStore();
        var encrypted = Protect(Encoding.UTF8.GetBytes(value));
        store[key] = Convert.ToBase64String(encrypted);
        SaveStore(store);
    }

    public Task SetAsync(string key, string value)
    {
        Set(key, value);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Retrieves and decrypts a value by key. Returns null if not found.
    /// </summary>
    public string? Get(string key)
    {
        var store = LoadStore();
        if (!store.TryGetValue(key, out var b64))
            return null;

        try
        {
            var encrypted = Convert.FromBase64String(b64);
            var decrypted = Unprotect(encrypted);
            return Encoding.UTF8.GetString(decrypted);
        }
        catch (Exception ex)
        {
            LogService.Error($"SecureStorage: failed to decrypt key '{key}'", ex);
            return null;
        }
    }

    public Task<string?> GetAsync(string key) => Task.FromResult(Get(key));

    /// <summary>
    /// Removes a key from the store.
    /// </summary>
    public void Remove(string key)
    {
        var store = LoadStore();
        if (store.Remove(key))
            SaveStore(store);
    }

    public Task RemoveAsync(string key)
    {
        Remove(key);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Checks whether a key exists in the store.
    /// </summary>
    public bool ContainsKey(string key)
    {
        return LoadStore().ContainsKey(key);
    }

    public Task<bool> ContainsKeyAsync(string key) => Task.FromResult(ContainsKey(key));

    private Dictionary<string, string> LoadStore()
    {
        if (!File.Exists(_storagePath))
            return new();

        try
        {
            var json = File.ReadAllText(_storagePath);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
        }
        catch
        {
            return new();
        }
    }

    private void SaveStore(Dictionary<string, string> store)
    {
        var dir = Path.GetDirectoryName(_storagePath);
        if (dir != null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(store, new JsonSerializerOptions { WriteIndented = false });
        File.WriteAllText(_storagePath, json);
    }

    private static byte[] Protect(byte[] data)
    {
        if (OperatingSystem.IsWindows())
            return ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);

        // Fallback: no encryption on non-Windows (log warning)
        LogService.Log("SecureStorage: DPAPI not available, storing as-is.");
        return data;
    }

    private static byte[] Unprotect(byte[] data)
    {
        if (OperatingSystem.IsWindows())
            return ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);

        return data;
    }
}
