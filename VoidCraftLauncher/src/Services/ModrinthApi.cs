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
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "VoidCraftLauncher/2.0 (admin@void-craft.eu)");
        }

        public async Task<string> SearchModpacksAsync(string query)
        {
            // Facet for project_type=modpack: [["project_type:modpack"]]
            // Encoded: %5B%5B%22project_type%3Amodpack%22%5D%5D
            var facets = Uri.EscapeDataString("[[\"project_type:modpack\"]]");
            var q = Uri.EscapeDataString(query);
            
            // Sort by downloads by default for popularity
            var response = await _httpClient.GetAsync($"search?query={q}&facets={facets}&index=downloads");
            
            if (!response.IsSuccessStatusCode)
            {
                 throw new Exception($"Modrinth API error: {response.StatusCode}");
            }
            
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

    }
}
