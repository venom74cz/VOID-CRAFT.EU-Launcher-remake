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

        public async Task<string> SearchModpacksAsync(string query)
        {
            // Sort by Popularity (2) descending
            var response = await _httpClient.GetAsync($"v1/mods/search?gameId=432&classId=4471&sortField=2&sortOrder=desc&searchFilter={Uri.EscapeDataString(query)}");
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
    }
}
