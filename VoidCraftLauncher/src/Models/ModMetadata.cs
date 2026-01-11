using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VoidCraftLauncher.Models
{
    public class ModMetadata
    {
        [JsonPropertyName("fileName")]
        public string FileName { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("slug")]
        public string Slug { get; set; }

        [JsonPropertyName("summary")]
        public string Summary { get; set; }

        [JsonPropertyName("categories")]
        public List<string> Categories { get; set; } = new();
    }
}
