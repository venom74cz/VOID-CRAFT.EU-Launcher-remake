using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace VoidCraftLauncher.Services
{
    public class CurseForgeApi
    {
        private readonly string _apiKey = "$2a$10$u61J7NMsbxMK38t0PNiwo.qOQt.vu7zUS1u.l0U8Cz4aFLnCv8DNa";
        private readonly HttpClient _httpClient;

        public CurseForgeApi()
        {
            _httpClient = new HttpClient();
            _httpClient.BaseAddress = new Uri("https://api.curseforge.com/");
            _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        }

        public async Task<string> GetModpackInfoAsync(int modpackId)
        {
            var response = await _httpClient.GetAsync($"v1/mods/{modpackId}");
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"CurseForge API error ({response.StatusCode}): {error}");
            }

            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> GetModpackFilesAsync(int modpackId)
        {
            var response = await _httpClient.GetAsync($"v1/mods/{modpackId}/files");
            
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"CurseForge API error: {response.StatusCode}");
            }

            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> GetModpackBySlugAsync(string slug)
        {
            var response = await _httpClient.GetAsync($"v1/mods/search?gameId=432&classId=4471&slug={slug}");
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> SearchModpacksAsync(string query, int index = 0)
        {
            // Sort by Popularity (2) descending, grab 50 items
            var url = $"v1/mods/search?gameId=432&classId=4471&sortField=2&sortOrder=desc&pageSize=50&index={index}";
            if (!string.IsNullOrWhiteSpace(query))
                url += $"&searchFilter={Uri.EscapeDataString(query)}";
                
            var response = await _httpClient.GetAsync(url);
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> GetProjectDescriptionAsync(int modId)
        {
            try
            {
                var html = await GetProjectDescriptionHtmlAsync(modId);
                if (!string.IsNullOrWhiteSpace(html))
                {
                    var htmlDoc = new HtmlAgilityPack.HtmlDocument();
                    htmlDoc.LoadHtml(html);
                    return System.Net.WebUtility.HtmlDecode(htmlDoc.DocumentNode.InnerText).Trim();
                }
            }
            catch { }
            return "";
        }

        public async Task<string> GetProjectDescriptionHtmlAsync(int modId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"v1/mods/{modId}/description");
                if (!response.IsSuccessStatusCode) return "";

                var json = await response.Content.ReadAsStringAsync();
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("data", out var dataElement))
                {
                    return dataElement.GetString() ?? "";
                }
            }
            catch { }

            return "";
        }

        /// <summary>
        /// Search for individual mods (classId=6) for custom profile mod browser.
        /// Optional gameVersion filter for MC version targeting.
        /// </summary>
        public async Task<string> SearchModsAsync(string query, string? gameVersion = null, string? modLoaderType = null, int offset = 0)
        {
            var url = $"v1/mods/search?gameId=432&classId=6&sortField=2&sortOrder=desc&pageSize=50&index={offset}&searchFilter={Uri.EscapeDataString(query)}";
            if (!string.IsNullOrEmpty(gameVersion))
                url += $"&gameVersion={Uri.EscapeDataString(gameVersion)}";
            if (!string.IsNullOrEmpty(modLoaderType))
            {
                // CurseForge modLoaderType: 1=Forge, 4=Fabric, 6=NeoForge
                var loaderTypeId = modLoaderType.ToLowerInvariant() switch
                {
                    "forge" => "1",
                    "fabric" => "4",
                    "neoforge" => "6",
                    _ => ""
                };
                if (!string.IsNullOrEmpty(loaderTypeId))
                    url += $"&modLoaderType={loaderTypeId}";
            }
            var response = await _httpClient.GetAsync(url);
            return await response.Content.ReadAsStringAsync();
        }

        /// <summary>
        /// Get latest file for a mod, matching given MC version and mod loader.
        /// </summary>
        public async Task<string> GetModFilesAsync(int modId, string? gameVersion = null)
        {
            var url = $"v1/mods/{modId}/files?sortField=1&sortOrder=desc";
            if (!string.IsNullOrEmpty(gameVersion))
                url += $"&gameVersion={Uri.EscapeDataString(gameVersion)}";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                throw new Exception($"CurseForge API error: {response.StatusCode}");
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> GetModFileAsync(int modId, int fileId)
        {
            var response = await _httpClient.GetAsync($"v1/mods/{modId}/files/{fileId}");

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"CurseForge API error ({response.StatusCode}): {error}");
            }

            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string?> GetFileDownloadUrlAsync(int modId, int fileId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"v1/mods/{modId}/files/{fileId}/download-url");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var node = System.Text.Json.Nodes.JsonNode.Parse(json);
                    return node?["data"]?.ToString();
                }
            }
            catch { }
            return null;
        }

        public async Task<string> GetFilesAsync(IEnumerable<int> fileIds)
        {
            var json = JsonSerializer.Serialize(new { fileIds = fileIds });
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync("v1/mods/files", content);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"CurseForge API error: {response.StatusCode}");
            }

            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> GetModsAsync(IEnumerable<int> modIds)
        {
            var json = JsonSerializer.Serialize(new { modIds = modIds });
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync("v1/mods", content);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"CurseForge API error: {response.StatusCode}");
            }

            return await response.Content.ReadAsStringAsync();
        }
    }
}
