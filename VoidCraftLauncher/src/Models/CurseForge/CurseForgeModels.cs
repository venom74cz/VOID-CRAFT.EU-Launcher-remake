using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VoidCraftLauncher.Models.CurseForge
{
    // Struktura manifest.json uvnitř modpacku
    public class CurseForgeManifest
    {
        [JsonPropertyName("minecraft")]
        public MinecraftInfo Minecraft { get; set; }

        [JsonPropertyName("manifestType")]
        public string ManifestType { get; set; }

        [JsonPropertyName("manifestVersion")]
        public int ManifestVersion { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("version")]
        public string Version { get; set; }

        [JsonPropertyName("author")]
        public string Author { get; set; }

        [JsonPropertyName("files")]
        public List<ManifestFile> Files { get; set; }

        [JsonPropertyName("overrides")]
        public string Overrides { get; set; }
    }

    public class MinecraftInfo
    {
        [JsonPropertyName("version")]
        public string Version { get; set; }

        [JsonPropertyName("modLoaders")]
        public List<ModLoaderInfo> ModLoaders { get; set; }
    }

    public class ModLoaderInfo
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("primary")]
        public bool Primary { get; set; }
    }

    public class ManifestFile
    {
        [JsonPropertyName("projectID")]
        public int ProjectID { get; set; }

        [JsonPropertyName("fileID")]
        public int FileID { get; set; }

        [JsonPropertyName("required")]
        public bool Required { get; set; }
    }
    
    // Pro deserializaci odpovědi z /mods/files
    public class CurseFileDatas
    {
        [JsonPropertyName("data")]
        public List<CurseFile> Data { get; set; }
    }

    public class CurseFile
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("modId")]
        public int ModId { get; set; }

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; }

        [JsonPropertyName("fileName")]
        public string FileName { get; set; }

        [JsonPropertyName("downloadUrl")]
        public string DownloadUrl { get; set; }

        [JsonPropertyName("fileLength")]
        public long FileLength { get; set; }
        
        [JsonPropertyName("hashes")]
        public List<FileHash> Hashes { get; set; }
    }

    public class FileHash
    {
        [JsonPropertyName("value")]
        public string Value { get; set; }

        [JsonPropertyName("algo")]
        public int Algo { get; set; } // 1 = SHA1, 2 = MD5
    }

    // Pro odpověď z /v1/mods (batch)
    public class CurseModsData
    {
        [JsonPropertyName("data")]
        public List<CurseMod> Data { get; set; }
    }

    public class CurseMod
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("classId")]
        public int ClassId { get; set; } // 6=Mod, 12=ResourcePack, 6552=ShaderPack?

        [JsonPropertyName("name")]
        public string Name { get; set; }
        
        [JsonPropertyName("slug")]
        public string Slug { get; set; }

        [JsonPropertyName("summary")]
        public string Summary { get; set; }

        [JsonPropertyName("categories")]
        public List<Category> Categories { get; set; }
    }

    public class Category
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("slug")]
        public string Slug { get; set; }
    }
}
