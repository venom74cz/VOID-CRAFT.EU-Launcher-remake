using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using VoidCraftLauncher.Models.CreatorStudio;
using VoidCraftLauncher.Services;

namespace VoidCraftLauncher.Services.CreatorStudio;

public sealed class GitHubReleaseService
{
    private const string WorkflowRelativePath = ".github/workflows/voidpack-release.yml";
    private const string ScriptRelativePath = ".github/scripts/build_voidpack.py";

    private readonly HttpClient _httpClient;

    public GitHubReleaseService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public bool TryParseRepository(string? remoteUrl, out GitHubRepositoryReference repository)
    {
        repository = new GitHubRepositoryReference(string.Empty, string.Empty, string.Empty);
        if (string.IsNullOrWhiteSpace(remoteUrl))
        {
            return false;
        }

        var normalized = remoteUrl.Trim();
        normalized = normalized.EndsWith(".git", StringComparison.OrdinalIgnoreCase)
            ? normalized[..^4]
            : normalized;

        string owner;
        string repo;

        if (normalized.StartsWith("git@github.com:", StringComparison.OrdinalIgnoreCase))
        {
            var payload = normalized[15..];
            var parts = payload.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
            {
                return false;
            }

            owner = parts[0];
            repo = parts[1];
        }
        else if (Uri.TryCreate(normalized, UriKind.Absolute, out var uri) &&
                 string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            var parts = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                return false;
            }

            owner = parts[0];
            repo = parts[1];
        }
        else
        {
            return false;
        }

        repository = new GitHubRepositoryReference(owner, repo, $"https://github.com/{owner}/{repo}");
        return true;
    }

    public string BuildTagName(string version)
    {
        var normalizedVersion = string.IsNullOrWhiteSpace(version) ? "0.1.0" : version.Trim();
        return normalizedVersion.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? normalizedVersion : $"v{normalizedVersion}";
    }

    public string BuildAssetFileName(CreatorManifest manifest)
    {
        var slug = string.IsNullOrWhiteSpace(manifest.Slug) ? "voidpack" : manifest.Slug.Trim();
        var version = string.IsNullOrWhiteSpace(manifest.Version) ? "0.1.0" : manifest.Version.Trim();
        return $"{slug}-{version}.voidpack";
    }

    public bool HasWorkflowFiles(string workspacePath)
    {
        return File.Exists(Path.Combine(workspacePath, WorkflowRelativePath.Replace('/', Path.DirectorySeparatorChar))) &&
               File.Exists(Path.Combine(workspacePath, ScriptRelativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    public IReadOnlyList<string> GetPublishTrackedPaths(string workspacePath, IEnumerable<string>? trackedFiles = null)
    {
        var normalizedTrackedFiles = new HashSet<string>(
            (trackedFiles ?? Array.Empty<string>())
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(NormalizeRelativePath),
            StringComparer.OrdinalIgnoreCase);

        var publishPaths = new HashSet<string>(InstanceExportService.BuildGitPublishTrackedPaths(workspacePath, normalizedTrackedFiles), StringComparer.OrdinalIgnoreCase);
        AddIfExistingOrTracked(publishPaths, normalizedTrackedFiles, workspacePath, WorkflowRelativePath);
        AddIfExistingOrTracked(publishPaths, normalizedTrackedFiles, workspacePath, ScriptRelativePath);

        return publishPaths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public IReadOnlyList<string> GetPublishStatusScopePaths(string workspacePath)
    {
        var publishPaths = new HashSet<string>(InstanceExportService.GetGitPublishStatusRoots(), StringComparer.OrdinalIgnoreCase);

        var workflowPath = Path.Combine(workspacePath, WorkflowRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var scriptPath = Path.Combine(workspacePath, ScriptRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(workflowPath))
        {
            publishPaths.Add(WorkflowRelativePath);
        }

        if (File.Exists(scriptPath))
        {
            publishPaths.Add(ScriptRelativePath);
        }

        return publishPaths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public bool IsPublishTrackedPath(string? relativePath)
    {
        var normalizedPath = NormalizeRelativePath(relativePath);
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return false;
        }

        return string.Equals(normalizedPath, WorkflowRelativePath, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalizedPath, ScriptRelativePath, StringComparison.OrdinalIgnoreCase) ||
             InstanceExportService.IsGitPublishPath(normalizedPath);
    }

    public async Task EnsureVoidpackWorkflowAsync(string workspacePath)
    {
        var workflowPath = Path.Combine(workspacePath, WorkflowRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var scriptPath = Path.Combine(workspacePath, ScriptRelativePath.Replace('/', Path.DirectorySeparatorChar));

        Directory.CreateDirectory(Path.GetDirectoryName(workflowPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(scriptPath)!);

        await File.WriteAllTextAsync(workflowPath, BuildWorkflowYaml(), Encoding.UTF8);
        await File.WriteAllTextAsync(scriptPath, BuildBuildScript(), Encoding.UTF8);
    }

    public async Task<GitHubReleaseAssetInfo?> WaitForReleaseAssetAsync(
        GitHubRepositoryReference repository,
        string tagName,
        string assetName,
        Action<string>? statusCallback = null,
        CancellationToken cancellationToken = default)
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddMinutes(6);
        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            cancellationToken.ThrowIfCancellationRequested();
            statusCallback?.Invoke($"Čekám na publikovaný GitHub release {tagName}...");

            using var response = await _httpClient.GetAsync($"https://api.github.com/repos/{repository.Owner}/{repository.Repository}/releases/tags/{Uri.EscapeDataString(tagName)}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                var root = JsonNode.Parse(body);
                var assets = root?["assets"]?.AsArray();
                var assetNode = FindAssetByName(assets, assetName);
                var shaNode = FindAssetByName(assets, assetName + ".sha256");

                if (assetNode != null && shaNode != null)
                {
                    var shaUrl = shaNode["browser_download_url"]?.ToString() ?? string.Empty;
                    var shaResponse = await _httpClient.GetStringAsync(shaUrl, cancellationToken);
                    var shaValue = shaResponse.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim();

                    return new GitHubReleaseAssetInfo(
                        repository,
                        tagName,
                        assetNode["name"]?.ToString() ?? assetName,
                        assetNode["browser_download_url"]?.ToString() ?? string.Empty,
                        root?["html_url"]?.ToString() ?? repository.WebUrl,
                        assetNode["size"]?.GetValue<long?>() ?? 0,
                        shaValue,
                        0);
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
        }

        return null;
    }

    public string BuildTaggedRawUrl(GitHubRepositoryReference repository, string tagName, string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/').TrimStart('/');
        return $"https://raw.githubusercontent.com/{repository.Owner}/{repository.Repository}/{Uri.EscapeDataString(tagName)}/{normalized}";
    }

    private static void AddIfExistingOrTracked(HashSet<string> publishPaths, HashSet<string> trackedPaths, string workspacePath, string relativePath)
    {
        var normalizedPath = NormalizeRelativePath(relativePath);
        var absolutePath = Path.Combine(workspacePath, normalizedPath.Replace('/', Path.DirectorySeparatorChar));
        if (trackedPaths.Contains(normalizedPath) || File.Exists(absolutePath))
        {
            publishPaths.Add(normalizedPath);
        }
    }

    private static string NormalizeRelativePath(string? relativePath)
    {
        return string.IsNullOrWhiteSpace(relativePath)
            ? string.Empty
            : relativePath.Replace('\\', '/').Trim().TrimStart('/');
    }

    private static JsonNode? FindAssetByName(JsonArray? assets, string assetName)
    {
        if (assets == null)
        {
            return null;
        }

        foreach (var asset in assets)
        {
            if (string.Equals(asset?["name"]?.ToString(), assetName, StringComparison.OrdinalIgnoreCase))
            {
                return asset;
            }
        }

        return null;
    }

    private static string BuildWorkflowYaml()
    {
        return """
name: Build VOIDPACK Release

on:
  push:
    tags:
      - 'v*'

permissions:
  contents: write

jobs:
  build-voidpack:
    runs-on: ubuntu-latest

    steps:
      - name: Checkout
        uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - name: Setup Python
        uses: actions/setup-python@v5
        with:
          python-version: '3.12'

      - name: Build VOIDPACK
        run: python .github/scripts/build_voidpack.py --workspace . --tag "${{ github.ref_name }}"

      - name: Resolve artifact paths
        id: artifact
        shell: bash
        run: |
          asset_path=$(ls dist/*.voidpack | head -n 1)
          sha_path="${asset_path}.sha256"
          echo "asset_path=${asset_path}" >> "$GITHUB_OUTPUT"
          echo "sha_path=${sha_path}" >> "$GITHUB_OUTPUT"

      - name: Publish GitHub Release
        env:
          GH_TOKEN: ${{ github.token }}
        shell: bash
        run: |
          tag="${{ github.ref_name }}"
          if gh release view "$tag" > /dev/null 2>&1; then
            gh release upload "$tag" "${{ steps.artifact.outputs.asset_path }}" "${{ steps.artifact.outputs.sha_path }}" --clobber
                        gh release edit "$tag" --draft=false
          else
            gh release create "$tag" "${{ steps.artifact.outputs.asset_path }}" "${{ steps.artifact.outputs.sha_path }}" --title "$tag" --generate-notes
          fi
""";
    }

    private static string BuildBuildScript()
    {
        return """
import argparse
import hashlib
import json
import os
from pathlib import Path
import zipfile

INCLUDE_PATHS = [
    "saves",
    "config",
    "mods",
    "options.txt",
    "resourcepacks",
    "shaderpacks",
    "assets/branding",
]

MOD_METADATA_FILE = "mods_metadata.json"
INSTANCE_MANIFEST_FILE = "voidcraft_manifest.json"


def load_creator_manifest(workspace: Path) -> dict:
    manifest_path = workspace / "creator_manifest.json"
    if not manifest_path.exists():
        raise SystemExit("creator_manifest.json chybi. Workflow nema z ceho sestavit VOIDPACK release.")
    return json.loads(manifest_path.read_text(encoding="utf-8"))


def normalize_relative(path: Path) -> str:
    return str(path).replace("\\", "/")


def is_tracked_mod_binary(path: Path) -> bool:
    return path.name.endswith(".jar") or path.name.endswith(".jar.disabled")


def normalize_mod_file_name(file_name: str) -> str:
    return file_name[: -len(".disabled")] if file_name.endswith(".disabled") else file_name


def format_mod_display_name(file_name: str) -> str:
    return Path(normalize_mod_file_name(file_name)).stem.replace("_", " ").replace("-", " ")


def read_json_file(path: Path):
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except Exception:
        return None


def load_instance_manifest(workspace: Path) -> dict:
    manifest_path = workspace / INSTANCE_MANIFEST_FILE
    if not manifest_path.exists():
        return {}
    payload = read_json_file(manifest_path)
    return payload if isinstance(payload, dict) else {}


def gather_files(workspace: Path) -> list[Path]:
    gathered: list[Path] = []
    for include_path in INCLUDE_PATHS:
        candidate = workspace / include_path
        if candidate.is_dir():
            if include_path == "mods":
                for file_path in sorted(candidate.rglob("*")):
                    if not file_path.is_file():
                        continue
                    relative = file_path.relative_to(workspace)
                    if is_tracked_mod_binary(file_path):
                        continue
                    if ".mod_metadata" in relative.parts:
                        continue
                    gathered.append(relative)
            else:
                for file_path in sorted(candidate.rglob("*")):
                    if file_path.is_file():
                        gathered.append(file_path.relative_to(workspace))
        elif candidate.is_file():
            gathered.append(candidate.relative_to(workspace))
    deduped = []
    seen = set()
    for item in gathered:
        key = normalize_relative(item)
        if key not in seen:
            seen.add(key)
            deduped.append(item)
    return deduped


def build_mod_entries(workspace: Path) -> list[dict]:
    metadata_index: dict[str, dict] = {}

    metadata_path = workspace / MOD_METADATA_FILE
    metadata_payload = read_json_file(metadata_path)
    if isinstance(metadata_payload, list):
        for entry in metadata_payload:
            if not isinstance(entry, dict):
                continue
            file_name = str(entry.get("FileName") or entry.get("fileName") or "").strip()
            if not file_name:
                continue
            metadata_index[normalize_mod_file_name(file_name)] = entry

    sidecar_directory = workspace / "mods" / ".mod_metadata"
    if sidecar_directory.exists():
        for sidecar_path in sorted(sidecar_directory.glob("*.json")):
            payload = read_json_file(sidecar_path)
            if not isinstance(payload, dict):
                continue
            file_name = str(payload.get("FileName") or payload.get("fileName") or "").strip()
            if not file_name:
                continue
            metadata_index[normalize_mod_file_name(file_name)] = payload

    mods_directory = workspace / "mods"
    if not mods_directory.exists():
        return []

    normalized: list[dict] = []
    for mod_path in sorted(mods_directory.iterdir()):
        if not mod_path.is_file() or not is_tracked_mod_binary(mod_path):
            continue

        normalized_file_name = normalize_mod_file_name(mod_path.name)
        metadata = metadata_index.get(normalized_file_name, {})
        source = str(metadata.get("Source") or metadata.get("source") or "Manual")
        project_id = str(metadata.get("ProjectId") or metadata.get("projectId") or metadata.get("Id") or "")
        file_id = str(metadata.get("FileId") or metadata.get("fileId") or "")
        version_id = str(metadata.get("VersionId") or metadata.get("versionId") or "")
        download_url = str(metadata.get("DownloadUrl") or metadata.get("downloadUrl") or "")
        can_auto_download = bool(download_url) or (
            source.lower() == "curseforge" and project_id and file_id
        ) or (
            source.lower() == "modrinth" and (version_id or project_id)
        )

        normalized.append({
            "FileName": normalized_file_name,
            "Name": str(metadata.get("Name") or metadata.get("name") or format_mod_display_name(normalized_file_name)),
            "Source": source,
            "ProjectId": project_id,
            "FileId": file_id,
            "VersionId": version_id,
            "DownloadUrl": download_url,
            "Summary": str(metadata.get("Summary") or metadata.get("summary") or metadata.get("Description") or ""),
            "Author": str(metadata.get("Author") or metadata.get("author") or ""),
            "IconUrl": str(metadata.get("IconUrl") or metadata.get("iconUrl") or ""),
            "WebLink": str(metadata.get("WebLink") or metadata.get("webLink") or ""),
            "IsEnabled": not mod_path.name.endswith(".disabled"),
            "RequiresManualFile": not can_auto_download,
        })

    return normalized


def build_voidpack_manifest(instance_name: str, instance_manifest: dict, files: list[Path], mod_entries: list[dict]) -> dict:
    return {
        "LauncherVersion": "github-workflow",
        "InstanceName": instance_name,
        "ExportedAt": __import__("datetime").datetime.utcnow().isoformat() + "Z",
        "Categories": sorted({path.parts[0] for path in files if len(path.parts) > 0}),
        "IncludedPaths": [normalize_relative(path) for path in files],
        "ModEntryCount": len(mod_entries),
        "DownloadableModCount": sum(1 for item in mod_entries if not item.get("RequiresManualFile")),
        "ManualModCount": sum(1 for item in mod_entries if item.get("RequiresManualFile")),
        "MinecraftVersion": str(instance_manifest.get("minecraft_version") or ""),
        "ModLoader": str(instance_manifest.get("mod_loader_id") or ""),
    }


def build_archive(workspace: Path, tag: str) -> Path:
    creator_manifest = load_creator_manifest(workspace)
    slug = str(creator_manifest.get("Slug") or "voidpack").strip() or "voidpack"
    version = str(creator_manifest.get("Version") or tag.lstrip("v") or "0.1.0").strip() or "0.1.0"
    instance_name = str(creator_manifest.get("PackName") or creator_manifest.get("Slug") or "VOIDPACK").strip() or "VOIDPACK"
    asset_name = f"{slug}-{version}.voidpack"
    output_dir = workspace / "dist"
    output_dir.mkdir(parents=True, exist_ok=True)
    asset_path = output_dir / asset_name
    mod_entries = build_mod_entries(workspace)
    included_files = gather_files(workspace)
    instance_manifest = load_instance_manifest(workspace)
    manifest_payload = build_voidpack_manifest(instance_name, instance_manifest, included_files, mod_entries)

    with zipfile.ZipFile(asset_path, "w", compression=zipfile.ZIP_DEFLATED) as archive:
        archive.writestr("voidpack_manifest.json", json.dumps(manifest_payload, indent=2, ensure_ascii=False))
        archive.writestr("voidpack_modlist.json", json.dumps(mod_entries, indent=2, ensure_ascii=False))
        for relative_path in included_files:
            archive.write(workspace / relative_path, normalize_relative(relative_path))

    sha_path = Path(str(asset_path) + ".sha256")
    sha_value = hashlib.sha256(asset_path.read_bytes()).hexdigest()
    sha_path.write_text(sha_value + "\n", encoding="utf-8")
    print(asset_path)
    return asset_path


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--workspace", default=".")
    parser.add_argument("--tag", default="v0.1.0")
    args = parser.parse_args()
    build_archive(Path(args.workspace).resolve(), args.tag)


if __name__ == "__main__":
    main()
""";
    }
}

public sealed record GitHubRepositoryReference(string Owner, string Repository, string WebUrl);

public sealed record GitHubReleaseAssetInfo(
    GitHubRepositoryReference Repository,
    string TagName,
    string AssetName,
    string DownloadUrl,
    string ReleasePageUrl,
    long FileSizeBytes,
    string FileHashSha256,
    int ModCount);