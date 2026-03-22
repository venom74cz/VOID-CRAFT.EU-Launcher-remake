using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace VoidCraftLauncher.Models;

/// <summary>
/// Represents a Minecraft server entry in the Server Hub.
/// </summary>
public partial class ServerInfo : ObservableObject
{
    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private string _address = "";

    [ObservableProperty]
    private int _port = 25565;

    [ObservableProperty]
    private string _motd = "";

    [ObservableProperty]
    private bool _isOnline;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayerCountLabel))]
    private int _playerCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayerCountLabel))]
    private int _maxPlayers;

    [ObservableProperty]
    private string _statusText = "Načítám...";

    [ObservableProperty]
    private bool _isPinned;

    /// <summary>The linked modpack name for Quick Connect (auto-download + launch).</summary>
    [ObservableProperty]
    private string? _linkedModpackName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LinkedModCountLabel))]
    private int _linkedModCount;

    /// <summary>The linked CurseForge or Modrinth project ID.</summary>
    [ObservableProperty]
    private int _linkedModpackProjectId;

    /// <summary>Minecraft version required by the server.</summary>
    [ObservableProperty]
    private string _requiredMcVersion = "";

    /// <summary>Modloader expected by the server (forge, fabric, neoforge).</summary>
    [ObservableProperty]
    private string _requiredModLoader = "";

    /// <summary>Whether to auto-connect to this server after game starts.</summary>
    [ObservableProperty]
    private bool _autoConnect;

    /// <summary>Optional icon URL.</summary>
    [ObservableProperty]
    private string? _iconUrl;

    /// <summary>True when the server entry was discovered automatically from an instance.</summary>
    [ObservableProperty]
    private bool _isAutoDiscovered;

    /// <summary>Human-readable source label for discovered servers.</summary>
    [ObservableProperty]
    private string? _discoverySource;

    /// <summary>Timestamp of last status poll.</summary>
    public DateTime LastPolled { get; set; }

    public string PlayerCountLabel => MaxPlayers > 0 ? $"{PlayerCount}/{MaxPlayers} hráčů" : $"{PlayerCount} hráčů";

    public string LinkedModCountLabel => LinkedModCount > 0 ? $"{LinkedModCount} modů" : "Nezjištěno";
}
