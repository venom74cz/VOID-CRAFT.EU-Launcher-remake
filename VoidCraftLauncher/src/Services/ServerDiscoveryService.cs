using fNbt;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VoidCraftLauncher.Models;

namespace VoidCraftLauncher.Services;

public sealed class ServerDiscoveryService
{
    private readonly LauncherService _launcherService;
    private readonly ObservabilityService _observability;

    public ServerDiscoveryService(LauncherService launcherService, ObservabilityService observability)
    {
        _launcherService = launcherService;
        _observability = observability;
    }

    public IReadOnlyList<ServerInfo> DiscoverInstalledServers(IEnumerable<ModpackInfo> modpacks)
    {
        var discovered = new List<ServerInfo>();
        var seenEndpoints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var modpack in modpacks.Where(pack => !string.IsNullOrWhiteSpace(pack.Name)))
        {
            var serversDatPath = Path.Combine(_launcherService.GetModpackPath(modpack.Name), "servers.dat");
            if (!File.Exists(serversDatPath))
            {
                continue;
            }

            try
            {
                var nbt = new NbtFile();
                nbt.LoadFromFile(serversDatPath);

                if (nbt.RootTag["servers"] is not NbtList serverList)
                {
                    continue;
                }

                foreach (var tag in serverList)
                {
                    if (tag is not NbtCompound serverTag)
                    {
                        continue;
                    }

                    var endpoint = serverTag.Get<NbtString>("ip")?.Value?.Trim();
                    if (string.IsNullOrWhiteSpace(endpoint))
                    {
                        continue;
                    }

                    var (address, port) = ParseEndpoint(endpoint);
                    var endpointKey = $"{address}:{port}";
                    if (!seenEndpoints.Add(endpointKey))
                    {
                        continue;
                    }

                    var serverName = serverTag.Get<NbtString>("name")?.Value;
                    discovered.Add(new ServerInfo
                    {
                        Name = string.IsNullOrWhiteSpace(serverName) ? endpoint : serverName,
                        Address = address,
                        Port = port,
                        LinkedModpackName = modpack.Name,
                        LinkedModpackProjectId = modpack.ProjectId,
                        RequiredMcVersion = modpack.IsCustomProfile ? modpack.CustomMcVersion : string.Empty,
                        RequiredModLoader = modpack.IsCustomProfile ? modpack.CustomModLoader : string.Empty,
                        AutoConnect = true,
                        StatusText = "Načítám...",
                        IsAutoDiscovered = true,
                        DiscoverySource = $"Detekováno z {modpack.Name}"
                    });
                }
            }
            catch (Exception ex)
            {
                LogService.Error($"Server discovery failed for {modpack.Name}", ex);
                _observability.RecordFallback("ServerDiscovery.Load", modpack.Name, "skip-instance", ex.Message);
            }
        }

        return discovered;
    }

    private static (string Address, int Port) ParseEndpoint(string endpoint)
    {
        var trimmed = endpoint.Trim();
        var lastColonIndex = trimmed.LastIndexOf(':');
        if (lastColonIndex > 0 && lastColonIndex < trimmed.Length - 1 && int.TryParse(trimmed[(lastColonIndex + 1)..], out var port))
        {
            return (trimmed[..lastColonIndex], port);
        }

        return (trimmed, 25565);
    }
}