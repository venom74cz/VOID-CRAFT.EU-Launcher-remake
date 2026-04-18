using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace VoidCraftLauncher.Models;

public sealed class AiProfile : INotifyPropertyChanged
{
    private bool _isCurlMode;

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

    [JsonPropertyName("customSystemPrompt")]
    public string CustomSystemPrompt { get; set; } = string.Empty;

    [JsonPropertyName("maxContextChars")]
    public int MaxContextChars { get; set; } = 8000;

    [JsonPropertyName("isCurlMode")]
    public bool IsCurlMode
    {
        get => _isCurlMode;
        set
        {
            if (_isCurlMode != value)
            {
                _isCurlMode = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
