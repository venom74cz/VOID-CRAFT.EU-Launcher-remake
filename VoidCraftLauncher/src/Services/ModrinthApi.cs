using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace VoidCraftLauncher.Services
{
    public class ModrinthApi
    {
        private readonly HttpClient _httpClient;

        public ModrinthApi()
        {
            _httpClient = new HttpClient();
            _httpClient.BaseAddress = new Uri("https://api.modrinth.com/v2/");
            // Modrinth requires a specific User-Agent format: User-Agent: AppName/Version (Contact)
            var launcherVersion = typeof(ModrinthApi).Assembly.GetName().Version?.ToString(3) ?? "3.1.8";
            _httpClient.DefaultRequestHeaders.Add("User-Agent", $"VoidCraftLauncher/{launcherVersion} (admin@void-craft.eu)");
        }

        public async Task<string> SearchModpacksAsync(string query, int offset = 0)
        {
            // Facet for project_type=modpack: [["project_type:modpack"]]
            var facets = Uri.EscapeDataString("[[\"project_type:modpack\"]]");
            var url = $"search?facets={facets}&index=downloads&limit=50&offset={offset}";
            
            if (!string.IsNullOrWhiteSpace(query))
                url += $"&query={Uri.EscapeDataString(query)}";
            
            // Sort by downloads by default for popularity and fetch up to 50 results
            var response = await _httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                 throw new Exception($"Modrinth API error: {response.StatusCode}");
            }
            
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> GetProjectDescriptionAsync(string projectId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"project/{projectId}");
                if (!response.IsSuccessStatusCode) return "";

                var json = await response.Content.ReadAsStringAsync();
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("body", out var bodyElement))
                {
                    return bodyElement.GetString() ?? "";
                }
            }
            catch { }
            return "";
        }

        /// <summary>
        /// Search for individual mods on Modrinth for custom profile mod browser.
        /// </summary>
        public async Task<string> SearchModsAsync(string query, string? gameVersion = null, string? modLoader = null, int offset = 0)
        {
            // Build facets: [["project_type:mod"]]
            var facetParts = new System.Collections.Generic.List<string> { "[\"project_type:mod\"]" };
            if (!string.IsNullOrEmpty(gameVersion))
                facetParts.Add($"[\"versions:{gameVersion}\"]");
            if (!string.IsNullOrEmpty(modLoader))
                facetParts.Add($"[\"categories:{modLoader.ToLowerInvariant()}\"]");
            
            var facets = Uri.EscapeDataString("[" + string.Join(",", facetParts) + "]");
            var q = Uri.EscapeDataString(query);
            
            var response = await _httpClient.GetAsync($"search?query={q}&facets={facets}&index=downloads&limit=50&offset={offset}");
            if (!response.IsSuccessStatusCode)
                throw new Exception($"Modrinth API error: {response.StatusCode}");
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> GetProjectAsync(string projectId)
        {
            var response = await _httpClient.GetAsync($"project/{projectId}");
            if (!response.IsSuccessStatusCode) throw new Exception("Modrinth Project API error");
            return await response.Content.ReadAsStringAsync();
        }
        
        public async Task<string> GetProjectVersionsAsync(string projectId)
        {
             var response = await _httpClient.GetAsync($"project/{projectId}/version");
             if (!response.IsSuccessStatusCode) throw new Exception("Modrinth Versions API error");
             return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> GetVersionAsync(string versionId)
        {
            var response = await _httpClient.GetAsync($"version/{versionId}");
            if (!response.IsSuccessStatusCode) throw new Exception("Modrinth Version API error");
            return await response.Content.ReadAsStringAsync();
        }

    }
}
