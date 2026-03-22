using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using VoidCraftLauncher.Models;

namespace VoidCraftLauncher.Services;

/// <summary>
/// Polls Minecraft server status via mcsrvstat.us API with configurable interval and caching.
/// Supports both the pinned VOID-CRAFT server and custom community servers.
/// Falls back gracefully on network errors.
/// </summary>
public class ServerStatusService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ObservabilityService _observability;
    private readonly Dictionary<string, (ServerInfo Info, DateTime CachedAt)> _cache = new();
    private readonly TimeSpan _cacheLifetime = TimeSpan.FromSeconds(45);
    private CancellationTokenSource? _cts;

    public event Action<ServerInfo>? StatusUpdated;

    public ServerStatusService(HttpClient httpClient, ObservabilityService observability)
    {
        _httpClient = httpClient;
        _observability = observability;
    }

    /// <summary>
    /// Polls a single server and returns updated info. Uses cache if fresh.
    /// </summary>
    public async Task<ServerInfo> PollAsync(ServerInfo server)
    {
        var key = $"{server.Address}:{server.Port}";

        if (_cache.TryGetValue(key, out var cached) && DateTime.UtcNow - cached.CachedAt < _cacheLifetime)
        {
            CopyStatus(cached.Info, server);
            return server;
        }

        using var operation = _observability.BeginOperation("ServerStatus.Poll");

        try
        {
            var endpoint = server.Port == 25565
                ? $"https://api.mcsrvstat.us/2/{server.Address}"
                : $"https://api.mcsrvstat.us/2/{server.Address}:{server.Port}";

            var response = await _httpClient.GetStringAsync(endpoint);
            var json = JsonNode.Parse(response);

            if (json?["online"]?.GetValue<bool>() == true)
            {
                var players = json["players"];
                server.IsOnline = true;
                server.PlayerCount = players?["online"]?.GetValue<int>() ?? 0;
                server.MaxPlayers = players?["max"]?.GetValue<int>() ?? 0;
                server.StatusText = "Online";

                var motdList = json["motd"]?["clean"]?.AsArray();
                if (motdList != null && motdList.Count > 0)
                {
                    var motd = string.Join(" ", motdList);
                    server.Motd = string.IsNullOrWhiteSpace(motd) ? server.Name : motd.Trim();
                }
            }
            else
            {
                server.IsOnline = false;
                server.StatusText = "Offline";
                server.PlayerCount = 0;
                server.Motd = "Server nedostupný";
            }

            server.LastPolled = DateTime.UtcNow;
            _cache[key] = (CreateCacheSnapshot(server), DateTime.UtcNow);
            StatusUpdated?.Invoke(server);
            operation.Complete();
            return server;
        }
        catch (Exception ex)
        {
            LogService.Error($"ServerStatus poll failed for {key}", ex);

            if (_cache.TryGetValue(key, out var staleCached))
            {
                CopyStatus(staleCached.Info, server);
                server.LastPolled = DateTime.UtcNow;
                _observability.RecordFallback("ServerStatus.Poll", "live-api", "stale-cache", ex.Message);
                StatusUpdated?.Invoke(server);
                operation.Complete();
                return server;
            }

            server.IsOnline = false;
            server.StatusText = "Chyba";
            server.Motd = "Nepodařilo se načíst stav serveru";
            server.LastPolled = DateTime.UtcNow;
            StatusUpdated?.Invoke(server);
            operation.Fail(ex.Message);
            return server;
        }
    }

    /// <summary>
    /// Starts a background loop that continuously polls all provided servers.
    /// </summary>
    public void StartPollingLoop(IEnumerable<ServerInfo> servers, int intervalMs = 60000)
    {
        StopPolling();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                foreach (var server in servers)
                {
                    if (token.IsCancellationRequested) break;
                    await PollAsync(server);
                }
                try { await Task.Delay(intervalMs, token); }
                catch (TaskCanceledException) { break; }
            }
        }, token);
    }

    public void StopPolling()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    public void InvalidateCache(string address)
    {
        _cache.Remove(address);
    }

    private static void CopyStatus(ServerInfo from, ServerInfo to)
    {
        to.IsOnline = from.IsOnline;
        to.PlayerCount = from.PlayerCount;
        to.MaxPlayers = from.MaxPlayers;
        to.StatusText = from.StatusText;
        to.Motd = from.Motd;
        to.LastPolled = from.LastPolled;
    }

    private static ServerInfo CreateCacheSnapshot(ServerInfo source)
    {
        return new ServerInfo
        {
            Name = source.Name,
            Address = source.Address,
            Port = source.Port,
            Motd = source.Motd,
            IsOnline = source.IsOnline,
            PlayerCount = source.PlayerCount,
            MaxPlayers = source.MaxPlayers,
            StatusText = source.StatusText,
            IsPinned = source.IsPinned,
            LinkedModpackName = source.LinkedModpackName,
            LinkedModpackProjectId = source.LinkedModpackProjectId,
            RequiredMcVersion = source.RequiredMcVersion,
            RequiredModLoader = source.RequiredModLoader,
            AutoConnect = source.AutoConnect,
            IconUrl = source.IconUrl,
            LinkedModCount = source.LinkedModCount,
            IsAutoDiscovered = source.IsAutoDiscovered,
            DiscoverySource = source.DiscoverySource,
            LastPolled = source.LastPolled
        };
    }

    public void Dispose()
    {
        StopPolling();
    }
}
