using System;
using System.Text.Json.Serialization;

namespace VoidCraftLauncher.Models;

public sealed class AiProfile
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = "Nová AI";

    [JsonPropertyName("baseUrl")]
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";

    [JsonPropertyName("apiKey")]
    public string ApiKey { get; set; } = string.Empty;

    [JsonPropertyName("modelName")]
    public string ModelName { get; set; } = "gpt-4o-mini";

    [JsonPropertyName("isDefault")]
    public bool IsDefault { get; set; }

    [JsonPropertyName("rawCurlCommand")]
    public string RawCurlCommand { get; set; } = string.Empty;
}
